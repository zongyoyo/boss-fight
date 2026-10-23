using System;
using BossFight.Boss;
using BossFight.Core;
using BossFight.Cutscene;
using UnityEngine;

namespace BossFight.Battle
{
    /// <summary>戰鬥流程的狀態：Intro → Phase1 → Transition → Phase2 → Ultimate → Berserk。勝利、失敗由 T9 加上。</summary>
    public enum BattleState
    {
        /// <summary>還沒開始（Start 之前）。</summary>
        None,
        /// <summary>開場動畫：玩家輸入鎖定，Boss 還沒行動。</summary>
        Intro,
        /// <summary>第一階段：BossController 從開場序列開始出招。</summary>
        Phase1,
        /// <summary>轉階段演出：Boss 無敵、場上 Hazard 已清掉、玩家輸入鎖定。</summary>
        Transition,
        /// <summary>第二階段。</summary>
        Phase2,
        /// <summary>終極技：Boss 血量首次降到狂暴門檻，放 OIIA 大迴旋（浮空倒數、安全區、落地頭暈）；頭暈結束後進入 Berserk。</summary>
        Ultimate,
        /// <summary>狂暴（HP ≤ 400）：出招間隔 0.3 秒、冷卻 ×0.75、音樂 1.3 倍速，招式池不變。</summary>
        Berserk,
    }

    /// <summary>
    /// 戰鬥流程狀態機：Intro → Phase1 → Transition → Phase2 → Ultimate → Berserk。Start 時記一次挑戰，播開場動畫；第二次挑戰起開場可以長按跳過。
    /// 開場結束（播完或跳過）時記下「看過開場」，切到第一階段並呼叫 BossController.StartBattle，Boss 從固定開場序列開打。
    /// Boss 血量降到第二階段門檻時 BossController 自己進入 Transition（無敵、打斷招式、清 Hazard）；這裡收到後播轉階段演出，
    /// 演出結束時呼叫 BossController.CompleteTransition 套用第二階段，切到 Phase2。有接轉階段演出時會關掉 Boss 的 AutoCompleteTransition。
    /// 血量首次降到狂暴門檻時 BossController 自己出狂暴的進場招式 OIIA 大迴旋（OnMoveChosen 的 MoveChoiceSource.PhaseEntry），這裡切到 Ultimate；
    /// 大迴旋與落地頭暈結束、Boss 套用狂暴（OnPhaseChanged）時切到 Berserk，音樂改成狂暴的速度。沒有設定進場招式時從 Phase2 直接切到 Berserk。
    /// 音樂速度：換階段時照 BossPhaseData.musicSpeed 設定；轉階段演出開始時先排好下一階段的速度，演出中重播音樂時才套用。
    /// 沒有接 CutsceneDirector 時直接開打／直接換階段。「看過開場」存在 BattleSession，重新載入場景（重試）時保留。
    /// UI 與演出訂閱 OnBattleStateChanged。
    /// 放在 Battle（最上層）：它要指揮 Boss 與演出，Core 才能維持在最底層、不引用其他系統。
    /// </summary>
    [DisallowMultipleComponent]
    public class BattleManager : MonoBehaviour
    {
        [Tooltip("開場動畫；留空時直接開打")]
        [SerializeField] CutsceneDirector intro;
        [Tooltip("轉階段演出；留空時 Boss 照 BossPhaseData.transitionTime 計時自動轉階段")]
        [SerializeField] CutsceneDirector transition;
        [SerializeField] BossController boss;
        [Tooltip("換階段時改音樂速度；留空時不管音樂")]
        [SerializeField] AudioManager audioManager;

        /// <summary>流程狀態改變；參數是新的狀態。</summary>
        public event Action<BattleState> OnBattleStateChanged;

        public BattleState State { get; private set; }

        void OnEnable()
        {
            if (intro != null) intro.OnCutsceneFinished += HandleIntroFinished;
            if (transition != null) transition.OnCutsceneFinished += HandleTransitionFinished;
            if (boss != null)
            {
                boss.OnStateChanged += HandleBossStateChanged;
                boss.OnPhaseChanged += HandlePhaseChanged;
                boss.OnMoveChosen += HandleMoveChosen;
            }
        }

        void OnDisable()
        {
            if (intro != null) intro.OnCutsceneFinished -= HandleIntroFinished;
            if (transition != null) transition.OnCutsceneFinished -= HandleTransitionFinished;
            if (boss != null)
            {
                boss.OnStateChanged -= HandleBossStateChanged;
                boss.OnPhaseChanged -= HandlePhaseChanged;
                boss.OnMoveChosen -= HandleMoveChosen;
            }
        }

        void Start()
        {
            if (boss != null) boss.AutoCompleteTransition = transition == null;
            BattleSession.BeginAttempt();
            if (intro == null)
            {
                StartPhase1();
                return;
            }
            SetState(BattleState.Intro);
            intro.Play(BattleSession.IntroSeen);
        }

        void HandleIntroFinished(bool skipped)
        {
            if (State != BattleState.Intro) return;
            BattleSession.MarkIntroSeen();
            StartPhase1();
        }

        void StartPhase1()
        {
            SetState(BattleState.Phase1);
            if (boss != null) boss.StartBattle();
        }

        void HandleBossStateChanged(BossState next)
        {
            if (next != BossState.Transition || State == BattleState.Transition) return;
            SetState(BattleState.Transition);
            if (transition == null) return; // Boss 自己計時結束，HandlePhaseChanged 再切到第二階段

            var phase = boss.PendingPhase;
            if (audioManager != null && phase != null) audioManager.SetMusicSpeed(phase.musicSpeed, false);
            transition.Play(false);
        }

        void HandleTransitionFinished(bool skipped)
        {
            if (State == BattleState.Transition) boss.CompleteTransition(); // → OnPhaseChanged → Phase2
        }

        // 進場招式（OIIA 大迴旋）開始
        void HandleMoveChosen(BossMoveChoice choice)
        {
            if (choice.source == MoveChoiceSource.PhaseEntry && State == BattleState.Phase2) SetState(BattleState.Ultimate);
        }

        void HandlePhaseChanged(BossPhaseData phase)
        {
            if (audioManager != null) audioManager.SetMusicSpeed(phase.musicSpeed);
            switch (State)
            {
                case BattleState.Transition:
                    SetState(BattleState.Phase2);
                    break;
                case BattleState.Phase2:
                case BattleState.Ultimate:
                    SetState(BattleState.Berserk);
                    break;
            }
        }

        void SetState(BattleState next)
        {
            State = next;
            OnBattleStateChanged?.Invoke(next);
        }
    }
}
