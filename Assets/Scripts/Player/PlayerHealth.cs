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
    /// 無敵（強制無敵、翻滾前段、受擊後）→ 盾反（舉盾、正面、可盾反、舉盾不到 parryWindow）→ 格擋（舉盾、正面、可擋）→ 直接命中。
    /// 格擋只吃 (1 − blockDamageReduction) 的傷害、耐力扣原傷害 × blockStaminaPerDamage × 攻擊的 blockStaminaMultiplier，扣到 0 就破防；
    /// 盾反完全免傷、畫面停頓 parryHitStop 秒並送出 OnParry；給攻擊方的失衡值 parryImbalance 放在回傳的 DamageOutcome，
    /// 經 Hitbox 的 HitReport 回到攻擊方（Boss 的 BossStagger 從 BossController.OnMoveHit 拿）；
    /// 只有直接命中會給 hitInvulnerableTime 的無敵。
    /// 事件順序：OnHealthChanged → OnDamageTaken → OnParry／OnGuardBreak → OnDied。
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
        /// <summary>(剩餘骨頭, 每場次數)。</summary>
        public event Action<int, int> OnBonesChanged;

        public float Current { get; private set; }
        public float Max => stats.maxHealth;
        public bool IsDead => Current <= 0f;
        public int BonesLeft { get; private set; }
        public int BonesMax => stats.boneCount;
        /// <summary>被直接打中後的無敵時間內。</summary>
        public bool IsHitInvulnerable => Time.time < hitInvulnerableUntil;
        public float HitInvulnerableRemaining => Mathf.Max(0f, hitInvulnerableUntil - Time.time);
        /// <summary>強制無敵（Debug F2，之後的演出也可以用）：所有攻擊都當成閃過。</summary>
        public bool ForcedInvulnerable { get; set; }

        PlayerController controller;
        PlayerStamina stamina;
        HitStop hitStop;
        float hitInvulnerableUntil = float.NegativeInfinity;

        void Awake()
        {
            controller = GetComponent<PlayerController>();
            stamina = GetComponent<PlayerStamina>();
            hitStop = GetComponent<HitStop>();
            Current = stats.maxHealth;
            BonesLeft = stats.boneCount;
        }

        void Start()
        {
            // 讓先訂閱的 UI 拿到初始值
            OnHealthChanged?.Invoke(Current, Max);
            OnBonesChanged?.Invoke(BonesLeft, BonesMax);
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
            else if (guarded && info.canParry && held <= stats.parryWindow)
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
