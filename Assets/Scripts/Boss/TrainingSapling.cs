using System;
using BossFight.Core;
using UnityEngine;

namespace BossFight.Boss
{
    // 新模式加在最後，保留既有 SO 的待機／白色攻擊／標靶編號。
    public enum SaplingMode { Idle, Attack, Target, DodgeAttack }
    public enum SaplingDefeatReason { Damage, Execution }
    public enum SaplingDespawnReason { Lifetime, Cleared, Disabled }

    /// <summary>這次預兆的完整資料；表現從事件取色，和實際判定使用同一份可格擋設定。</summary>
    public readonly struct SaplingTelegraph
    {
        public readonly int id;
        public readonly float duration, reach, width;
        public readonly bool canBlock, canParry;
        public SaplingTelegraph(int id, TrainingSaplingData data, SaplingMode mode)
        {
            this.id = id; duration = data.telegraphTime; reach = data.reach; width = data.width;
            canBlock = mode != SaplingMode.DodgeAttack && data.canBlock;
            canParry = mode != SaplingMode.DodgeAttack && data.canParry;
        }
    }

    /// <summary>UI 的事件快照；不必每幀讀取樹精欄位。</summary>
    public readonly struct SaplingView
    {
        public readonly float health, maxHealth, stagger, maxStagger;
        public readonly SaplingMode mode;
        public readonly bool visible, stunned;
        public SaplingView(TrainingSapling s)
        {
            health = s.Health; maxHealth = s.Data.maxHealth;
            stagger = s.Stagger; maxStagger = s.Data.staggerMax;
            mode = s.Mode; visible = s.IsAlive; stunned = s.IsStunned;
        }
    }

    public readonly struct SaplingDamageReport
    {
        public readonly DamageInfo info;
        public readonly DamageResult result;
        public readonly float healthLost;
        public SaplingDamageReport(in DamageInfo info, DamageResult result, float healthLost)
        { this.info = info; this.result = result; this.healthLost = healthLost; }
    }

    /// <summary>同一樹精的 id 單調遞增；取消的攻擊與正常落空分開，不能拿來計閃避成功。</summary>
    public readonly struct SaplingAttackReport
    {
        public readonly TrainingSapling source;
        public readonly int id;
        public readonly bool hadWindow, hasHit, cancelled;
        /// <summary>在威脅範圍內翻滾、與判定窗重疊且沒被打到；走遠處空滾不算。</summary>
        public readonly bool rollEvaded;
        public readonly Vector3 targetPosition;
        public readonly HitReport hit;
        public SaplingAttackReport(TrainingSapling source, int id, bool hadWindow, bool hasHit, in HitReport hit, bool cancelled,
                                   bool rollEvaded, Vector3 targetPosition)
        {
            this.source = source; this.id = id; this.hadWindow = hadWindow; this.hasHit = hasHit; this.hit = hit; this.cancelled = cancelled;
            this.rollEvaded = rollEvaded; this.targetPosition = targetPosition;
        }
    }

    /// <summary>可重用的小樹精；只認 Core 的目標與傷害，不引用玩家或教學流程。</summary>
    [RequireComponent(typeof(ExecutionTarget), typeof(LockOnTarget))]
    [DisallowMultipleComponent]
    public class TrainingSapling : MonoBehaviour, IDamageable
    {
        [SerializeField] TrainingSaplingData data;
        [SerializeField] Hitbox attackHitbox;
        [SerializeField] Collider hurtCollider;
        [Tooltip("T26 可直接驗收；T27 流程接管時關閉，再呼叫 Appear")]
        [SerializeField] bool appearOnStart = true;

