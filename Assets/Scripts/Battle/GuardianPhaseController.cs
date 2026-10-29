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
    public enum GuardianStage { Dormant, Phase1, RootBarrage, AwaitingPhase2, Stopped, Phase2 }

    /// <summary>
    /// 大樹守衛的戰鬥流程（T28–T29）：開戰（只由登場演出結束或開發者工具呼叫 StartBattle）、一階段的 60 秒計時與 80% 血量鎖、
    /// 萬根齊發（頭暈中等頭暈結束再放、期間無敵）、活過之後交給 GuardianPhaseTwo 播轉階段、二階段計時。
    /// 本層指揮玩家與 Boss，Boss 不反向引用本層。
    /// </summary>
    [DefaultExecutionOrder(500)]
    public class GuardianPhaseController : MonoBehaviour, IPlayerBinding
    {
        [SerializeField] GuardianBattleData data;
        [SerializeField] BossController boss;
        [SerializeField] BossHealth health;
        [SerializeField] BossStagger stagger;
        [SerializeField] RootBarrageMove barrage;
        [SerializeField] InputLockArbiter session;
        [Tooltip("開戰時中止教學（開發者略過登場時教學可能還在跑）")]
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
        public BossHealth Health => health;
        public RootBarrageMove Barrage => barrage;
        public GuardianBattleData Data => data;
        /// <summary>已經要求萬根齊發、還沒放出（等頭暈或處決結束）。</summary>
        public bool RootBarrageRequested => requested && Stage == GuardianStage.Phase1;

        PlayerRig player;
        AreaStrike guidance;
        bool requested, finishedPending, warned;
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
            player = rig;
            if (boss == null) return;
            boss.TargetHurtbox = rig != null ? rig.HurtCollider : null;
            boss.TargetBodyRadius = rig != null ? rig.BodyRadius : 0f;
        }

        void OnEnable()
        {
            health.OnBossHealthChanged += HealthChanged;
            boss.OnMoveFinished += MoveFinished;
        }

        void OnDisable()
        {
            health.OnBossHealthChanged -= HealthChanged;
            boss.OnMoveFinished -= MoveFinished;
            ClearGuidance();
            session.SetInputLocked(this, false);
        }

        /// <summary>開戰：登場演出正常結束時由 PrologueCutsceneFlow 呼叫（開發者工具略過登場也走這裡）。不記教學完成、不寫存檔。</summary>
        public void StartBattle()
        {
            if (Stage != GuardianStage.Dormant || player == null || player.Health.IsDead) return;
            session.SetInputLocked(this, true);
            if (tutorial != null) tutorial.Abort();
            model.SetActive(true);
            hurtCollider.enabled = lockTarget.enabled = true;
            boss.SetBodyCollision(true);
            health.Invulnerable = false;
            var modifiers = difficulty != null && options != null ? difficulty.Get(options.Difficulty) : DifficultyModifiers.None;
            boss.SetDifficulty(modifiers);
            player.Controller.SetDifficulty(modifiers);
            player.Health.SetBoneGainEnabled(true);
            player.Health.SetBones(player.Health.BonesMax);
            Elapsed = 0f;
            SetStage(GuardianStage.Phase1);
            boss.StartBattle();
            OnBattleStarted?.Invoke();
            session.SetInputLocked(this, false);
        }

        /// <summary>要求放萬根齊發（60 秒、80% 血量或開發者）；頭暈、處決中等結束後才放。</summary>
        public void RequestRootBarrage()
        {
            if (Stage == GuardianStage.Dormant) StartBattle();
            if (Stage != GuardianStage.Phase1 || requested) return;
            requested = true;
            requestedFrame = Time.frameCount;
        }

        void HealthChanged(float current, float max)
        {
            if (Stage == GuardianStage.Phase1 && current <= max * data.healthFloorRatio) RequestRootBarrage();
        }

        void MoveFinished(BossMove move)
        {
            if (move != barrage || Stage != GuardianStage.RootBarrage) return;
            finishedPending = true;
            finishedFrame = Time.frameCount;
        }

        void Update()
        {
            if (Stage == GuardianStage.Phase2)
            {
                Elapsed += Time.deltaTime;
                return;
            }
            if (Stage != GuardianStage.Phase1 || Time.deltaTime <= 0f) return;
            Elapsed += Time.deltaTime;
            if (!warned && Elapsed >= data.guidanceAt) ShowGuidance();
            if (Elapsed >= data.phaseDuration) RequestRootBarrage();
        }

        // 第 56 秒：提示「即將萬根齊發，靠近守衛」與 9 m 內的安全指引
        void ShowGuidance()
        {
            warned = true;
            OnHintChanged?.Invoke(data.warningText);
            var area = new AttackArea
            {
                shape = AttackAreaShape.Circle, center = boss.transform.position, forward = Vector3.forward,
                fieldCenter = boss.ArenaCenter, fieldRadius = boss.Arena.radius, radius = data.guidanceRadius,
            };
            guidance = Instantiate(guidancePrefab);
            guidance.SafetyGuidance = true;
            guidance.Begin(area, new DamageInfo(0f, true, false, 0f), 0f, 0f, null, true);
            guidance.SetGuidanceProgress(1f);
        }

        // Hitbox 在 LateUpdate 結算。下一幀才消化門檻請求，讓該刀的失衡／處決先完整結算。
        void LateUpdate()
        {
            if (player == null || player.Health.IsDead || Time.deltaTime <= 0f) return;
            if (Stage == GuardianStage.Phase1 && requested && Time.frameCount > requestedFrame &&
                !stagger.IsStunned && !boss.HoldStun && !player.Controller.IsExecuting)
            {
                ClearGuidance();
                stagger.Frozen = true;
                health.Invulnerable = true;
                SetStage(GuardianStage.RootBarrage);
                OnHintChanged?.Invoke(string.Empty);
                boss.StartScriptedMove(barrage);
            }
            if (Stage == GuardianStage.RootBarrage && finishedPending && Time.frameCount > finishedFrame)
            {
                // 延到下一幀，確保最後一波和玩家倒下同幀時由倒下優先
                finishedPending = false;
                SetStage(GuardianStage.AwaitingPhase2);
                boss.SuspendCombat();
                health.Invulnerable = true;
                session.SetInputLocked(this, true);
                OnHintChanged?.Invoke(data.transitionText);
                OnRootBarrageCompleted?.Invoke();
            }
        }

        /// <summary>分出勝負（PrologueOutcome）：清場、停住戰鬥。</summary>
        public void StopBattle()
        {
            if (Stage == GuardianStage.Stopped) return;
            ClearGuidance();
            requested = finishedPending = false;
            boss.SuspendCombat();
            health.Invulnerable = true;
            stagger.Frozen = true;
            SetStage(GuardianStage.Stopped);
            OnHintChanged?.Invoke(string.Empty);
        }

        /// <summary>GuardianPhaseTwo／PrologueOutcome 先取得自己的鎖，再釋放本流程的交棒鎖。</summary>
        public void ReleaseTransitionLock() => session.SetInputLocked(this, false);

        /// <summary>二階段已套用（GuardianPhaseTwo.CompleteTransition）。</summary>
        public void EnterPhase2()
        {
            OnHintChanged?.Invoke(string.Empty);
            SetStage(GuardianStage.Phase2);
        }

        /// <summary>開發者直入二階段：進入跟活過萬根齊發一樣的交棒狀態，不改血量。</summary>
        public void PreparePhase2()
        {
            ClearGuidance();
            requested = finishedPending = false;
            SetStage(GuardianStage.AwaitingPhase2);
        }

        void SetStage(GuardianStage value)
        {
            Stage = value;
            OnStageChanged?.Invoke(value);
        }

        void ClearGuidance()
        {
            if (guidance != null) guidance.Despawn();
            guidance = null;
        }
    }
}
