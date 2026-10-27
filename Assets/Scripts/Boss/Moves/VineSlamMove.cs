using BossFight.Hazards;
using UnityEngine;
namespace BossFight.Boss.Moves
{
    /// <summary>兩條藤依序砸地；各段白光、填充、音效與判定同步。</summary>
    public class VineSlamMove : GuardianMove
    {
        Vector3 aim;
        AttackArea Line(float side) => Area(AttackAreaShape.Rectangle, Origin + Vector3.Cross(Vector3.up, aim) * Spec.sideOffset * side, aim);
        public override void BeginTelegraph() { base.BeginTelegraph(); aim = Aim; Spawn(Line(-1f), TelegraphTime, false); index = 1; }
        void Next()
        {
            if (Spec.trackLast && index == Spec.count - 1) aim = Aim;
            Spawn(Line(index % 2 == 0 ? -1f : 1f), Spec.interval); index++;
        }
        public override void BeginActive() { if (index < Spec.count) Next(); }
        public override bool TickActive(float dt)
        {
            timer += dt;
            if (index < Spec.count && timer >= (index - 1) * Spec.interval) Next();
            return index >= Spec.count && AllResolved;
        }
    }
}
