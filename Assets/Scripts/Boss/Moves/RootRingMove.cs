using System.Collections.Generic;
using BossFight.Hazards;
using UnityEngine;
namespace BossFight.Boss.Moves
{
    /// <summary>
    /// 以守衛為中心的根波；環帶與判定一起擴散到真正場界。
    /// 預兆（序章修正 C6）：守衛腳下的起始圈依序填滿，同時淡淡畫出整條路徑（起始圈到場界）；
    /// 二階段往內收的那一圈，預兆期間場地邊緣先亮一圈。每道環波前方都有一段預警帶（leadWidth）。
    /// </summary>
    public class RootRingMove : GuardianMove
    {
        readonly List<AreaStrike> previews = new List<AreaStrike>();
        AreaStrike edgePreview;
        bool HasInward => Spec.reverseSecond && Spec.count > 1;
        public override void BeginTelegraph()
        {
            base.BeginTelegraph(); previews.Clear(); edgePreview = null;
            var start = Area(AttackAreaShape.Circle, Origin, Vector3.forward); start.radius = Spec.innerRadius;
            previews.Add(Spawn(start, TelegraphTime, false, true));
            // 整條路徑：預兆時間給無限大，填色停在 0，只有外框與底色
            var path = Area(AttackAreaShape.Ring, Origin, Vector3.forward); path.radius = MaximumRange; path.innerRadius = Spec.innerRadius;
            previews.Add(Spawn(path, float.PositiveInfinity, false, true));
            if (HasInward)
            {
                // 往內收的那一圈從場地外圍進來：場地邊緣一圈，到它出發時填滿
                var edge = Area(AttackAreaShape.Ring, Boss.ArenaCenter, Vector3.forward); edge.radius = FieldRadius; edge.innerRadius = Mathf.Max(0f, FieldRadius - Spec.leadWidth);
                edgePreview = Spawn(edge, TelegraphTime + Spec.interval, false, true);
            }
        }
        void Launch()
        {
            var a = Area(AttackAreaShape.Ring, Origin, Vector3.forward);
            bool inward = Spec.reverseSecond && index == 1;
            a.radius = inward ? MaximumRange : Spec.innerRadius + Spec.width;
            a.innerRadius = a.radius - Spec.width;
            var ring = Spawn(a, 0f, false);
            ring.ShowLead(Spec.leadWidth);
            if (inward)
            {
                ring.Contract(Spec.speed, Spec.innerRadius + Spec.width);
                if (edgePreview != null) edgePreview.Despawn();
                edgePreview = null;
            }
            else ring.Expand(Spec.speed, MaximumRange);
            index++;
        }
        public override void BeginActive()
        {
            foreach (var p in previews) if (p != null) p.Despawn();
            previews.Clear();
            Launch();
            if (Spec.count > 1) Telegraph(Data.canBlock, Spec.interval);
        }
        public override bool TickActive(float dt)
        {
            timer += dt;
            if (index < Spec.count && timer >= Spec.interval) { Launch(); timer = 0f; if (index < Spec.count) Telegraph(Data.canBlock, Spec.interval); }
            return index >= Spec.count;
        }
    }
}
