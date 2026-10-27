using System;
using BossFight.Boss;
using BossFight.Cameras;
using BossFight.Core;
using BossFight.Hazards;
using BossFight.Player;
using BossFight.Progression;
using Unity.Cinemachine;
using UnityEngine;

namespace BossFight.Battle
{
    /// <summary>T29 簡易轉場；T30 可保留 CompleteTransition 入口並替換表現。</summary>
    [DefaultExecutionOrder(600)]
    public class GuardianPhaseTwo : MonoBehaviour, IPlayerBinding
    {
        [SerializeField] GuardianPhaseController guardian;
        [SerializeField] PrologueSession session;
        [SerializeField] DialogueSession dialogue;
        [SerializeField] CameraController cameraController;
        [SerializeField] CinemachineCamera shot;
        [SerializeField] AudioManager audioManager;
        [SerializeField] TimedFx wavePrefab;
        [SerializeField] Transform guardianFace;
        PlayerRig player;
        BossController boss;
        BossHealth health;
        BossStagger stagger;
        FeedbackEmitter emitter;
        GuardianFullPowerVisual fullPowerVisual;
        enum Step { None, Lead, Dialogue, Push, Finish, External }
        /// <summary>T30 可在接管前設 true，改由正式 Timeline 回呼 CompleteTransition。</summary>
        public bool ExternalTransitionPresentation { get; set; }
        public event Action OnTransitionPresentationRequested;
        Step step;
        float age;
        int dialogueId, finishFrame;
        bool dialogueDone;
        Vector3 pushFrom, pushTo;
        GuardianBattleData Data => guardian.Data;
        public void BindPlayer(PlayerRig rig) => player = rig;
        void Awake()
        {
            boss = guardian.Boss; health = boss.GetComponent<BossHealth>(); stagger = boss.GetComponent<BossStagger>();
            emitter = boss.GetComponent<FeedbackEmitter>(); fullPowerVisual = boss.GetComponent<GuardianFullPowerVisual>();
            boss.AutoCompleteTransition = false;
        }
        void OnEnable()
        {
            guardian.OnRootBarrageCompleted += BeginTransition;
            guardian.OnStageChanged += Stage;
            boss.OnMoveChosen += Chosen;
            dialogue.OnViewChanged += DialogueView;
            dialogue.OnFinished += DialogueFinished;
        }
        void OnDisable()
        {
            guardian.OnRootBarrageCompleted -= BeginTransition; guardian.OnStageChanged -= Stage;
            boss.OnMoveChosen -= Chosen; dialogue.OnViewChanged -= DialogueView; dialogue.OnFinished -= DialogueFinished;
            CancelTransition();
        }
        void Chosen(BossMoveChoice choice)
        {
            if (guardian.Stage == GuardianStage.Phase2 && choice.move.Data.phase == boss.Phase.movePool)
                health.ApplySelfCost(Data.selfCostRatio);
        }
        public void BeginTransition()
        {
            if (step != Step.None || player == null || player.Health.IsDead || guardian.Stage != GuardianStage.AwaitingPhase2) return;
            session.SetInputLocked(this, true); guardian.ReleaseTransitionLock();
            boss.ClearAllHazards(); health.Invulnerable = true; stagger.Frozen = true;
            if (!boss.RequestPhaseTransition(1)) { session.SetInputLocked(this, false); return; }
            player.Controller.ClearLockTarget();
            Vector3 p = boss.transform.position + Vector3.back * Data.transitionStageRadius;
            p.y = player.transform.position.y;
            player.Controller.Teleport(p, Quaternion.LookRotation(Vector3.forward));
            shot.Priority = ExternalTransitionPresentation ? 0 : 30;
            step = ExternalTransitionPresentation ? Step.External : Step.Lead; age = 0f;
            if (ExternalTransitionPresentation) OnTransitionPresentationRequested?.Invoke();
        }
        /// <summary>開發者直入仍走相同轉場，不能繞過鎖與血量下限收尾。</summary>
        public void DebugEnterPhase2()
        {
            if (guardian.Stage == GuardianStage.Dormant) guardian.StartBattle();
            if (guardian.Stage != GuardianStage.Phase1 || player == null || player.Health.IsDead) return;
            session.SetInputLocked(this, true); boss.SuspendCombat();
            guardian.PreparePhase2(); BeginTransition();
        }
        void DialogueView(DialogueView view) { if (step == Step.Dialogue && view.visible) dialogueId = view.id; }
        void DialogueFinished(int id, bool completed) { if (step == Step.Dialogue && id == dialogueId && completed) dialogueDone = true; }
        void Stage(GuardianStage value) { if (value == GuardianStage.Stopped) CancelTransition(); }
        void Update()
        {
            if (step == Step.None || step == Step.External || player == null || player.Health.IsDead || Time.deltaTime <= 0f) return;
            age += Time.deltaTime;
            if (step == Step.Lead && age >= Data.transitionLeadTime)
            {
                step = Step.Dialogue; age = 0f; dialogueDone = false;
                dialogue.Begin(Data.guardianName, new[] { Data.phase2Line }, false);
            }
            else if (step == Step.Dialogue && (dialogueDone || age >= Data.transitionDialogueTime))
            {
                dialogue.Hide(); step = Step.Push; age = 0f;
                pushFrom = player.transform.position;
                pushTo = boss.transform.position + Vector3.back * Data.transitionPushRadius; pushTo.y = pushFrom.y;
                TimedFx.Spawn(wavePrefab, boss.transform.position, Quaternion.identity, Color.white, Data.transitionPushRadius);
                if (fullPowerVisual != null) fullPowerVisual.SetFullPower();
                if (emitter != null && emitter.Data != null) emitter.Play(emitter.Data.shockwave, boss.transform.position);
            }
            else if (step == Step.Push)
            {
                float t = Data.transitionPushTime > 0f ? Mathf.Clamp01(age / Data.transitionPushTime) : 1f;
                player.Controller.ScriptedMoveTo(Vector3.Lerp(pushFrom, pushTo, Mathf.SmoothStep(0f, 1f, t)), boss.transform.position - pushTo);
                if (t >= 1f) { step = Step.Finish; finishFrame = Time.frameCount; }
            }
        }
        void LateUpdate()
        {
            if (step == Step.Finish && Time.frameCount > finishFrame && Time.deltaTime > 0f) CompleteTransition();
        }
        public void CompleteTransition()
        {
            if (step == Step.None || player == null || player.Health.IsDead || !gameObject.scene.isLoaded || boss.State != BossState.Transition) return;
            health.HealthFloor = 0f; stagger.ResetImbalance(); stagger.Frozen = false;
            boss.CompleteTransition(); guardian.EnterPhase2();
            if (audioManager != null) audioManager.SetMusicSpeed(boss.Phase.musicSpeed);
            player.Controller.ClearLockTarget(); cameraController.ResetBehind(guardianFace.position);
            shot.Priority = 0; step = Step.None; session.SetInputLocked(this, false);
        }
        public void CancelTransition()
        {
            if (step != Step.None && dialogue != null) dialogue.Hide();
            step = Step.None; if (shot != null) shot.Priority = 0;
            if (session != null) session.SetInputLocked(this, false);
        }
    }
}
