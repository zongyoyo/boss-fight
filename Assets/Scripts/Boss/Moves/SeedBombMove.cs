using BossFight.Hazards;
using UnityEngine;
namespace BossFight.Boss.Moves
{
    /// <summary>紅色種子是外觀，爆炸預警與傷害仍使用可格擋的白色分類。</summary>
    public class SeedBombMove : GuardianMove
    {
        [SerializeField] SeedBombVisual seedPrefab;
        [SerializeField] Transform launchPoint;
        public override void BeginActive()
        {
            var d = (SeedBombData)Data;
            Vector3 target = PlayerPoint;
            float delay = d.flightTime + d.warning;
            Telegraph(d.canBlock, delay);
            for (int i = 0; i < d.count; i++)
            {
                Vector3 offset = i == 0 ? Vector3.zero : Quaternion.Euler(0f, (i - 1) * 360f / (d.count - 1), 0f) * Vector3.forward * d.landingSpread;
                var area = Area(AttackAreaShape.Circle, ClampPoint(target + offset, d.radius), Vector3.forward);
                var strike = Spawn(area, delay, false);
                var seed = Instantiate(seedPrefab, strike.transform);
                seed.Launch(launchPoint.position, area.center, d.flightTime, d.arcHeight, d.warning);
            }
        }
        public override bool TickActive(float dt) => AllResolved;
    }
}
