using BossFight.Hazards;
using UnityEngine;
namespace BossFight.Boss.Moves
{
    /// <summary>
    /// 萬根五波，依已確認方案 A；本類不改血量、失衡或玩家位置，由上層持有無敵與流程。
    /// 第五波的安全圈找不到時逐步放寬（序章修正 C11），一定會放出這一波、招式一定會結束，只留 Warning：
    /// 玩家附近那一圈：reachableDistance 內可走到 → 整個場地內可走到 → 不檢查步行路徑 → 夾回場內的玩家位置；
    /// 其他圈：不碰障礙且彼此分開 → 不要求分開 → 隨便一個場內位置。
    /// </summary>
    public class RootBarrageMove : GuardianMove
    {
        int wave, chase;
        bool sampled, finalSpawned;
        Vector3 sample;
        public override float TelegraphTime => Spec.outerWarning;
        const int PlacementAttempts = 512;
        Vector3 NearSafeCircle()
        {
            if (PickReachable(Spec.safeRadius, Spec.reachableDistance, out Vector3 p)) return p;
            Debug.LogWarning("[Guardian] 玩家 " + Spec.reachableDistance + " m 內找不到可走到的安全圈，放寬到整個場地。", this);
            if (PickReachable(Spec.safeRadius, FieldRadius * 2f, out p)) return p;
            Debug.LogWarning("[Guardian] 整個場地都找不到可走到的安全圈，改成不檢查步行路徑；請核對場地碰撞。", this);
            if (PickReachable(Spec.safeRadius, FieldRadius * 2f, out p, false)) return p;
            Debug.LogWarning("[Guardian] 找不到不碰障礙的安全圈，直接放在玩家位置；請核對場地碰撞。", this);
            return ClampPoint(PlayerPoint, Spec.safeRadius);
        }
        Vector3 OtherSafeCircle(Vector3[] centers, int count)
        {
            for (int pass = 0; pass < 2; pass++)
            {
                bool separate = pass == 0;
                for (int attempt = 0; attempt < PlacementAttempts; attempt++)
                {
                    Vector3 p = RandomFieldPoint(Spec.safeRadius);
                    if (!ValidCircle(p, Spec.safeRadius, p, 0f)) continue;
                    bool apart = true;
                    for (int j = 0; j < count && separate; j++) apart &= Flat(p - centers[j]).magnitude >= Spec.safeRadius * 2f;
                    if (apart) return p;
                }
                Debug.LogWarning(separate ? "[Guardian] 安全圈排不開，改成不要求彼此分開。" : "[Guardian] 找不到不碰障礙的安全圈位置，隨便放一個場內位置；請核對場地碰撞。", this);
            }
            return RandomFieldPoint(Spec.safeRadius);
        }
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
                var centers = new Vector3[Spec.safeCount];
                centers[0] = NearSafeCircle();
                for (int i = 1; i < centers.Length; i++) centers[i] = OtherSafeCircle(centers, i);
                // 全場的填色由守衛往外推
                var a = Area(AttackAreaShape.Field, Origin, Vector3.forward); a.safeCenters = centers; a.safeRadius = Spec.safeRadius;
                Spawn(a, Spec.finalWarning); finalSpawned = true; return false;
            }
            return finalSpawned && AllResolved;
        }
    }
}
