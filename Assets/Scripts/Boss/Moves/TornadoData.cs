using UnityEngine;

namespace BossFight.Boss.Moves
{
    /// <summary>
    /// OIIA 龍捲風（第二階段新招）：預兆 telegraphTime 秒原地旋轉成漩渦，之後 duration 秒以 chaseSpeed 追蹤玩家，
    /// 漩渦每 tickInterval 秒打一次 damage（不可擋）；結束後硬直 recoveryTime 秒，並給自己加 endImbalance 的失衡值。
    /// 資產在 Assets/ScriptableObjects/Boss/Moves/Phase2/。
    /// </summary>
    [CreateAssetMenu(fileName = "Tornado", menuName = "BossFight/Boss/Moves/Tornado")]
    public class TornadoData : BossMoveData
    {
        [Header("OIIA 龍捲風")]
        [Tooltip("追蹤玩家的時間（秒）")]
        [Min(0.1f)] public float duration = 5f;
        [Tooltip("追蹤速度 (m/s)，略慢於玩家")]
        [Min(0f)] public float chaseSpeed = 4f;
        [Tooltip("每隔幾秒打一次")]
        [Min(0.05f)] public float tickInterval = 0.5f;
        [Tooltip("漩渦判定的半徑 (m)，以 Boss 為中心")]
        [Min(0.1f)] public float hitRadius = 1.8f;
        [Tooltip("漩渦判定的高度 (m)，從地面往上；跳起來也躲不掉")]
        [Min(0.1f)] public float hitHeight = 4f;
        [Tooltip("結束時給自己加的失衡值（硬直開始時加；加滿就直接頭暈）")]
        [Min(0f)] public float endImbalance = 40f;
    }
}
