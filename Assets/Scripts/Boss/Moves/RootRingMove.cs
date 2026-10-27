using BossFight.Hazards;
using UnityEngine;
namespace BossFight.Boss.Moves
{
    /// <summary>以守衛為中心的兩圈根波；環帶與判定一起擴散到真正場界。</summary>
    public class RootRingMove : GuardianMove
    {
        public override void BeginTelegraph()
        {
            base.BeginTelegraph(); var a = Area(AttackAreaShape.Circle, Origin, Vector3.forward); a.radius = Spec.innerRadius;
            Spawn(a, TelegraphTime, false, true);
        }
        void Launch()
        {
            var a = Area(AttackAreaShape.Ring, Origin, Vector3.forward); a.radius = Spec.innerRadius + Spec.width;
            Spawn(a, 0f, false).Expand(Spec.speed, MaximumRange); index++;
        }
        public override void BeginActive()
        {
            ClearPreviews(); Launch();
            if (Spec.count > 1) Telegraph(Data.canBlock, Spec.interval);
        }
        public override bool TickActive(float dt)
        {
            timer += dt;
            if (index < Spec.count && timer >= Spec.interval) { Launch(); timer = 0f; }
            return index >= Spec.count;
        }
    }
}
