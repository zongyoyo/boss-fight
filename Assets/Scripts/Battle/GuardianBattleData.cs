using UnityEngine;
namespace BossFight.Battle
{
    [CreateAssetMenu(menuName = "BossFight/Battle/Guardian Battle", fileName = "GuardianBattle")]
    public class GuardianBattleData : ScriptableObject
    {
        [Header("一階段")]
        [Min(0f), Tooltip("一階段時間上限 (s)")] public float phaseDuration = 60f;
        [Min(0f), Tooltip("萬根事前指引時機 (s)")] public float guidanceAt = 56f;
        [Range(0f, 1f), Tooltip("一階段血量下限比例")] public float healthFloorRatio = 0.8f;
        [Min(0f), Tooltip("事前指引的安全半徑 (m)")] public float guidanceRadius = 9f;
        [TextArea, Tooltip("萬根事前提示")] public string warningText = "即將萬根齊發，靠近守衛（9 m 內安全）";
        [TextArea, Tooltip("活過萬根的交接訊息")] public string transitionText = "守衛準備全力應戰……";
        [Header("守衛實體")]
        [Min(0f), Tooltip("樹幹的實體／受擊半徑 (m)")] public float bodyRadius = 1.1f;
        [Min(0f), Tooltip("樹洞受擊區高度 (m)")] public float hurtHeight = 2f;
        [Header("鎖定鏡頭（純視覺，GuardianBuilder 寫進守衛的 LockOnTarget）")]
        [Min(0f), Tooltip("鏡頭看的點的高度 (m)：樹幹中段")] public float lockAimHeight = 4f;
        [Min(0f), Tooltip("鎖定鏡頭框住守衛用的半徑 (m)")] public float lockFramingRadius = 3f;
        [Range(-10f, 60f), Tooltip("鎖定守衛時的俯視角度（度）；低一點才看得到樹冠")] public float lockOnPitch = 6f;
        [Min(0f), Tooltip("鎖定守衛時鏡頭離玩家的距離 (m)；0 = 鎖定鏡頭原本的距離")] public float lockOnDistance = 8f;
        [Tooltip("鎖定菱形與處決標記的位置（樹洞，相對守衛根物件）")] public Vector3 markerOffset = new Vector3(0f, 1.4f, 0.8f);
        [Header("二階段")]
        [Range(0f, 1f), Tooltip("每個二階段招式開始消耗當前血量比例")] public float selfCostRatio = 0.05f;
        [Min(0f), Tooltip("轉階段演出開始時把玩家擺到守衛南側的距離 (m)；推開的終點在 PrologueCutsceneData.pushRadius")] public float transitionStageRadius = 6f;
        [Tooltip("二階段血條名稱")] public string phase2Name = "大樹守衛・全力";
        [Tooltip("守衛的名字（轉階段、勝利演出的說話者）")] public string guardianName = "大樹守衛";
        [TextArea, Tooltip("轉階段演出的台詞（PrologueCutsceneFlow 在 TransitionLine cue 播）")] public string phase2Line = "很少有人能堅持到這裡，我認可你了，接下來我要出全力了。";
        [Tooltip("二階段血條顏色")] public Color phase2HealthColor = new Color(0.85f, 0.06f, 0.04f);
        [Min(0f), Tooltip("自損區段白閃秒數（純視覺）")] public float selfCostFlashTime = 0.2f;
    }
}
