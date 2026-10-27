using System.Collections.Generic;
using BossFight.Core;
using BossFight.Hazards;
using UnityEngine;

namespace BossFight.Boss.Moves
{
    /// <summary>守衛招式的共用生成與幾何；只依賴 Boss／Hazards／Core，不引用玩家或上層流程。</summary>
    public abstract class GuardianMove : BossMove
    {
        [SerializeField] AreaStrike areaPrefab;
        [Tooltip("守衛實體、樹幹與外牆；Builder 收集，安全圈與步行路徑使用")]
        [SerializeField] Collider[] obstacles;
        [Tooltip("安全圈搜尋的純效能取樣密度")]
        [Min(8), SerializeField] int searchDirections = 128;
        protected GuardianMoveData Spec => (GuardianMoveData)Data;
        protected readonly List<AreaStrike> live = new List<AreaStrike>();
        readonly RaycastHit[] pathHits = new RaycastHit[32];
        protected float timer;
        protected int index;
        protected Vector3 Origin => OnGround(Boss.transform.position);
        protected Vector3 PlayerPoint => Boss.Target != null ? OnGround(Boss.Target.position) : Origin;
        protected float FieldRadius => Boss.Arena.radius;
        protected float MaximumRange => FieldRadius + Flat(Origin - Boss.ArenaCenter).magnitude;
        protected AttackArea Area(AttackAreaShape shape, Vector3 point, Vector3 direction)
        {
            return new AttackArea { shape = shape, center = point, forward = direction,
                radius = Spec.radius, innerRadius = Spec.innerRadius, angle = Spec.angle,
                length = Spec.reach, width = Spec.width, height = Spec.hitHeight,
                fieldCenter = Boss.ArenaCenter, fieldRadius = FieldRadius };
        }
        protected AreaStrike Spawn(AttackArea a, float warning, bool notify = true, bool preview = false)
        {
            if (notify) Telegraph(Data.canBlock, warning);
            var strike = SpawnHazard(areaPrefab, Boss.ArenaCenter, Quaternion.identity);
            live.Add(strike);
            strike.OnStrike += ImpactArea;
            strike.OnAvoided += Avoided;
            strike.Begin(a, CreateDamage(), warning, Spec.hitWindow, Boss.TargetHurtbox,
                () => Boss.TargetDodgeEligible != null ? Boss.TargetDodgeEligible() : Boss.TargetRolling, preview, Spec.visualStyle);
            return strike;
        }
        void ImpactArea(AreaStrike strike) => Impact(BossImpactKind.PawSwipe, strike.Area.center);
        void Avoided(Vector3 position) => Boss.NotifyAttackAvoided(position);
        protected bool AllResolved
        {
            get { foreach (var a in live) if (a != null && !a.Resolved) return false; return true; }
        }
        public override void BeginTelegraph() { timer = 0f; index = 0; live.Clear(); }
        public override void TickTelegraph(float dt) => Boss.FaceTarget(dt);
        public override void End() { }
        protected Vector3 Aim => Flat(PlayerPoint - Origin).sqrMagnitude > 0f ? Flat(PlayerPoint - Origin).normalized : Boss.transform.forward;
        protected void ClearPreviews() { foreach (var a in live) if (a != null) a.Despawn(); live.Clear(); }
        protected Vector3 ClampPoint(Vector3 p, float margin)
        {
            Vector3 v = Flat(p - Boss.ArenaCenter);
            float r = Mathf.Max(0f, FieldRadius - margin);
            if (v.magnitude > r) v = v.normalized * r;
            return OnGround(Boss.ArenaCenter + v);
        }
        protected bool ValidCircle(Vector3 p, float radius, Vector3 from, float walkerRadius)
        {
            if (Flat(p - Boss.ArenaCenter).magnitude + radius > FieldRadius) return false;
            foreach (var c in obstacles)
            {
                if (c == null || !c.enabled || c.isTrigger) continue;
                Bounds b = c.bounds;
                if (b.max.y <= GroundY || b.min.y > GroundY + Spec.hitHeight) continue;
                Vector3 q = c.ClosestPoint(p + Vector3.up * Mathf.Min(Spec.hitHeight * 0.5f, b.extents.y));
                if (Flat(q - p).sqrMagnitude < radius * radius) return false;
            }
            Vector3 delta = p - from;
            if (walkerRadius > 0f && delta.sqrMagnitude > 0f)
            {
                float targetHeight = Boss.TargetHurtbox != null ? Boss.TargetHurtbox.bounds.size.y : Spec.hitHeight;
                Vector3 bottom = from + Vector3.up * walkerRadius;
                Vector3 top = from + Vector3.up * Mathf.Max(walkerRadius, targetHeight - walkerRadius);
                int hits = Physics.CapsuleCastNonAlloc(bottom, top, walkerRadius, delta.normalized, pathHits, delta.magnitude, ~0, QueryTriggerInteraction.Ignore);
                if (hits >= pathHits.Length) return false;
                for (int i = 0; i < hits; i++)
                    if (System.Array.IndexOf(obstacles, pathHits[i].collider) >= 0) return false;
            }
            return true;
        }
        protected bool PickReachable(float radius, float distance, out Vector3 result)
        {
            Vector3 from = PlayerPoint;
            float walkerRadius = Boss.TargetBodyRadius;
            for (int ring = 0; ring <= 16; ring++)
                for (int i = 0; i < searchDirections; i++)
                {
                    Vector3 p = from + Quaternion.Euler(0f, i * 360f / searchDirections, 0f) * Vector3.forward * (distance * ring / 16f);
                    if (ValidCircle(p, radius, from, walkerRadius)) { result = p; return true; }
                }
            result = from; return false;
        }
        protected Vector3 RandomFieldPoint(float margin)
        {
            Vector2 p = Random.insideUnitCircle * FieldRadius;
            return ClampPoint(Boss.ArenaCenter + new Vector3(p.x, 0f, p.y), margin);
        }
    }
}
