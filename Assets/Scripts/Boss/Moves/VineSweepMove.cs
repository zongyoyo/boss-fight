using BossFight.Hazards;
namespace BossFight.Boss.Moves
{
    /// <summary>前方扇形橫掃，預警固定後照同一幾何命中。</summary>
    public class VineSweepMove : GuardianMove
    {
        UnityEngine.Vector3 aim;
        public override void BeginTelegraph() { base.BeginTelegraph(); aim = Aim; Spawn(Area(AttackAreaShape.Sector, Origin, aim), TelegraphTime, false); }
        public override void BeginActive()
        {
            if (Spec.count <= 1) return;
            var area = Area(AttackAreaShape.Sector, Origin, -aim);
            area.angle = Spec.secondAngle > 0f ? Spec.secondAngle : Spec.angle;
            Spawn(area, Spec.interval);
        }
        public override bool TickActive(float dt) => AllResolved;
    }
}
