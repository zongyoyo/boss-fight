using BossFight.Hazards;
using UnityEngine;
namespace BossFight.Boss.Moves
{
    /// <summary>
    /// 藤蔓依序砸地；各段白光、填充、音效與判定同步。
    /// 招式預兆一開始就把所有段畫在地上、依序填滿（填滿 = 那一段開判定），段與段之間照 interval，所以每段看得到的預兆都比招式預兆長（序章修正 C3）。
    /// 二階段的最後一條（trackLast）在它自己的預兆開始時才朝玩家重新瞄準，預兆長度用 warning（不低於階段下限）。
    /// </summary>
    public class VineSlamMove : GuardianMove
    {
        Vector3 aim;
        int retargetIndex; // 等到自己的預兆才畫的那一條（沒有 = -1）
        AttackArea Line(int i) => Area(AttackAreaShape.Rectangle, Origin + Vector3.Cross(Vector3.up, aim) * Spec.sideOffset * (i % 2 == 0 ? -1f : 1f), aim);
        // 第 i 條（0 起算）從出招開始算的落下時間
        float StrikeTime(int i) => i * Spec.interval;
        public override void BeginTelegraph()
        {
            base.BeginTelegraph(); aim = Aim;
            int last = Spec.count - 1;
            retargetIndex = Spec.trackLast && last > 0 && Spec.warning > 0f && StrikeTime(last) >= Spec.warning ? last : -1;
            for (int i = 0; i < Spec.count; i++)
                if (i != retargetIndex) Spawn(Line(i), TelegraphTime + StrikeTime(i), false);
        }
        public override bool TickActive(float dt)
        {
            timer += dt;
            if (retargetIndex >= 0 && timer >= StrikeTime(retargetIndex) - Spec.warning)
            {
                aim = Aim;
                Spawn(Line(retargetIndex), Spec.warning);
                retargetIndex = -1;
            }
            return retargetIndex < 0 && AllResolved;
        }
    }
}
