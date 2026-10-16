using UnityEngine;

namespace BossFight.Boss.Moves
{
    /// <summary>
    /// 扭屁股飛撲：預兆伏低扭屁股，地上的落點圈跟著玩家；起跳時鎖定落點，拋物線飛 flightTime 秒，落地瞬間打一下圓柱判定。
    /// 落點停在玩家前方 landShort 公尺，Boss 才不會壓進玩家身體；landingRadius 要大於 landShort + 玩家 Hurtbox 半徑，才蓋得到玩家站的位置。
    /// 資產在 Assets/ScriptableObjects/Boss/Moves/Phase1/。
    /// </summary>
    [CreateAssetMenu(fileName = "ButtPounce", menuName = "BossFight/Boss/Moves/Butt Pounce")]
    public class ButtPounceData : BossMoveData
    {
        [Header("扭屁股飛撲")]
        [Tooltip("滯空時間（秒）")]
        [Min(0.05f)] public float flightTime = 0.5f;
        [Tooltip("拋物線最高點離地 (m)")]
        [Min(0f)] public float leapHeight = 2f;
        [Tooltip("落點在玩家前方多遠 (m)")]
        [Min(0f)] public float landShort = 1.2f;
        [Tooltip("落地判定的半徑 (m)，以落點為中心")]
        [Min(0.1f)] public float landingRadius = 2f;
        [Tooltip("落地判定的高度 (m)，從地面往上")]
        [Min(0.1f)] public float hitHeight = 2f;
        [Tooltip("落地判定的持續時間（秒）")]
        [Min(0.01f)] public float hitWindow = 0.1f;
    }
}
