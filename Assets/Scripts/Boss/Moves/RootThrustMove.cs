using BossFight.Hazards;
using UnityEngine;
namespace BossFight.Boss.Moves
{
    /// <summary>
    /// 每段預兆開始時鎖定腳下位置，不追蹤已顯示的裂紋。第 1 段用招式預兆，之後每段在落下前 warning 秒開預兆（不低於階段下限），
    /// 段與段落下相隔 interval。
    /// </summary>
    public class RootThrustMove : GuardianMove
    {
        public override void BeginTelegraph() { base.BeginTelegraph(); Spawn(Area(AttackAreaShape.Circle, PlayerPoint, Vector3.forward), TelegraphTime, false); index = 1; }
        public override bool TickActive(float dt)
        {
            timer += dt;
            if (index < Spec.count && timer >= index * Spec.interval - Spec.warning)
            { index++; Spawn(Area(AttackAreaShape.Circle, PlayerPoint, Vector3.forward), Spec.warning); }
            return index >= Spec.count && AllResolved;
        }
    }
}
