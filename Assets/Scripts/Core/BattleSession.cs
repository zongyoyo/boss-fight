using UnityEngine;

namespace BossFight.Core
{
    /// <summary>
    /// 這次執行（開遊戲到關遊戲）的挑戰紀錄：第幾次挑戰、看過開場沒有、看過轉階段沒有，以及 ESC 選單的秘技解鎖了開發者模式開關沒有。
    /// 重新載入場景（重試）時保留，關掉遊戲就歸零。
    /// 只有 BattleManager 讀寫；其他系統要知道就訂閱 BattleManager 的事件，或由它用參數傳過去（例如 CutsceneDirector.Play 的 canSkip）。
    /// 這是 CLAUDE.md「不用 static」規則的例外：場景重新載入後元件都是新的，跨場景的資料總要放在某個活得比場景久的地方；
    /// 這裡只放這四個值，不放任何元件引用。進 Play 模式時（包含關掉 Domain Reload 的情況）會清空。
    /// </summary>
    public static class BattleSession
    {
        /// <summary>這次執行的第幾次挑戰；BattleManager 開場前加一，第一次挑戰是 1。</summary>
        public static int Attempt { get; private set; }

        /// <summary>開場動畫已經播完（或跳過）過一次；之後的挑戰可以長按跳過。</summary>
        public static bool IntroSeen { get; private set; }

        /// <summary>轉階段演出已經播完（或跳過）過一次；之後再轉階段可以長按跳過。</summary>
        public static bool TransitionSeen { get; private set; }

        /// <summary>ESC 選單輸入了秘技：這次執行的選單都顯示開發者模式開關（不存檔）。</summary>
        public static bool DeveloperOptionsUnlocked { get; private set; }

        public static void BeginAttempt() => Attempt++;

        public static void MarkIntroSeen() => IntroSeen = true;

        public static void MarkTransitionSeen() => TransitionSeen = true;

        public static void MarkDeveloperOptionsUnlocked() => DeveloperOptionsUnlocked = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnLaunch()
        {
            Attempt = 0;
            IntroSeen = false;
            TransitionSeen = false;
            DeveloperOptionsUnlocked = false;
        }
    }
}
