#if UNITY_EDITOR || DEVELOPMENT_BUILD || BOSSFIGHT_DEV_TOOLS
using BossFight.Core;
using UnityEngine;

namespace BossFight.DebugTools
{
    /// <summary>
    /// 開發者模式（ESC 選單「設定」裡的開關，存在 GameOptions）：關掉時停用 Debug 工具（BossDebugTools 的 F1–F7 與 Boss 面板、
    /// BalanceDebugPanel 的 F12 與計時、PlayerDebugOverlay 的左上角狀態），快捷鍵也不吃。
    /// 從開切到關時順便清掉 F2 玩家無敵與 F3 指定的下一招；F12 已經改過的數值留著（複本照用，重開 Play 才回到資產的值）。
    /// 暫停（ESC 選單）時也先停用：IMGUI 面板會蓋在選單上，F 鍵也不該在選單裡生效；開著的 F12 面板會被關掉。
    /// 只訂閱 GameOptions.OnDeveloperModeChanged 與 GamePause.OnPausedChanged。
    /// </summary>
    [DisallowMultipleComponent]
    public class DeveloperMode : MonoBehaviour
    {
        [SerializeField] GameOptions options;
        [SerializeField] GamePause pause;
        [Tooltip("關掉開發者模式時清掉 F2 無敵與 F3 指定招")]
        [SerializeField] BossDebugTools bossTools;
        [Tooltip("開發者模式開著、而且沒有暫停時才啟用的 Debug 元件")]
        [SerializeField] Behaviour[] tools = new Behaviour[0];

        bool developerMode = true, paused;

        void OnEnable()
        {
            if (options != null) options.OnDeveloperModeChanged += HandleDeveloperMode;
            if (pause != null) pause.OnPausedChanged += HandlePaused;
        }

        void OnDisable()
        {
            if (options != null) options.OnDeveloperModeChanged -= HandleDeveloperMode;
            if (pause != null) pause.OnPausedChanged -= HandlePaused;
        }

        void HandleDeveloperMode(bool on)
        {
            if (developerMode && !on && bossTools != null) bossTools.ClearCheats();
            developerMode = on;
            Apply();
        }

        void HandlePaused(bool on)
        {
            paused = on;
            Apply();
        }

        void Apply()
        {
            bool active = developerMode && !paused;
            foreach (var tool in tools)
            {
                if (tool != null) tool.enabled = active;
            }
        }
    }
}
#endif