        public event Action<int> OnAttackTelegraphed;
        public event Action<SaplingTelegraph> OnAttackWarning;
        public event Action<int> OnAttackWindowOpened;
        public event Action<int, HitReport> OnAttackHit;
        public event Action<SaplingAttackReport> OnAttackResolved;
        public event Action<SaplingDamageReport> OnDamaged;
        public event Action<float, float> OnHealthChanged;
        public event Action<float, float> OnStaggerChanged;
        public event Action OnStunned;
        public event Action<SaplingMode> OnModeChanged;
        public event Action<SaplingDamageReport> OnExecuted;
        public event Action<SaplingDefeatReason> OnDefeated;
        public event Action<SaplingDespawnReason> OnDespawned;
        public event Action OnAppeared;
        public event Action<SaplingView> OnViewChanged;

        public TrainingSaplingData Data => data;
        public float Health { get; private set; }
        public float Stagger { get; private set; }
        public SaplingMode Mode { get; private set; }
        public bool IsAlive { get; private set; }
        public bool IsStunned { get; private set; }
        public bool Invulnerable { get; private set; }
        public bool AccumulateStagger { get; private set; }

        enum AttackPhase { Waiting, Telegraph, Active }
        AttackPhase phase;
        ExecutionTarget executionTarget;
        LockOnTarget lockTarget;
        Transform target;
        float age, cycleTime, phaseTime, lastGain, executionHealthBeforeHit;
        int attackId, openedFrame;
        bool attackPending, hadWindow, hasHit, executionHit, shuttingDown;
        HitReport lastHit;
        SaplingTelegraph currentWarning;
        bool targetRolling, rollOverlapped, rollThreatened;

        void Awake()
        {
            executionTarget = GetComponent<ExecutionTarget>();
            lockTarget = GetComponent<LockOnTarget>();
            attackHitbox.Filter = OnlyTarget;
            hurtCollider.enabled = false;
            lockTarget.enabled = false;
        }

        void OnEnable()
        {
            shuttingDown = false;
            attackHitbox.OnHit += HandleAttackHit;
            executionTarget.OnExecutionFinished += HandleExecutionEnded;
        }

        void OnDisable()
        {
            shuttingDown = true;
            if (IsAlive) Despawn(SaplingDespawnReason.Disabled);
            attackHitbox.OnHit -= HandleAttackHit;
            executionTarget.OnExecutionFinished -= HandleExecutionEnded;
        }

        void Start() { if (appearOnStart && !IsAlive) Appear(); }

        /// <summary>只記引用；可在上層 IPlayerBinding 第一次（早於本元件 Awake）呼叫。</summary>
        public void SetTarget(Transform value) => target = value;

        /// <summary>上層由玩家翻滾事件傳入；不引用 Player，也不影響 Hitbox 或無敵結算。</summary>
        public void SetTargetRolling(bool value)
        {
            targetRolling = value;
            if (!attackPending || !value) return;
            rollThreatened |= TargetInAttackRange();
            if (phase == AttackPhase.Active) rollOverlapped = true;
        }

        /// <summary>T29 生成後設定戰鬥資料，再呼叫 Appear；不修改任何 SO。</summary>
        public void Configure(TrainingSaplingData value, Transform targetTransform)
        {
            if (IsAlive) Despawn(SaplingDespawnReason.Cleared);
            data = value;
            SetTarget(targetTransform);
        }

        public void Appear()
        {
            if (data == null || IsAlive || shuttingDown) return;
            executionTarget.EndExecution();
            executionTarget.SetExecutable(false);
            Health = data.maxHealth;
            Stagger = age = phaseTime = 0f;
            cycleTime = data.attackPeriod;
            lastGain = float.NegativeInfinity;
            IsAlive = true;
            IsStunned = executionHit = false;
            Invulnerable = data.initiallyInvulnerable;
            AccumulateStagger = data.initiallyAccumulateStagger;
            Mode = data.initialMode;
            phase = AttackPhase.Waiting;
            hurtCollider.enabled = lockTarget.enabled = true;
            OnAppeared?.Invoke();
            OnModeChanged?.Invoke(Mode);
            PublishState();
        }

