using BossFight.Hazards;
using UnityEngine;
namespace BossFight.Boss.Moves
{
    /// <summary>每段預兆開始時鎖定腳下位置，不追蹤已顯示的裂紋。</summary>
    public class RootThrustMove : GuardianMove
    {
        public override void BeginTelegraph() { base.BeginTelegraph(); Spawn(Area(AttackAreaShape.Circle, PlayerPoint, Vector3.forward), TelegraphTime, false); index = 1; }
        public override bool TickActive(float dt)
        {
            timer += dt;
            if (index < Spec.count && timer + TelegraphTime >= index * Spec.interval)
            { index++; Spawn(Area(AttackAreaShape.Circle, PlayerPoint, Vector3.forward), Spec.warning); }
            return index >= Spec.count && AllResolved;
        }
    }
}
