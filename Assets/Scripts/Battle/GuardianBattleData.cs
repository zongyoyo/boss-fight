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
        [TextArea, Tooltip("活過萬根的交接訊息")] public string transitionText = "守衛準備全力應戰……";
        [Header("守衛實體")]
        [Min(0f), Tooltip("樹幹的實體／受擊半徑 (m)")] public float bodyRadius = 1.1f;
        [Min(0f), Tooltip("樹洞受擊區高度 (m)")] public float hurtHeight = 2f;
        [Header("二階段與暫代演出")]
        [Range(0f, 1f), Tooltip("每個二階段招式開始消耗當前血量比例")] public float selfCostRatio = 0.05f;
        [Min(0f), Tooltip("轉階段鏡頭起始停留秒數")] public float transitionLeadTime = 1f;
        [Min(0f), Tooltip("守衛台詞自動前進秒數")] public float transitionDialogueTime = 3f;
        [Min(0f), Tooltip("南側衝擊波推離秒數")] public float transitionPushTime = 1f;
        [Min(0f), Tooltip("演出安排玩家於守衛南側的距離 (m)")] public float transitionStageRadius = 6f;
        [Min(0f), Tooltip("衝擊波終點距守衛距離 (m)")] public float transitionPushRadius = 9f;
        [Tooltip("二階段血條名稱")] public string phase2Name = "大樹守衛・全力";
        [Tooltip("轉階段說話者")] public string guardianName = "大樹守衛";
        [TextArea, Tooltip("轉階段台詞")] public string phase2Line = "很少有人能堅持到這裡，我認可你了，接下來我要出全力了。";
        [Tooltip("二階段血條顏色")] public Color phase2HealthColor = new Color(0.85f, 0.06f, 0.04f);
        [Min(0f), Tooltip("自損區段白閃秒數（純視覺）")] public float selfCostFlashTime = 0.2f;
        [Min(0f), Tooltip("T29 暫代勝利呈現秒數；T30 接正式完成回呼後停用")] public float placeholderVictoryTime = 10f;
        [TextArea, Tooltip("暫代勝利演出的提示")] public string placeholderVictoryHint = "大樹守衛倒下了……";
    }
}
