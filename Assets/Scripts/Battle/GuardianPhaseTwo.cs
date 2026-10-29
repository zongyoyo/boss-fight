using System;
using BossFight.Boss;
using BossFight.Cameras;
using BossFight.Core;
using BossFight.Player;
using BossFight.Progression;
using UnityEngine;

namespace BossFight.Battle
{
    /// <summary>
    /// 大樹守衛二階段的戰鬥側（T29）：活過萬根齊發後鎖輸入、清場、守衛無敵、把玩家擺到守衛南側，送 OnTransitionPresentationRequested；
    /// 演出（PrologueCutsceneFlow 播轉階段 Timeline）正常結束時呼叫 CompleteTransition 套用二階段（軀幹條歸零、解除血量鎖、音樂、全力外觀）。
    /// 二階段每次出招扣當前血量的 selfCostRatio。
    /// </summary>
    [DefaultExecutionOrder(600)]
    public class GuardianPhaseTwo : MonoBehaviour, IPlayerBinding
    {
        [SerializeField] GuardianPhaseController guardian;
        [SerializeField] InputLockArbiter session;
        [SerializeField] CameraController cameraController;
        [SerializeField] AudioManager audioManager;
        [Tooltip("轉階段結束時自由視角轉向的點（樹臉）")]
        [SerializeField] Transform guardianFace;

        /// <summary>轉階段的戰鬥側準備好了，請上層播演出；演出結束呼叫 CompleteTransition，中止呼叫 CancelTransition。</summary>
        public event Action OnTransitionPresentationRequested;
        /// <summary>二階段自損一次：扣掉的血量（給 Debug 面板）。</summary>
        public event Action<float> OnSelfCost;

        PlayerRig player;
        BossController boss;
        BossHealth health;
        BossStagger stagger;
        GuardianFullPowerVisual fullPowerVisual;
        bool transitioning;

        GuardianBattleData Data => guardian.Data;
        public bool IsTransitioning => transitioning;

        public void BindPlayer(PlayerRig rig) => player = rig;

        void Awake()
        {
            boss = guardian.Boss;
            health = boss.GetComponent<BossHealth>();
            stagger = boss.GetComponent<BossStagger>();
            fullPowerVisual = boss.GetComponent<GuardianFullPowerVisual>();
            boss.AutoCompleteTransition = false;
        }

        void OnEnable()
        {
            guardian.OnRootBarrageCompleted += BeginTransition;
            guardian.OnStageChanged += HandleStage;
            boss.OnMoveChosen += HandleMoveChosen;
        }

        void OnDisable()
        {
            guardian.OnRootBarrageCompleted -= BeginTransition;
            guardian.OnStageChanged -= HandleStage;
            boss.OnMoveChosen -= HandleMoveChosen;
            CancelTransition();
        }

        void HandleMoveChosen(BossMoveChoice choice)
        {
            if (guardian.Stage != GuardianStage.Phase2 || choice.move.Data.phase != boss.Phase.movePool) return;
            float before = health.Current;
            health.ApplySelfCost(Data.selfCostRatio);
            OnSelfCost?.Invoke(before - health.Current);
        }

        void HandleStage(GuardianStage value)
        {
            if (value == GuardianStage.Stopped) CancelTransition();
        }

        public void BeginTransition()
        {
            if (transitioning || player == null || player.Health.IsDead || guardian.Stage != GuardianStage.AwaitingPhase2) return;
            session.SetInputLocked(this, true);
            guardian.ReleaseTransitionLock();
            boss.ClearAllHazards();
            health.Invulnerable = true;
            stagger.Frozen = true;
            if (!boss.RequestPhaseTransition(1))
            {
                session.SetInputLocked(this, false);
                return;
            }
            player.Controller.ClearLockTarget();
            Vector3 p = boss.transform.position + Vector3.back * Data.transitionStageRadius;
            p.y = player.transform.position.y;
            player.Controller.Teleport(p, Quaternion.LookRotation(Vector3.forward));
            transitioning = true;
            OnTransitionPresentationRequested?.Invoke();
        }

        /// <summary>開發者直入二階段（一階段或萬根齊發中）：走跟活過萬根齊發一樣的轉階段，不能繞過鎖與血量下限收尾。</summary>
        public bool DebugEnterPhase2()
        {
            if (guardian.Stage == GuardianStage.Dormant) guardian.StartBattle();
            if ((guardian.Stage != GuardianStage.Phase1 && guardian.Stage != GuardianStage.RootBarrage) ||
                player == null || player.Health.IsDead || stagger.IsStunned) return false;
            session.SetInputLocked(this, true);
            boss.SuspendCombat();
            guardian.PreparePhase2();
            BeginTransition();
            session.SetInputLocked(this, transitioning);
            return transitioning;
        }

        /// <summary>轉階段演出正常結束時呼叫：套用二階段。</summary>
        public void CompleteTransition()
        {
            if (!transitioning || player == null || player.Health.IsDead || !gameObject.scene.isLoaded || boss.State != BossState.Transition) return;
            health.HealthFloor = 0f;
            stagger.ResetImbalance();
            stagger.Frozen = false;
            boss.CompleteTransition();
            guardian.EnterPhase2();
            if (fullPowerVisual != null) fullPowerVisual.SetFullPower();
            if (audioManager != null) audioManager.SetMusicSpeed(boss.Phase.musicSpeed);
            player.Controller.ClearLockTarget();
            cameraController.ResetBehind(guardianFace.position);
            transitioning = false;
            session.SetInputLocked(this, false);
        }

        /// <summary>演出中止（換場景、分出勝負）：只放開自己的鎖，不冒充完成。</summary>
        public void CancelTransition()
        {
            transitioning = false;
            if (session != null) session.SetInputLocked(this, false);
        }
    }
}
