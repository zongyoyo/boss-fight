using System;
using BossFight.Boss;
using BossFight.Boss.Moves;
using BossFight.Core;
using BossFight.Hazards;
using BossFight.Player;
using BossFight.Progression;
using UnityEngine;
namespace BossFight.Battle
{
    public enum GuardianStage { Dormant, Phase1, RootBarrage, AwaitingPhase2, Stopped }
    /// <summary>T28 一階段流程；本層指揮玩家與 Boss，Boss 不反向引用本層。</summary>
    [DefaultExecutionOrder(500)]
    public class GuardianPhaseController : MonoBehaviour, IPlayerBinding
    {
        [SerializeField] GuardianBattleData data;
        [SerializeField] BossController boss;
        [SerializeField] BossHealth health;
        [SerializeField] BossStagger stagger;
        [SerializeField] RootBarrageMove barrage;
        [SerializeField] BossHitFeedback hitFeedback;
        [SerializeField] PrologueSession session;
        [SerializeField] TutorialDirector tutorial;
        [SerializeField] AreaStrike guidancePrefab;
        [SerializeField] GameOptions options;
        [SerializeField] DifficultyData difficulty;
        [SerializeField] Collider hurtCollider;
        [SerializeField] LockOnTarget lockTarget;
        [SerializeField] GameObject model;
        public event Action OnBattleStarted;
        public event Action<GuardianStage> OnStageChanged;
        public event Action<string> OnHintChanged;
        public event Action OnRootBarrageCompleted;
        public GuardianStage Stage { get; private set; }
        public float Elapsed { get; private set; }
        public BossController Boss => boss;
        public GuardianBattleData Data => data;
        PlayerRig player;
        AreaStrike guidance;
        bool subscribed, requested, finishedPending, warned, rollFeedback;
        int requestedFrame = -1;
        int finishedFrame = -1;
        void Awake()
        {
            boss.AutomaticPhaseTransitions = false;
            health.Invulnerable = true;
            health.HealthFloor = health.Max * data.healthFloorRatio;
            hurtCollider.enabled = lockTarget.enabled = false;
            model.SetActive(false);
            boss.SetBodyCollision(false);
        }
        public void BindPlayer(PlayerRig rig)
        {
            if (subscribed) Unsubscribe();
            player = rig;
            if (boss != null) boss.TargetHurtbox = rig != null ? rig.GetComponentInChildren<Hurtbox>().GetComponent<Collider>() : null;
            if (boss != null) boss.TargetBodyRadius = rig != null ? rig.GetComponent<CharacterController>().radius : 0f;
            if (subscribed) Subscribe();
        }
        void Subscribe()
        {
            if (player == null) return;
            player.Controller.OnRollStarted += RollStarted; player.Controller.OnRollEnded += RollEnded;
        }
        void Unsubscribe()
        {
            if (player == null) return;
            player.Controller.OnRollStarted -= RollStarted; player.Controller.OnRollEnded -= RollEnded;
        }
        void OnEnable()
        {
            subscribed = true; Subscribe();
            boss.TargetDodgeEligible = CanReportDodge;
            health.OnBossHealthChanged += HealthChanged;
            boss.OnMoveFinished += MoveFinished; boss.OnMoveHit += MoveHit; boss.OnAttackAvoided += Avoided;
            if (tutorial != null) tutorial.OnTutorialCompleted += TutorialCompleted;
        }
        void OnDisable()
        {
            Unsubscribe(); subscribed = false;
            boss.TargetDodgeEligible = null;
            health.OnBossHealthChanged -= HealthChanged;
            boss.OnMoveFinished -= MoveFinished; boss.OnMoveHit -= MoveHit; boss.OnAttackAvoided -= Avoided;
            if (tutorial != null) tutorial.OnTutorialCompleted -= TutorialCompleted;
            ClearGuidance(); session.SetInputLocked(this, false);
        }
        /// <summary>T30 登場結束／開發按鍵共用入口；不記教學完成或進度存檔。</summary>
        void TutorialCompleted()
        {
            if (data.startAfterTutorial) StartBattle();
        }
        public void StartBattle()
        {
            if (Stage != GuardianStage.Dormant || player == null || player.Health.IsDead) return;
            session.SetInputLocked(this, true);
            if (tutorial != null) tutorial.Abort();
            model.SetActive(true); hurtCollider.enabled = lockTarget.enabled = true;
            boss.SetBodyCollision(true);
            health.Invulnerable = false;
            var modifiers = difficulty != null && options != null ? difficulty.Get(options.Difficulty) : DifficultyModifiers.None;
            boss.SetDifficulty(modifiers); player.Controller.SetDifficulty(modifiers);
            player.Health.SetBoneGainEnabled(true); player.Health.SetBones(player.Health.BonesMax);
            Elapsed = 0f; SetStage(GuardianStage.Phase1); boss.StartBattle();
            OnBattleStarted?.Invoke(); session.SetInputLocked(this, false);
        }
        public void RequestRootBarrage()
        {
            if (Stage == GuardianStage.Dormant) StartBattle();
            if (Stage != GuardianStage.Phase1 || requested) return;
            requested = true; requestedFrame = Time.frameCount;
        }
        void HealthChanged(float current, float max)
        { if (Stage == GuardianStage.Phase1 && current <= max * data.healthFloorRatio) RequestRootBarrage(); }
        void MoveFinished(BossMove move)
        {
            if (move != barrage || Stage != GuardianStage.RootBarrage) return;
            finishedPending = true; finishedFrame = Time.frameCount;
        }
        void Update()
        {
            if (Stage != GuardianStage.Phase1 || Time.deltaTime <= 0f) return;
            Elapsed += Time.deltaTime;
            if (!warned && Elapsed >= data.guidanceAt)
            {
                warned = true; OnHintChanged?.Invoke(data.warningText);
                var a = new AttackArea { shape = AttackAreaShape.Circle, center = boss.transform.position, forward = Vector3.forward,
                    fieldCenter = boss.ArenaCenter, fieldRadius = boss.Arena.radius, radius = data.guidanceRadius };
                guidance = Instantiate(guidancePrefab); guidance.SafetyGuidance = true;
                guidance.Begin(a, new DamageInfo(0f, true, false, 0f), 0f, 0f, null, null, true);
                guidance.SetGuidanceProgress(1f);
            }
            if (Elapsed >= data.phaseDuration) RequestRootBarrage();
        }
        // Hitbox 在 LateUpdate 結算。下一幀才消化門檻請求，讓該刀的失衡／處決先完整結算。
        void LateUpdate()
        {
            if (player == null || player.Health.IsDead || Time.deltaTime <= 0f) return;
            if (Stage == GuardianStage.Phase1 && requested && Time.frameCount > requestedFrame &&
                !stagger.IsStunned && !boss.HoldStun && !player.Controller.IsExecuting)
            {
                ClearGuidance();
                stagger.Frozen = true; health.Invulnerable = true;
                SetStage(GuardianStage.RootBarrage); OnHintChanged?.Invoke(string.Empty);
                boss.StartScriptedMove(barrage);
            }
            if (Stage == GuardianStage.RootBarrage && finishedPending && Time.frameCount > finishedFrame)
            {
                finishedPending = false;
                // 延到下一幀，確保最後一波和玩家倒下同幀時由倒下優先。
                SetStage(GuardianStage.AwaitingPhase2); boss.SuspendCombat(); health.Invulnerable = true;
                session.SetInputLocked(this, true);
                OnHintChanged?.Invoke(data.transitionText); OnRootBarrageCompleted?.Invoke();
                Debug.Log("[Guardian] 活過萬根齊發：交接二階段（T29）。", this);
            }
        }
        public void StopBattle()
        {
            if (Stage == GuardianStage.Stopped) return;
            ClearGuidance(); requested = finishedPending = false;
            boss.SuspendCombat(); health.Invulnerable = true; stagger.Frozen = true;
            SetStage(GuardianStage.Stopped); OnHintChanged?.Invoke(string.Empty);
        }
        /// <summary>T29／T30 先取得自己的 owner 鎖，再釋放本流程的交棒鎖。</summary>
        public void ReleaseTransitionLock() => session.SetInputLocked(this, false);
        void SetStage(GuardianStage value) { Stage = value; OnStageChanged?.Invoke(value); }
        void ClearGuidance() { if (guidance != null) guidance.Despawn(); guidance = null; }
        void RollStarted(Vector3 _) { rollFeedback = false; boss.TargetRolling = true; }
        void RollEnded() => boss.TargetRolling = false;
        bool CanReportDodge() => player != null && player.Controller.IsRolling && !player.Health.ForcedInvulnerable && !player.Health.IsHitInvulnerable;
        void MoveHit(BossMove move, HitReport report)
        {
            if (report.result == DamageResult.Evaded && player != null && player.Controller.IsRolling && player.Controller.IsInvulnerable && !player.Health.IsHitInvulnerable)
                Avoided(player.transform.position);
        }
        void Avoided(Vector3 point)
        {
            if (player == null || player.Health.ForcedInvulnerable || player.Health.IsDead || rollFeedback) return;
            rollFeedback = true; hitFeedback.PlaySaplingEvade(point); player.Feedback.PlayEvadeFeedback();
        }
    }
}
