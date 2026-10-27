using BossFight.Hazards;
using UnityEngine;
namespace BossFight.Boss.Moves
{
    /// <summary>全場八圈；兩圈在玩家附近，提示開始即鎖點，白圈代表可擋。</summary>
    public class LeafStormMove : GuardianMove
    {
        public override void BeginActive()
        { Wave(); }
        void Wave()
        {
            Vector3 player = PlayerPoint;
            Telegraph(Data.canBlock, Spec.warning);
            for (int i = 0; i < Spec.count / Spec.waves; i++)
            {
                Vector2 offset = Random.insideUnitCircle * Spec.scatter;
                Vector3 p = i < Spec.nearCount ? ClampPoint(player + new Vector3(offset.x, 0f, offset.y), Spec.radius) : RandomFieldPoint(Spec.radius);
                Spawn(Area(AttackAreaShape.Circle, p, Vector3.forward), Spec.warning, false);
            }
            index++;
        }
        public override bool TickActive(float dt)
        {
            timer += dt;
            if (index < Spec.waves && timer >= index * Spec.interval) Wave();
            return index >= Spec.waves && AllResolved;
        }
    }
}
