using BossFight.Hazards;
namespace BossFight.Boss.Moves
{
    /// <summary>前方扇形橫掃，預警固定後照同一幾何命中。</summary>
    public class VineSweepMove : GuardianMove
    {
        public override void BeginTelegraph() { base.BeginTelegraph(); Spawn(Area(AttackAreaShape.Sector, Origin, Aim), TelegraphTime, false); }
        public override bool TickActive(float dt) => AllResolved;
    }
}
