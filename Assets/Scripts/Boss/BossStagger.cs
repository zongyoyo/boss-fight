using System;
using BossFight.Core;
using BossFight.Player;
using UnityEngine;

namespace BossFight.Boss
{
    /// <summary>為什麼離開頭暈（OnStunEnded 送出）。</summary>
    public enum StunEndReason
    {
        /// <summary>頭暈時間到。</summary>
        Timeout,
        /// <summary>被處決。</summary>
        Executed,
        /// <summary>被轉階段或倒下打斷。</summary>
        Interrupted,
    }

    /// <summary>
    /// OIIA 貓的失衡值、頭暈與處決（企劃書「Boss 數值」的失衡值規則）。
    /// 增加：玩家的攻擊直接命中時加 DamageInfo.imbalance（輕攻擊 +3、重攻擊 +15，數值在 PlayerStats）；
    /// 玩家盾反了這隻 Boss 的攻擊時加 PlayerHealth.OnParry 送來的值（+25，PlayerStats.parryImbalance）；招式可以呼叫 AddImbalance（龍捲風 +40）。
    /// 頭暈、轉階段、倒下、還沒開打時不累積。最後一次增加後 BossStats.imbalanceDecayDelay 秒開始每秒減少 imbalanceDecayRate。
    /// 上限是目前 BossPhaseData.imbalanceMax，換階段時保留目前的值（不超過新上限）。滿了呼叫 BossController.Stun，打斷招式、進入頭暈。
    /// 不管頭暈是怎麼來的（失衡值滿、招式指定的固定頭暈），頭暈期間受傷 × BossStats.stunDamageMultiplier、可以被處決（ExecutionTarget）；
    /// 處決進行中讓 BossController.HoldStun，那一刀（DamageInfo.isExecution）打到就提早結束頭暈。離開頭暈時失衡值歸零。
    /// 執行期狀態存在元件自己身上；UI（T9 的失衡條）與演出只訂閱這裡的事件。
    /// 事件：OnStaggerChanged（開打與換階段時也會送）、OnStunned、OnExecuted、OnStunEnded。
    /// 處決把 Boss 打過轉階段門檻或打倒時，頭暈會先被打斷，所以 OnStunEnded(Executed) 會比 OnExecuted 早送出。
    /// </summary>
    [RequireComponent(typeof(BossController), typeof(BossHealth))]
    [DisallowMultipleComponent]
    public class BossStagger : MonoBehaviour
    {
        [Tooltip("失衡值衰減與頭暈受傷倍率")]
        [SerializeField] BossStats stats;
        [Tooltip("玩家：盾反打在這隻 Boss 身上時，加上 OnParry 送來的失衡值（PlayerStats.parryImbalance）")]
        [SerializeField] PlayerHealth player;

        /// <summary>(目前失衡值, 上限)。</summary>
        public event Action<float, float> OnStaggerChanged;
        /// <summary>進入頭暈；參數是這次頭暈的秒數（處決中會延長到那一刀打到）。</summary>
        public event Action<float> OnStunned;
        /// <summary>離開頭暈，失衡值已經歸零。</summary>
        public event Action<StunEndReason> OnStunEnded;
        /// <summary>處決打到；參數是實際扣掉的血量。</summary>
        public event Action<float> OnExecuted;

        public float Current { get; private set; }
        /// <summary>目前階段的上限。</summary>
        public float Max => boss.Phase != null ? boss.Phase.imbalanceMax : 0f;
        public bool IsStunned { get; private set; }
        /// <summary>再過幾秒開始衰減；衰減中、頭暈中或沒有失衡值時為 0。</summary>
        public float DecayDelayRemaining =>
            IsStunned || Current <= 0f ? 0f : Mathf.Max(0f, lastGainTime + stats.imbalanceDecayDelay - Time.time);
        public bool IsDecaying => !IsStunned && Current > 0f && DecayDelayRemaining <= 0f;

        BossController boss;
        BossHealth health;
        ExecutionTarget executionTarget;
        float lastGainTime = float.NegativeInfinity;

        void Awake()
        {
            boss = GetComponent<BossController>();
            health = GetComponent<BossHealth>();
            executionTarget = GetComponent<ExecutionTarget>();
            if (stats == null)
            {
                Debug.LogError("[Boss] BossStagger 沒有指定 BossStats，失衡值不會運作。", this);
                enabled = false;
                return;
            }
            if (executionTarget == null) Debug.LogWarning("[Boss] Boss 身上沒有 ExecutionTarget，頭暈時不能處決。", this);
            if (player == null) Debug.LogWarning("[Boss] BossStagger 沒有指定玩家的 PlayerHealth，盾反不會加失衡值。", this);
        }

