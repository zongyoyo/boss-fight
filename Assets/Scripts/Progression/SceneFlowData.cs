using UnityEngine;

namespace BossFight.Progression
{
    /// <summary>換場景的設定：休息區與 Boss 戰的場景名稱、出生點 id、黑幕淡出淡入的時間。SceneFlow 讀。</summary>
    [CreateAssetMenu(menuName = "BossFight/Scene Flow", fileName = "SceneFlowData")]
    public class SceneFlowData : ScriptableObject
    {
        [Header("場景")]
        [Tooltip("休息區的場景名稱（Build Settings 第 0 個）")]
        public string hubScene = "Hub";
        [Tooltip("Boss 戰的場景名稱（T24 起改由 BossDefinition 指定）")]
        public string battleScene = "BossArena";

        [Header("出生點")]
        [Tooltip("啟動遊戲、重置存檔後在休息區的出生點 id（營火旁）")]
        public string startArrival = "Start";
        [Tooltip("從 Boss 戰回來時在休息區的出生點 id（傳送門前）")]
        public string portalArrival = "Portal";

        [Header("黑幕")]
        [Tooltip("換場景前淡出成全黑的秒數（真實時間）")]
        [Min(0f)] public float fadeOutTime = 0.2f;
        [Tooltip("新場景從全黑淡入的秒數（真實時間）")]
        [Min(0f)] public float fadeInTime = 0.2f;
    }
}
