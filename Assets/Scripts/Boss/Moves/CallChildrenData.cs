using UnityEngine;
namespace BossFight.Boss.Moves
{
    [CreateAssetMenu(menuName = "BossFight/Guardian/Call Children")]
    public class CallChildrenData : GuardianMoveData
    {
        [Min(1), Tooltip("場上戰鬥樹精上限")] public int maxChildren = 2;
        [Min(0f), Tooltip("玩家左右生成距離 (m)")] public float spawnDistance = 3f;
        [Min(0f), Tooltip("生成處的避障半徑 (m)")] public float spawnClearance = 0.5f;
    }
}
