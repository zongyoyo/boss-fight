using BossFight.Hazards;
using UnityEngine;
namespace BossFight.Boss.Moves
{
    /// <summary>兩條藤依序砸地；各段白光、填充、音效與判定同步。</summary>
    public class VineSlamMove : GuardianMove
    {
        Vector3 aim;
        AttackArea Line(float side) => Area(AttackAreaShape.Rectangle, Origin + Vector3.Cross(Vector3.up, aim) * Spec.sideOffset * side, aim);
        public override void BeginTelegraph() { base.BeginTelegraph(); aim = Aim; Spawn(Line(-1f), TelegraphTime, false); }
        public override void BeginActive() { Spawn(Line(1f), Spec.interval); }
        public override bool TickActive(float dt) => AllResolved;
    }
}
