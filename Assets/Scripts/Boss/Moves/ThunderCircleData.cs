using UnityEngine;

namespace BossFight.Boss.Moves
{
    /// <summary>
    /// 魔法陣落雷：預兆抬頭長叫，出招時地上出現 circleCount 個紅圈（Hazards/LightningStrike），warningTime 秒後同時落雷，每道 damage。
    /// 第一個紅圈在玩家當下的位置，其他的在玩家周圍 scatterMin–scatterMax 公尺內隨機。
    /// recoveryTime 是「施法中」Boss 不動的總時間，從紅圈出現算起（企劃書的 2.0 秒），落雷後剩下的時間才是破綻。
    /// 資產在 Assets/ScriptableObjects/Boss/Moves/Phase1/。
    /// </summary>
    [CreateAssetMenu(fileName = "ThunderCircle", menuName = "BossFight/Boss/Moves/Thunder Circle")]
    public class ThunderCircleData : BossMoveData
    {
        [Header("魔法陣落雷")]
        [Tooltip("紅圈數量")]
        [Min(1)] public int circleCount = 3;
        [Tooltip("紅圈（落雷判定）的半徑 (m)")]
        [Min(0.1f)] public float circleRadius = 2f;
        [Tooltip("紅圈出現到落雷的時間（秒）")]
        [Min(0f)] public float warningTime = 1f;
        [Tooltip("其他紅圈離玩家的最近距離 (m)")]
        [Min(0f)] public float scatterMin = 2.5f;
        [Tooltip("其他紅圈離玩家的最遠距離 (m)")]
        [Min(0f)] public float scatterMax = 5f;
        [Tooltip("紅圈中心之間盡量相隔的距離 (m)；找不到夠遠的位置時會重疊")]
        [Min(0f)] public float minSpacing = 3f;
        [Tooltip("紅圈中心離外牆至少多遠 (m)")]
        [Min(0f)] public float edgeMargin = 1f;
        [Tooltip("落雷判定的持續時間（秒）")]
        [Min(0.01f)] public float hitWindow = 0.1f;
        [Tooltip("落雷判定的高度 (m)，從地面往上；跳起來也躲不掉")]
        [Min(0.1f)] public float hitHeight = 6f;
    }
}
