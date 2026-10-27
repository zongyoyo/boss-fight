using BossFight.Hazards;
using UnityEngine;
namespace BossFight.Boss.Moves
{
    /// <summary>五道葉刃；整條飛行路線先畫白色預警，出手後葉片與掃掠判定前進。</summary>
    public class LeafScatterMove : GuardianMove
    {
        Vector3 aim;
        readonly System.Collections.Generic.List<AreaStrike> previews = new System.Collections.Generic.List<AreaStrike>();
        Vector3 Direction(int i) => Quaternion.Euler(0f, (Spec.count > 1 ? -Spec.angle * 0.5f + Spec.angle * i / (Spec.count - 1) : 0f) + index * Spec.angleOffset, 0f) * aim;
        void Preview(float duration)
        {
            for (int i = 0; i < Spec.count; i++) { var a = Area(AttackAreaShape.Rectangle, Origin, Direction(i)); a.length = MaximumRange; previews.Add(Spawn(a, duration, false, true)); }
        }
        public override void BeginTelegraph()
        {
            base.BeginTelegraph(); aim = Aim; previews.Clear(); Preview(TelegraphTime);
        }
        public override void BeginActive()
        {
            Launch();
            if (index < Spec.waves) { Telegraph(Data.canBlock, Spec.interval); Preview(Spec.interval); }
        }
        void Launch()
        {
            foreach (var p in previews) if (p != null) p.Despawn(); previews.Clear();
            for (int i = 0; i < Spec.count; i++)
            {
                var a = Area(AttackAreaShape.Rectangle, Origin, Direction(i));
                var leaf = Spawn(a, 0f, false); leaf.Fly(Spec.speed, MaximumRange); leaf.SetProjectileVisual(Spec.centerHeight);
            }
            index++;
        }
        public override bool TickActive(float dt)
        {
            timer += dt;
            if (index < Spec.waves && timer >= Spec.interval) Launch();
            return index >= Spec.waves;
        }
    }
}
