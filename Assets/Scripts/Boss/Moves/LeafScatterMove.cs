using BossFight.Hazards;
using UnityEngine;
namespace BossFight.Boss.Moves
{
    /// <summary>五道葉刃；整條飛行路線先畫白色預警，出手後葉片與掃掠判定前進。</summary>
    public class LeafScatterMove : GuardianMove
    {
        Vector3 aim;
        Vector3 Direction(int i) => Quaternion.Euler(0f, Spec.count > 1 ? -Spec.angle * 0.5f + Spec.angle * i / (Spec.count - 1) : 0f, 0f) * aim;
        public override void BeginTelegraph()
        {
            base.BeginTelegraph(); aim = Aim;
            for (int i = 0; i < Spec.count; i++) { var a = Area(AttackAreaShape.Rectangle, Origin, Direction(i)); a.length = MaximumRange; Spawn(a, TelegraphTime, false, true); }
        }
        public override void BeginActive()
        {
            ClearPreviews();
            for (int i = 0; i < Spec.count; i++)
            {
                var a = Area(AttackAreaShape.Rectangle, Origin, Direction(i));
                var leaf = Spawn(a, 0f, false); leaf.Fly(Spec.speed, MaximumRange); leaf.SetProjectileVisual(Spec.centerHeight);
            }
        }
        public override bool TickActive(float dt) => true;
    }
}
