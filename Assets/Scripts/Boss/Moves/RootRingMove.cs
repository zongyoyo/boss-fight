using BossFight.Hazards;
using UnityEngine;
namespace BossFight.Boss.Moves
{
    /// <summary>
    /// 以守衛為中心的根波；環帶與判定一起擴散到真正場界。
    /// 根刺（試玩回饋）：每一圈是 spikeCount 根刺，判定只算刺（AttackArea 的分段），刺與刺之間的空隙打不到；
    /// 刺的角度整招固定，相鄰兩圈錯開 ringStagger 格（下一圈的刺在上一圈兩根刺中間）。
    /// 預兆（序章修正 C6）：守衛腳下的起始圈依序填滿；每一圈的刺道（從起始圈到場界、只畫刺會走的線）淡淡畫出來，
    /// 第一圈在預兆時、之後每一圈在前一圈出發時畫，出發時收掉。二階段往內收的那一圈也一樣，它的刺道一路畫到場地邊緣。
    /// 每道環波前方都有一段預警帶（leadWidth），也只畫在刺上。
    /// </summary>
    public class RootRingMove : GuardianMove
    {
        AreaStrike lanes, startCircle;
        RootRingData Ring => (RootRingData)Data;
        // 第 ring 圈第一根刺的角度（度，0 = 北、順時針）
        float Phase(int ring) => ring % 2 == 1 ? Ring.ringStagger * 360f / Ring.spikeCount : 0f;
        AttackArea RingArea(int ring)
        {
            var a = Area(AttackAreaShape.Ring, Origin, Vector3.forward);
            a.segments = Ring.spikeCount; a.segmentPhase = Phase(ring); a.segmentHalfWidth = Ring.spikeRadius;
            return a;
        }
        // 第 ring 圈的刺道：預兆時間給無限大，填色停在 0，只有外框與底色
        void ShowLanes(int ring)
        {
            if (lanes != null) lanes.Despawn();
            lanes = null;
            if (ring >= Spec.count) return;
            var path = RingArea(ring); path.radius = MaximumRange; path.innerRadius = Spec.innerRadius;
            lanes = Spawn(path, float.PositiveInfinity, false, true);
        }
        public override void BeginTelegraph()
        {
            base.BeginTelegraph(); lanes = null;
            var start = Area(AttackAreaShape.Circle, Origin, Vector3.forward); start.radius = Spec.innerRadius;
            startCircle = Spawn(start, TelegraphTime, false, true);
            ShowLanes(0);
        }
        void Launch()
        {
            var a = RingArea(index);
            bool inward = Spec.reverseSecond && index == 1;
            a.radius = inward ? MaximumRange : Spec.innerRadius + Spec.width;
            a.innerRadius = a.radius - Spec.width;
            var ring = Spawn(a, 0f, false);
            ring.ShowLead(Spec.leadWidth);
            if (inward) ring.Contract(Spec.speed, Spec.innerRadius + Spec.width);
            else ring.Expand(Spec.speed, MaximumRange);
            index++;
            ShowLanes(index);
        }
        public override void BeginActive()
        {
            // 收掉起始圈（預兆用的）；刺道由 Launch 換成下一圈的
            if (startCircle != null) startCircle.Despawn();
            startCircle = null;
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
