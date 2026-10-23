using UnityEngine;

namespace BossFight.Core
{
    /// <summary>玩家設定（ESC 選單的「設定」）的預設值：第一次啟動、沒有存檔，或存檔被清掉時用。存檔後以 PlayerPrefs 為準。</summary>
    [CreateAssetMenu(menuName = "BossFight/Game Options Data", fileName = "GameOptionsData")]
    public class GameOptionsData : ScriptableObject
    {
        [Header("音量預設值（0..1）")]
        [Tooltip("主音量（AudioListener.volume）")]
        [Range(0f, 1f)] public float defaultMasterVolume = 1f;
        [Tooltip("音樂：乘在 AudioManager 的 musicVolume 上")]
        [Range(0f, 1f)] public float defaultMusicVolume = 1f;
        [Tooltip("音效：戰鬥音效、演出的 Timeline 音軌、選單試聽")]
        [Range(0f, 1f)] public float defaultSfxVolume = 1f;

        [Header("開發者模式")]
        [Tooltip("沒有存檔時開發者模式（F1–F7、F12、Debug 面板）是否開啟；正式 build 沒有 Debug 程式，這個值沒有作用")]
        public bool defaultDeveloperMode = true;
    }
}
