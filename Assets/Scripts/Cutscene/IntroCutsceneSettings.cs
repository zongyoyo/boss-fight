using UnityEngine;

namespace BossFight.Cutscene
{
    /// <summary>
    /// 開場動畫中不放在 Timeline 上的數值：長按跳過的秒數、字卡文字。
    /// 鏡頭、效果、聲音的時間點都在 Timeline 資產（Assets/Timelines/Intro.playable）上，在 Timeline 視窗調整。
    /// 執行期只讀不寫；資產在 Assets/ScriptableObjects/Cutscene/IntroCutsceneSettings.asset。
    /// </summary>
    [CreateAssetMenu(fileName = "IntroCutsceneSettings", menuName = "BossFight/Cutscene/Intro Cutscene Settings")]
    public class IntroCutsceneSettings : ScriptableObject
    {
        [Header("跳過")]
        [Tooltip("第二次挑戰起，長按 SkipCutscene（Space）多少秒跳過開場")]
        [Min(0.1f)] public float skipHoldTime = 1f;

        [Header("字卡")]
        [Tooltip("第一行（大字）")]
        public string title = "BOSS FIGHT";
        [Tooltip("第二行：Boss 名稱")]
        public string bossName = "OIIA 貓";
    }
}
