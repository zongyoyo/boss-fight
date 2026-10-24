using System;
using BossFight.Core;
using UnityEngine;

namespace BossFight.Player
{
    /// <summary>玩家結算一次攻擊後的紀錄，給 PlayerController（中斷蓄力、破防）、Debug 顯示，以及之後的 UI 與音效。</summary>
    public readonly struct PlayerDamageReport
    {
        public readonly DamageInfo info;
        public readonly DamageResult result;
        /// <summary>實際扣掉的血量。</summary>
        public readonly float healthLost;
        /// <summary>格擋實際扣掉的耐力。</summary>
        public readonly float staminaLost;
        /// <summary>命中時已經舉盾多久（秒）；沒舉盾為 -1。</summary>
        public readonly float blockHeldTime;

        public PlayerDamageReport(in DamageInfo info, DamageResult result, float healthLost, float staminaLost, float blockHeldTime)
        {
            this.info = info;
            this.result = result;
            this.healthLost = healthLost;
            this.staminaLost = staminaLost;
            this.blockHeldTime = blockHeldTime;
        }
    }

    /// <summary>
    /// 刀盾狗的血量、受傷結算與骨頭次數。數值讀 PlayerStats；目前血量、無敵時間、剩餘骨頭存在元件自己身上。
    /// 身上的 Hurtbox 把攻擊轉到 TakeDamage，依序判斷：
    /// 無敵（強制無敵、翻滾前段、受擊後）→ 盾反（舉盾、正面、可盾反、舉盾不到 parryWindow，加上難度修正）→ 格擋（舉盾、正面、可擋）→ 直接命中。
    /// 格擋只吃 (1 − blockDamageReduction) 的傷害、耐力扣原傷害 × blockStaminaPerDamage × 攻擊的 blockStaminaMultiplier，扣到 0 就破防；
    /// 盾反完全免傷、畫面停頓 parryHitStop 秒並送出 OnParry；給攻擊方的失衡值 parryImbalance 放在回傳的 DamageOutcome，
    /// 經 Hitbox 的 HitReport 回到攻擊方（Boss 的 BossStagger 從 BossController.OnMoveHit 拿）；
    /// 只有直接命中會給 hitInvulnerableTime 的無敵。
    /// 骨頭：開場 boneCount 根，也是上限。刀命中（PlayerAttack.OnAttackHit 結果是 Hit）累積 boneGainPerHit、處決命中 boneGainPerExecution，
    /// 被打中、格擋、破防累積 boneGainWhenHurt；湊滿 1 根就加一根骨頭（OnBoneEarned），滿上限時多的丟掉，倒下後不再累積。
    /// 事件順序：OnHealthChanged → OnDamageTaken → 骨頭（OnBoneProgressChanged／OnBonesChanged／OnBoneEarned）→ OnParry／OnGuardBreak → OnDied。
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerHealth : MonoBehaviour, IDamageable
    {
        [SerializeField] PlayerStats stats;

        /// <summary>(目前血量, 上限)。</summary>
        public event Action<float, float> OnHealthChanged;
        public event Action<PlayerDamageReport> OnDamageTaken;
        /// <summary>盾反成功；參數是 (被盾反的攻擊（來源在 info.source）, 給攻擊方的失衡值 PlayerStats.parryImbalance)。給玩家自己的演出用，攻擊方從 HitReport 拿失衡值。</summary>
        public event Action<DamageInfo, float> OnParry;
        /// <summary>舉盾時耐力被打到 0。</summary>
        public event Action OnGuardBreak;
        public event Action OnDied;
        /// <summary>(剩餘骨頭, 上限)。</summary>
        public event Action<int, int> OnBonesChanged;
        /// <summary>下一根骨頭的累積進度 0..1（滿上限時為 0）。</summary>
        public event Action<float> OnBoneProgressChanged;
        /// <summary>累積湊滿、多了一根骨頭（音效用；啃掉不會送）。</summary>
        public event Action OnBoneEarned;

        public float Current { get; private set; }
        public float Max => stats.maxHealth;
        public bool IsDead => Current <= 0f;
        public int BonesLeft { get; private set; }
        public int BonesMax => stats.boneCount;
        /// <summary>下一根骨頭的累積進度 0..1。</summary>
        public float BoneProgress { get; private set; }
        /// <summary>被直接打中後的無敵時間內。</summary>
        public bool IsHitInvulnerable => Time.time < hitInvulnerableUntil;
        public float HitInvulnerableRemaining => Mathf.Max(0f, hitInvulnerableUntil - Time.time);
        /// <summary>強制無敵（Debug F2，之後的演出也可以用）：所有攻擊都當成閃過。</summary>
        public bool ForcedInvulnerable { get; set; }

        PlayerController controller;
        PlayerStamina stamina;
        PlayerAttack attack;
        HitStop hitStop;
        float hitInvulnerableUntil = float.NegativeInfinity;

        // 難度修正的盾反判定加長（PlayerController.Difficulty）
        float ParryWindowBonus => controller != null ? controller.Difficulty.parryWindowBonus : 0f;

        void Awake()
        {
            controller = GetComponent<PlayerController>();
            stamina = GetComponent<PlayerStamina>();
            attack = GetComponent<PlayerAttack>();
            hitStop = GetComponent<HitStop>();
            Current = stats.maxHealth;
            BonesLeft = stats.boneCount;
        }

        void OnEnable()
        {
            if (attack != null) attack.OnAttackHit += HandleAttackHit;
        }

        void OnDisable()
        {
            if (attack != null) attack.OnAttackHit -= HandleAttackHit;
        }

        void Start()
        {
            // 讓先訂閱的 UI 拿到初始值
            OnHealthChanged?.Invoke(Current, Max);
            OnBonesChanged?.Invoke(BonesLeft, BonesMax);
            OnBoneProgressChanged?.Invoke(BoneProgress);
        }

        public DamageOutcome TakeDamage(in DamageInfo info)
        {
            if (IsDead) return DamageResult.Ignored;

            bool blocking = controller.IsBlocking;
            float held = blocking ? controller.BlockHeldTime : -1f;
            bool guarded = blocking && IsInFront(info.sourcePosition);

            DamageResult result;
            float healthLoss = 0f, staminaLoss = 0f;
            if (ForcedInvulnerable || controller.IsInvulnerable || IsHitInvulnerable)
            {
                result = DamageResult.Evaded;
            }
            else if (guarded && info.canParry && held <= stats.parryWindow + ParryWindowBonus)
            {
                result = DamageResult.Parried;
            }
            else if (guarded && info.canBlock)
            {
                staminaLoss = stamina.Drain(info.damage * stats.blockStaminaPerDamage * info.blockStaminaMultiplier);
                healthLoss = info.damage * (1f - stats.blockDamageReduction);
                result = stamina.Current <= 0f ? DamageResult.GuardBroken : DamageResult.Blocked;
            }
            else
            {
                healthLoss = info.damage;
                result = DamageResult.Hit;
                hitInvulnerableUntil = Time.time + stats.hitInvulnerableTime;
            }

            float lost = -SetHealth(Current - healthLoss);
            OnDamageTaken?.Invoke(new PlayerDamageReport(info, result, lost, staminaLoss, held));
            if (result == DamageResult.Hit || result == DamageResult.Blocked || result == DamageResult.GuardBroken)
                AddBoneProgress(stats.boneGainWhenHurt);
            if (result == DamageResult.Parried)
            {
                if (hitStop != null) hitStop.Play(stats.parryHitStop);
                OnParry?.Invoke(info, stats.parryImbalance);
            }
            else if (result == DamageResult.GuardBroken)
            {
                OnGuardBreak?.Invoke();
            }
            if (IsDead) OnDied?.Invoke();
            return new DamageOutcome(result, result == DamageResult.Parried ? stats.parryImbalance : 0f);
        }

        /// <summary>還有骨頭、沒倒下、沒滿血就扣一根並回傳 true；回血由 PlayerController 在動作期間呼叫 Heal。</summary>
        public bool TryUseBone()
        {
            if (IsDead || BonesLeft <= 0 || Current >= Max) return false;
            BonesLeft--;
            OnBonesChanged?.Invoke(BonesLeft, BonesMax);
            return true;
        }

        public void Heal(float amount)
        {
            if (IsDead || amount <= 0f) return;
            SetHealth(Current + amount);
        }

        // 刀打中有效果才算（Boss 無敵時是 Evaded）；處決那一刀只算處決的量
        void HandleAttackHit(HitReport report)
        {
            if (report.result != DamageResult.Hit) return;
            AddBoneProgress(report.info.isExecution ? stats.boneGainPerExecution : stats.boneGainPerHit);
        }

        /// <summary>累積骨頭（單位：根）。湊滿 1 根就加一根；滿上限時進度歸零、多的丟掉。</summary>
        void AddBoneProgress(float amount)
        {
            if (IsDead || amount <= 0f || BonesLeft >= BonesMax) return;

            // 0.05 累加 20 次會停在 0.99999，留一點誤差
            const float epsilon = 1e-4f;
            float progress = BoneProgress + amount;
            int earned = 0;
            while (progress >= 1f - epsilon && BonesLeft + earned < BonesMax)
            {
                earned++;
                progress = Mathf.Max(0f, progress - 1f);
            }
            if (BonesLeft + earned >= BonesMax) progress = 0f;

            BoneProgress = progress;
            OnBoneProgressChanged?.Invoke(BoneProgress);
            if (earned == 0) return;
            BonesLeft += earned;
            OnBonesChanged?.Invoke(BonesLeft, BonesMax);
            OnBoneEarned?.Invoke();
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD || BOSSFIGHT_DEV_TOOLS
        /// <summary>Debug 用：直接設定血量（F7 設 0 測失敗）。不會觸發 OnDamageTaken；設到 0 會倒下。</summary>
        public void DebugSetHealth(float value)
        {
            if (IsDead) return;
            SetHealth(value);
            if (IsDead) OnDied?.Invoke();
        }
#endif

        // 攻擊來源在面向左右 blockAngle / 2 以內；貼在身上分不出方向時算正面
        bool IsInFront(Vector3 source)
        {
            Vector3 to = source - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude < 1e-4f) return true;
            Vector3 forward = transform.forward;
            forward.y = 0f;
            return Vector3.Angle(forward, to) <= stats.blockAngle * 0.5f;
        }

        /// <returns>血量的變化量（扣血為負）。</returns>
        float SetHealth(float value)
        {
            value = Mathf.Clamp(value, 0f, Max);
            // 啃骨頭是每幀一點一點加的，浮點誤差會停在 99.99999，看起來滿血卻還能再啃一根
            if (Mathf.Approximately(value, Max)) value = Max;
            float delta = value - Current;
            if (delta == 0f) return 0f;
            Current = value;
            OnHealthChanged?.Invoke(Current, Max);
            return delta;
        }
    }
}
