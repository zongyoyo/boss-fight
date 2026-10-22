using UnityEngine;

namespace BossFight.Boss.Moves
{
    /// <summary>
    /// 貓掌連拍：預兆舉起右爪，出招時左右爪輪流往前拍 strikeCount 下，每下傷害是 damage。
    /// 判定是 Boss 前方的長方體（從 Boss 中心往前 hitReach）；方向在第一下鎖定，兩下之間不轉向。
    /// enhancedFinisher 打開時最後一下改用 finisher 的數值（第二階段的貓掌三連拍：第三下帶電、範圍變大、紅光不可擋）。
    /// 資產在 Assets/ScriptableObjects/Boss/Moves/Phase1/、Phase2/。
    /// </summary>
    [CreateAssetMenu(fileName = "PawSwipe", menuName = "BossFight/Boss/Moves/Paw Swipe")]
    public class PawSwipeData : BossMoveData
    {
        [Header("貓掌連拍")]
        [Tooltip("拍幾下")]
        [Min(1)] public int strikeCount = 2;
        [Tooltip("兩下之間的間隔（秒，從上一下的判定開始算）。玩家直接受擊後有無敵時間，間隔要比它長，第二下才打得到")]
        [Min(0.05f)] public float strikeInterval = 0.6f;
        [Tooltip("每一下的判定持續時間（秒）")]
        [Min(0.01f)] public float hitWindow = 0.1f;
        [Tooltip("判定往前的距離 (m)，從 Boss 中心量起；要比 maxDistance + 玩家 Hurtbox 半徑長，走到範圍邊緣出手才打得到")]
        [Min(0f)] public float hitReach = 3f;
        [Tooltip("判定的寬度 (m)")]
        [Min(0f)] public float hitWidth = 2.4f;
        [Tooltip("判定的高度 (m)，從腳底往上")]
        [Min(0.1f)] public float hitHeight = 2f;

        [Header("最後一下強化（第二階段三連拍）")]
        [Tooltip("最後一下改用下面的數值；關掉時每一下都一樣（第一階段）")]
        public bool enhancedFinisher;
        [Tooltip("最後一下的傷害")]
        [Min(0f)] public float finisherDamage = 24f;
        [Tooltip("最後一下可以用盾擋（預兆白光）；不可擋為紅光")]
        public bool finisherCanBlock;
        [Tooltip("最後一下可以盾反")]
        public bool finisherCanParry;
        [Tooltip("最後一下判定範圍的倍率（往前的距離與寬度各乘上這個值）")]
        [Min(0.1f)] public float finisherRangeScale = 1.5f;
        [Tooltip("倒數第二下到最後一下的間隔（秒）：這段時間是最後一下的預兆（換顏色），要夠玩家看到紅光後翻滾")]
        [Min(0.05f)] public float finisherInterval = 0.8f;
        [Tooltip("最後一下帶電（落雷特效）")]
        public bool finisherElectric = true;

        /// <summary>第 index 下（從 0 算）是不是強化的最後一下。</summary>
        public bool IsFinisher(int index) => enhancedFinisher && strikeCount > 1 && index == strikeCount - 1;
    }
}
