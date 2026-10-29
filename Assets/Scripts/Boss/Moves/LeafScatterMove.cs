using System.Collections.Generic;
using BossFight.Hazards;
using UnityEngine;
namespace BossFight.Boss.Moves
{
    /// <summary>
    /// 扇形葉刃（序章修正 C5）：預兆是整個扇形的淡底色，加上每道葉刃一支短箭頭（previewLength）依序填滿；
    /// 二階段的第二波在招式預兆時就一起畫出（錯開 angleOffset 度），晚 interval 秒才填滿，所以兩波看得出差別。
    /// 出手後葉片與地上的小框一起往前飛，掃掠判定照舊。
    /// </summary>
    public class LeafScatterMove : GuardianMove
    {
        Vector3 aim;
        // 每一波的預兆（扇形底色與箭頭），那一波出手時收掉
        readonly List<List<AreaStrike>> previews = new List<List<AreaStrike>>();
        float SpreadAngle(int wave) => wave * Spec.angleOffset;
        Vector3 Direction(int i, int wave) => Quaternion.Euler(0f, (Spec.count > 1 ? -Spec.angle * 0.5f + Spec.angle * i / (Spec.count - 1) : 0f) + SpreadAngle(wave), 0f) * aim;
        void Preview(int wave, float duration)
        {
            var list = new List<AreaStrike>();
            if (wave == 0)
            {
                // 整個扇形的底色（含第二波錯開的角度）：預兆時間給無限大，填色停在 0，只有外框與底色；第一波出手時收掉
                var fan = Area(AttackAreaShape.Sector, Origin, aim);
                fan.radius = MaximumRange;
                fan.angle = Spec.angle + Mathf.Abs(Spec.angleOffset) * (Spec.waves - 1);
                list.Add(Spawn(fan, float.PositiveInfinity, false, true));
            }
            for (int i = 0; i < Spec.count; i++)
            {
                var arrow = Area(AttackAreaShape.Rectangle, Origin, Direction(i, wave));
                arrow.length = Spec.previewLength;
                list.Add(Spawn(arrow, duration, false, true));
            }
            previews.Add(list);
        }
        public override void BeginTelegraph()
        {
            base.BeginTelegraph(); aim = Aim; previews.Clear();
            for (int wave = 0; wave < Spec.waves; wave++) Preview(wave, TelegraphTime + wave * Spec.interval);
        }
        public override void BeginActive() => Launch();
        void Launch()
        {
            if (index < previews.Count) foreach (var p in previews[index]) if (p != null) p.Despawn();
            for (int i = 0; i < Spec.count; i++)
            {
                var a = Area(AttackAreaShape.Rectangle, Origin, Direction(i, index));
                var leaf = Spawn(a, 0f, false); leaf.Fly(Spec.speed, MaximumRange); leaf.SetProjectileVisual(Spec.centerHeight);
            }
            index++;
        }
        public override bool TickActive(float dt)
        {
            timer += dt;
            if (index < Spec.waves && timer >= index * Spec.interval) Launch();
            return index >= Spec.waves;
        }
    }
}