        void OnEnable()
        {
            health.OnDamaged += HandleDamaged;
            boss.OnStateChanged += HandleStateChanged;
            boss.OnPhaseChanged += HandlePhaseChanged;
            if (player != null) player.OnParry += HandleParry;
            if (executionTarget != null)
            {
                executionTarget.OnExecutionStarted += HandleExecutionStarted;
                executionTarget.OnExecutionEnded += HandleExecutionEnded;
            }
        }

        void OnDisable()
        {
            health.OnDamaged -= HandleDamaged;
            boss.OnStateChanged -= HandleStateChanged;
            boss.OnPhaseChanged -= HandlePhaseChanged;
            if (player != null) player.OnParry -= HandleParry;
            if (executionTarget != null)
            {
                executionTarget.OnExecutionStarted -= HandleExecutionStarted;
                executionTarget.OnExecutionEnded -= HandleExecutionEnded;
            }
        }

        void Start()
        {
            // 讓先訂閱的 UI 拿到初始值
            OnStaggerChanged?.Invoke(Current, Max);
        }

        void Update()
        {
            if (IsStunned || Current <= 0f || Time.time - lastGainTime < stats.imbalanceDecayDelay) return;
            SetCurrent(Current - stats.imbalanceDecayRate * Time.deltaTime);
        }

        /// <summary>
        /// 累積失衡值，加滿就進入頭暈（打斷目前的招式）。頭暈、轉階段、倒下、還沒開打時無效。
        /// 玩家的攻擊與盾反會自動加；招式（例如龍捲風結束）用 BossMove.AddImbalance。
        /// </summary>
        public void AddImbalance(float amount)
        {
            if (amount <= 0f || !CanAccumulate) return;
            lastGainTime = Time.time;
            SetCurrent(Current + amount);
            if (Current >= Max) boss.Stun();
        }

        bool CanAccumulate
        {
            get
            {
                if (!boss.HasStarted) return false;
                switch (boss.State)
                {
                    case BossState.Stunned:
                    case BossState.Transition:
                    case BossState.Defeated:
                        return false;
                    default:
                        return true;
                }
            }
        }

        void SetCurrent(float value)
        {
            value = Mathf.Clamp(value, 0f, Max);
            if (value == Current) return;
            Current = value;
            OnStaggerChanged?.Invoke(Current, Max);
        }

        // ---------- 事件 ----------

        void HandleDamaged(BossDamageReport report)
        {
            if (report.result != DamageResult.Hit) return;
            if (report.info.isExecution)
            {
                OnExecuted?.Invoke(report.healthLost);
                if (!health.IsDefeated) boss.EndStun(); // 打倒時交給 OnDefeated 切到 Defeated
                return;
            }
            AddImbalance(report.info.imbalance);
        }

        // 只算這隻 Boss 的攻擊（招式的 Hitbox 與 Hazard 都以 Boss 根物件為攻擊方）
        void HandleParry(DamageInfo info, float imbalance)
        {
            if (info.source == gameObject) AddImbalance(imbalance);
        }

        void HandlePhaseChanged(BossPhaseData phase)
        {
            Current = Mathf.Min(Current, Max);
            OnStaggerChanged?.Invoke(Current, Max);
        }

        void HandleStateChanged(BossState next)
        {
            if (next == BossState.Stunned) BeginStun();
            else if (IsStunned) FinishStun(next);
        }

        void BeginStun()
        {
            IsStunned = true;
            health.DamageMultiplier = stats.stunDamageMultiplier;
            if (executionTarget != null) executionTarget.SetExecutable(true);
            OnStunned?.Invoke(boss.TimedStateDuration);
        }

        void FinishStun(BossState next)
        {
            StunEndReason reason = executionTarget != null && executionTarget.IsBeingExecuted ? StunEndReason.Executed
                                 : next == BossState.Idle ? StunEndReason.Timeout
                                 : StunEndReason.Interrupted;
            IsStunned = false;
            health.DamageMultiplier = 1f;
            if (executionTarget != null) executionTarget.SetExecutable(false);
            lastGainTime = float.NegativeInfinity;
            SetCurrent(0f);
            OnStunEnded?.Invoke(reason);
        }

        // 處決中頭暈時間到也先不結束，等那一刀打到（或玩家的處決動作結束）
        void HandleExecutionStarted()
        {
            if (IsStunned) boss.HoldStun = true;
        }

        void HandleExecutionEnded()
        {
            boss.HoldStun = false;
        }
    }
}
