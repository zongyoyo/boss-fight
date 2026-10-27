using UnityEngine;
namespace BossFight.Boss.Moves
{
    [CreateAssetMenu(menuName = "BossFight/Guardian/Seed Bomb")]
    public class SeedBombData : GuardianMoveData
    {
        [Min(0f), Tooltip("種子飛行秒數")] public float flightTime = 0.6f;
        [Min(0f), Tooltip("拋物線抬高 (m)")] public float arcHeight = 6f;
        [Min(0f), Tooltip("額外三顆落點的環半徑 (m)")] public float landingSpread = 3f;
    }
}
