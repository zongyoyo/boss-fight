using BossFight.Hazards;
using UnityEngine;
namespace BossFight.Boss.Moves
{
    /// <summary>萬根五波，依已確認方案 A；本類不改血量、失衡或玩家位置，由上層持有無敵與流程。</summary>
    public class RootBarrageMove : GuardianMove
    {
        int wave, chase;
        bool sampled, finalSpawned;
        Vector3 sample;
        public override float TelegraphTime => Spec.outerWarning;
        public override void BeginTelegraph()
        {
            base.BeginTelegraph(); wave = chase = 0; sampled = finalSpawned = false;
            var a = Area(AttackAreaShape.Ring, Origin, Vector3.forward); a.radius = MaximumRange; a.innerRadius = Spec.reach;
            Spawn(a, Spec.outerWarning, false);
        }
        public override bool TickActive(float dt)
        {
            if (!AllResolved) return false;
            if (wave == 0)
            {
                wave++; var a = Area(AttackAreaShape.Circle, Origin, Vector3.forward); a.radius = Spec.innerRadius;
                Spawn(a, Spec.innerWarning); return false;
            }
            if (wave == 1) { wave++; Spawn(Area(AttackAreaShape.Cross, Origin, Vector3.forward), Spec.crossWarning); return false; }
            if (wave == 2)
            {
                if (chase >= Spec.count) { wave++; return false; }
                if (!sampled) { sample = PlayerPoint; sampled = true; timer = 0f; return false; }
                timer += dt;
                if (timer >= Spec.sampleLead)
                { Spawn(Area(AttackAreaShape.Circle, sample, Vector3.forward), Spec.warning); sampled = false; chase++; }
                return false;
            }
            if (wave == 3 && !finalSpawned)
            {
                if (!PickReachable(Spec.safeRadius, Spec.reachableDistance, out Vector3 near))
                {
                    Debug.LogError("[Guardian] 找不到玩家 2m 內完整且可步行到達的安全圈；停止戰鬥，請核對場地碰撞。", this);
                    Boss.SuspendCombat(); return false;
                }
                var centers = new Vector3[Spec.safeCount]; centers[0] = near;
                for (int i = 1; i < centers.Length; i++)
                {
                    bool found = false;
                    for (int attempt = 0; attempt < 512; attempt++)
                    {
                        Vector3 p = RandomFieldPoint(Spec.safeRadius);
                        if (!ValidCircle(p, Spec.safeRadius, p, 0f)) continue;
                        bool separate = true;
                        for (int j = 0; j < i; j++) separate &= Flat(p - centers[j]).magnitude >= Spec.safeRadius * 2f;
                        if (!separate) continue;
                        centers[i] = p; found = true; break;
                    }
                    if (!found) { Debug.LogError("[Guardian] 安全圈排法失敗，停止戰鬥。", this); Boss.SuspendCombat(); return false; }
                }
                var a = Area(AttackAreaShape.Field, Boss.ArenaCenter, Vector3.forward); a.safeCenters = centers; a.safeRadius = Spec.safeRadius;
                Spawn(a, Spec.finalWarning); finalSpawned = true; return false;
            }
            return finalSpawned && AllResolved;
        }
    }
}
