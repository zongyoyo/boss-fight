using BossFight.Hazards;
namespace BossFight.Boss.Moves
{
    /// <summary>
    /// 前方扇形橫掃，預警固定後照同一幾何命中。二階段的第二掃（反方向、secondAngle）在招式預兆時就一起畫出來，
    /// 晚 interval 秒填滿（序章修正 C3），掃動方向跟第一掃相反。
    /// </summary>
    public class VineSweepMove : GuardianMove
    {
        UnityEngine.Vector3 aim;
        public override void BeginTelegraph()
        {
            base.BeginTelegraph(); aim = Aim;
            Spawn(Area(AttackAreaShape.Sector, Origin, aim), TelegraphTime, false).SetSweepDirection(1f);
            if (Spec.count <= 1) return;
            var area = Area(AttackAreaShape.Sector, Origin, -aim);
            area.angle = Spec.secondAngle > 0f ? Spec.secondAngle : Spec.angle;
            Spawn(area, TelegraphTime + Spec.interval, false).SetSweepDirection(-1f);
        }
        public override bool TickActive(float dt) => AllResolved;
    }
}
