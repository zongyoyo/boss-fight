using System;
using BossFight.Boss;
using BossFight.Core;

namespace BossFight.Battle
{
    /// <summary>
    /// 開發者工具（Debug 的 BossDebugTools、BalanceDebugPanel）認得的一場 Boss 戰流程。每場戰鬥各自實作：
    /// OIIA 貓是 BattleManager，序章的大樹守衛是 PrologueCutsceneFlow。Debug 工具只認這個介面，同一張按鍵表在兩場戰鬥都能用。
    /// 這些入口只給開發者工具呼叫，遊戲流程不要用。
    /// </summary>
    public interface IBattleDebugTarget
    {
        /// <summary>Boss 面板與平衡面板的標題（Boss 名稱）。</summary>
        string DebugBossName { get; }
        /// <summary>計時用的戰鬥狀態。序章：還沒開戰 None、萬根齊發 Ultimate、萬根之後到二階段開打 Transition。</summary>
        BattleState DebugState { get; }
        event Action<BattleState> OnDebugStateChanged;
        /// <summary>Ultimate（終極技那一段）在轉階段之前（序章的萬根齊發），還是在第二階段裡面（OIIA 的大迴旋）。</summary>
        bool DebugUltimateBeforeTransition { get; }
        /// <summary>計時上 Ultimate 那一段的名稱。</summary>
        string DebugUltimateName { get; }
        /// <summary>這場的難度（平衡面板的每場紀錄）。</summary>
        Difficulty DebugDifficulty { get; }
        /// <summary>第幾次挑戰。</summary>
        int DebugAttempt { get; }
        /// <summary>已經分出勝負（結算畫面或結局演出）。</summary>
        bool DebugIsOver { get; }

        /// <summary>按鍵提示上 F1 的說明。</summary>
        string DebugNextPhaseHint { get; }
        /// <summary>F1：進到下一個階段；回傳顯示給開發者的訊息。</summary>
        string DebugNextPhase();
        /// <summary>F6：Boss 血量歸零（越過血量鎖），接勝利流程；回傳訊息。</summary>
        string DebugDefeatBoss();
        /// <summary>F5：重新挑戰。</summary>
        void DebugRetry();
        /// <summary>F3：指定下一招。回傳 true 表示這一場自己處理了（例如萬根齊發走自己的流程），false 時照一般的 BossController.ForceNextMove。</summary>
        bool DebugForceMove(BossMove move);
        /// <summary>已經要求、還沒出的特殊招（不在 BossController.ForcedNextMove 裡的那種）；沒有時 null。</summary>
        BossMove DebugPendingMove { get; }
        /// <summary>F4 面板多顯示的幾行（rich text）；沒有時空字串。</summary>
        string DebugPanelLines();
    }
}