        public void SetMode(SaplingMode value)
        {
            if (Mode == value) return;
            ResolveAttack(true);
            Mode = value;
            cycleTime = data.attackPeriod;
            OnModeChanged?.Invoke(Mode);
            PublishState();
        }

        public void SetInvulnerable(bool value) => Invulnerable = value;
        public void SetStaggerAccumulation(bool value) => AccumulateStagger = value;

        public void PublishState()
        {
            OnHealthChanged?.Invoke(Health, data.maxHealth);
            OnStaggerChanged?.Invoke(Stagger, data.staggerMax);
            OnViewChanged?.Invoke(new SaplingView(this));
        }

        void Update()
        {
            if (!IsAlive || Time.deltaTime <= 0f) return;
            float dt = Time.deltaTime;
            age += dt;
            if (data.lifetime > 0f && age >= data.lifetime) { Despawn(SaplingDespawnReason.Lifetime); return; }
            if (IsStunned || executionHit) return;
            if (Stagger > 0f && Time.time - lastGain >= data.staggerDecayDelay)
            {
                Stagger = Mathf.Max(0f, Stagger - data.staggerDecayRate * dt);
                PublishState();
            }
            if ((Mode != SaplingMode.Attack && Mode != SaplingMode.DodgeAttack) || target == null) return;
            if (phase != AttackPhase.Active) FaceTarget(dt);
            cycleTime += dt;
            phaseTime += dt;
            switch (phase)
            {
                case AttackPhase.Waiting:
                    if (cycleTime >= data.attackPeriod) BeginTelegraph();
                    break;
                case AttackPhase.Telegraph:
                    if (phaseTime >= data.telegraphTime) OpenWindow();
                    break;
                case AttackPhase.Active:
                    if (targetRolling) { rollOverlapped = true; rollThreatened |= TargetInAttackRange(); }
                    // 至少讓 Hitbox 的 LateUpdate 掃過一次，低幀率也不能漏掉整個判定窗。
                    if (phaseTime >= data.activeTime && Time.frameCount > openedFrame) ResolveAttack(false);
                    break;
            }
        }

        void FaceTarget(float dt)
        {
            Vector3 to = target.position - transform.position; to.y = 0f;
            if (to.sqrMagnitude > 1e-6f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(to), data.turnSpeed * dt);
        }

        bool OnlyTarget(Collider collider) => target != null && collider.transform.IsChildOf(target);

        bool TargetInAttackRange()
        {
            if (target == null) return false;
            Vector3 local = transform.InverseTransformPoint(target.position);
            return Mathf.Abs(local.x) <= data.width * 0.5f && local.z >= 0f && local.z <= data.reach;
        }

        void BeginTelegraph()
        {
            phase = AttackPhase.Telegraph;
            phaseTime = cycleTime = 0f;
            attackId++;
            attackPending = true;
            hadWindow = hasHit = false;
            rollOverlapped = false;
            rollThreatened = targetRolling && TargetInAttackRange();
            lastHit = default;
            currentWarning = new SaplingTelegraph(attackId, data, Mode);
            OnAttackWarning?.Invoke(currentWarning);
            OnAttackTelegraphed?.Invoke(attackId);
        }

        void OpenWindow()
        {
            phase = AttackPhase.Active;
            phaseTime = 0f;
            openedFrame = Time.frameCount;
            hadWindow = true;
            rollOverlapped = targetRolling;
            rollThreatened |= targetRolling && TargetInAttackRange();
            attackHitbox.SetBox(new Vector3(0f, data.height * 0.5f, data.reach * 0.5f), new Vector3(data.width, data.height, data.reach));
            attackHitbox.Activate(new DamageInfo(data.damage, currentWarning.canBlock, currentWarning.canParry, 0f));
            OnAttackWindowOpened?.Invoke(attackId);
        }

        void HandleAttackHit(HitReport report)
        {
            hasHit = true;
            lastHit = report;
            OnAttackHit?.Invoke(attackId, report);
            if (report.result == DamageResult.Parried) AddStagger(report.counterImbalance);
        }

