using System;
using BossFight.Boss;
using BossFight.Cutscene;
using UnityEngine;

namespace BossFight.Core
{
    /// <summary>戰鬥流程的狀態。T6 只有開場與第一階段；轉階段、第二階段、勝利、失敗由 T7、T9 加上。</summary>
    public enum BattleState
    {
        /// <summary>還沒開始（Start 之前）。</summary>
        None,
        /// <summary>開場動畫：玩家輸入鎖定，Boss 還沒行動。</summary>
        Intro,
        /// <summary>第一階段：BossController 從開場序列開始出招。</summary>
        Phase1,
    }

    /// <summary>
    /// 戰鬥流程狀態機（目前只有 Intro → Phase1）。Start 時記一次挑戰，播開場動畫；第二次挑戰起開場可以長按跳過。
    /// 開場結束（播完或跳過）時記下「看過開場」，切到第一階段並呼叫 BossController.StartBattle，Boss 從固定開場序列開打。
    /// 沒有接 CutsceneDirector 時直接開打。「看過開場」存在 BattleSession，重新載入場景（重試）時保留。
    /// UI 與演出訂閱 OnBattleStateChanged。
    /// </summary>
    [DisallowMultipleComponent]
    public class BattleManager : MonoBehaviour
    {
        [Tooltip("開場動畫；留空時直接開打")]
        [SerializeField] CutsceneDirector intro;
        [SerializeField] BossController boss;

        /// <summary>流程狀態改變；參數是新的狀態。</summary>
        public event Action<BattleState> OnBattleStateChanged;

        public BattleState State { get; private set; }

        void OnEnable()
        {
            if (intro != null) intro.OnIntroFinished += HandleIntroFinished;
        }

        void OnDisable()
        {
            if (intro != null) intro.OnIntroFinished -= HandleIntroFinished;
        }

        void Start()
        {
            BattleSession.BeginAttempt();
            if (intro == null)
            {
                StartPhase1();
                return;
            }
            SetState(BattleState.Intro);
            intro.PlayIntro(BattleSession.IntroSeen);
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

        void SetState(BattleState next)
        {
            State = next;
            OnBattleStateChanged?.Invoke(next);
        }
    }
}
