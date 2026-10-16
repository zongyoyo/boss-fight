using UnityEngine;

namespace BossFight.Boss.Moves
{
    /// <summary>
    /// 貓掌連拍：預兆舉起右爪，出招時左右爪輪流往前拍 strikeCount 下，每下傷害是 damage。
    /// 判定是 Boss 前方的長方體（從 Boss 中心往前 hitReach）；方向在第一下鎖定，兩下之間不轉向。
    /// 資產在 Assets/ScriptableObjects/Boss/Moves/Phase1/。
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
    }
}
