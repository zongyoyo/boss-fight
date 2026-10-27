using UnityEngine;
namespace BossFight.Battle
{
    [CreateAssetMenu(menuName = "BossFight/Battle/Guardian Battle", fileName = "GuardianBattle")]
    public class GuardianBattleData : ScriptableObject
    {
        [Header("一階段")]
        [Tooltip("教學完成後直接接守衛；正式登場演出接管時關閉此捷徑")]
        public bool startAfterTutorial = true;
        [Min(0f), Tooltip("一階段時間上限 (s)")] public float phaseDuration = 60f;
        [Min(0f), Tooltip("萬根事前指引時機 (s)")] public float guidanceAt = 56f;
        [Range(0f, 1f), Tooltip("一階段血量下限比例")] public float healthFloorRatio = 0.8f;
        [Min(0f), Tooltip("事前指引的安全半徑 (m)")] public float guidanceRadius = 9f;
        [TextArea, Tooltip("萬根事前提示")] public string warningText = "即將萬根齊發，靠近守衛（9 m 內安全）";
        [TextArea, Tooltip("活過萬根的暫時交接訊息")] public string transitionText = "撐過萬根齊發，等待二階段交接（T29）";
        [Header("守衛實體")]
        [Min(0f), Tooltip("樹幹的實體／受擊半徑 (m)")] public float bodyRadius = 1.1f;
        [Min(0f), Tooltip("樹洞受擊區高度 (m)")] public float hurtHeight = 2f;
    }
}