        void ResolveAttack(bool cancelled)
        {
            attackHitbox.Deactivate();
            phase = AttackPhase.Waiting;
            if (!attackPending) return;
            attackPending = false;
            bool evaded = !cancelled && hadWindow && rollOverlapped && rollThreatened && (!hasHit || lastHit.result == DamageResult.Evaded);
            OnAttackResolved?.Invoke(new SaplingAttackReport(this, attackId, hadWindow, hasHit, lastHit, cancelled, evaded,
                                                          target != null ? target.position : Vector3.zero));
        }

        public DamageOutcome TakeDamage(in DamageInfo info)
        {
            if (!IsAlive || executionHit) return DamageResult.Ignored;
            if (Invulnerable)
            {
                OnDamaged?.Invoke(new SaplingDamageReport(info, DamageResult.Evaded, 0f));
                return DamageResult.Evaded;
            }
            bool executed = info.isExecution && IsStunned && executionTarget.IsBeingExecuted;
            float previous = Health;
            if (executed) executionHealthBeforeHit = previous;
            Health = executed ? 0f : Mathf.Max(Mode == SaplingMode.Target ? data.targetHealthFloor : 0f, Health - info.damage);
            var report = new SaplingDamageReport(info, DamageResult.Hit, previous - Health);
            executionHit = executed;
            PublishState();
            OnDamaged?.Invoke(report);
            if (executed)
            {
                // 不提前關掉根物件或 EndExecution；等玩家的整段處決動作完成才落葉。
                ResolveAttack(true);
                executionTarget.SetExecutable(false);
                hurtCollider.enabled = lockTarget.enabled = false;
                OnExecuted?.Invoke(report);
            }
            else if (Health <= 0f) Defeat(SaplingDefeatReason.Damage);
            else AddStagger(info.imbalance);
            return DamageResult.Hit;
        }

        void AddStagger(float amount)
        {
            if (!AccumulateStagger || !IsAlive || IsStunned || amount <= 0f) return;
            lastGain = Time.time;
            Stagger = Mathf.Min(data.staggerMax, Stagger + amount);
            if (Stagger >= data.staggerMax) ForceStun();
            else PublishState();
        }

        /// <summary>巴掌強制打滿；即使教學關閉累積也有效，沒有自動結束計時。</summary>
        public void ForceStun()
        {
            if (!IsAlive || IsStunned || executionHit) return;
            ResolveAttack(true);
            Stagger = data.staggerMax;
            IsStunned = true;
            Invulnerable = false;
            executionTarget.SetExecutable(true);
            PublishState();
            OnStunned?.Invoke();
        }

        void HandleExecutionEnded(bool completed)
        {
            if (shuttingDown || !gameObject.activeInHierarchy || !gameObject.scene.isLoaded || !IsAlive || !executionHit) return;
            if (completed) Defeat(SaplingDefeatReason.Execution);
            else
            {
                // 命中後仍可能因玩家停用或倒下而取消；保留教學靶，不假報處決完成。
                executionHit = false;
                Health = executionHealthBeforeHit;
                hurtCollider.enabled = lockTarget.enabled = true;
                executionTarget.SetExecutable(true);
                PublishState();
            }
        }

        void Defeat(SaplingDefeatReason reason)
        {
            if (!IsAlive) return;
            IsAlive = false;
            ResolveAttack(true);
            hurtCollider.enabled = lockTarget.enabled = false;
            executionTarget.SetExecutable(false);
            PublishState();
            OnDefeated?.Invoke(reason);
        }

        public void Despawn(SaplingDespawnReason reason = SaplingDespawnReason.Cleared)
        {
            if (!IsAlive) return;
            IsAlive = executionHit = false;
            ResolveAttack(true);
            hurtCollider.enabled = lockTarget.enabled = false;
            executionTarget.SetExecutable(false);
            executionTarget.EndExecution();
            PublishState();
            OnDespawned?.Invoke(reason);
        }
    }
}
