using BossFight.Core;
using BossFight.Hazards;
using UnityEngine;

namespace BossFight.Boss.Moves
{
    /// <summary>
    /// OIIA 迴旋衝撞（第一階段，中–遠距離）：預兆原地加速旋轉，地上從 Boss 到外牆的衝刺路徑線慢慢填滿；
    /// 前 trackTime 秒路徑跟著玩家轉，之後鎖定。填滿那一刻沿路徑直線衝刺，判定是 Boss 周圍的圓柱，穿過玩家不停、整段只打一次。
    /// 撞牆用 ArenaData 的 ArenaWall Layer 做 SphereCast（入口的門與門柱也算）；衝出場地半徑也當成撞牆。
    /// 撞牆時出現衝擊環、Boss 停轉搖晃，硬直 recoveryTime。可擋但耗耐力大（格擋耐力倍率），不可盾反。數值讀 SpinChargeData。
    /// </summary>
    [DisallowMultipleComponent]
    public class SpinChargeMove : BossMove
    {
        [Tooltip("攻擊判定，attacker 指向 Boss 根物件")]
        [SerializeField] Hitbox hitbox;
        [SerializeField] OiiaCatPoser poser;
        [Tooltip("衝刺路徑線（執行時生成一份）")]
        [SerializeField] GroundTelegraph telegraphPrefab;
        [Tooltip("撞牆的衝擊環")]
        [SerializeField] TimedFx impactPrefab;
        [SerializeField] [ColorUsage(true, true)] Color impactColor = new Color(3f, 2.4f, 1.6f, 1f);
        [Tooltip("衝擊環大小（倍率）")]
        [Min(0f)] [SerializeField] float impactSize = 2.5f;
        [Tooltip("預兆結束時的轉速（度/秒），從預兆開始時的轉速（平常是 0；開場結束時貓已經在轉）加速上去")]
        [SerializeField] float spinSpeed = 1440f;

        SpinChargeData charge;
        GroundTelegraph path;
        Vector3 direction;
        float timer, startSpin;
        bool hitWall;

        void Awake()
        {
            charge = Data as SpinChargeData;
            if (charge == null) Debug.LogError($"[Boss] {name} 的資料不是 SpinChargeData。", this);
            path = Instantiate(telegraphPrefab);
            path.name = $"{name}_Path";
        }

        void OnDestroy()
        {
            if (path != null) Destroy(path.gameObject);
        }

        void OnEnable() => hitbox.OnHit += ReportHit;

        void OnDisable() => hitbox.OnHit -= ReportHit;

        public override void BeginTelegraph()
        {
            timer = 0f;
            hitWall = false;
            startSpin = poser.SpinSpeed; // 開場的貓直接從旋轉接這一招，不先停下來
            Vector3 dir = Flat(Boss.transform.forward).normalized;
            path.ShowRect(OnGround(Boss.transform.position), dir, PathLength(dir), charge.pathWidth, Data.canBlock);
        }

        public override void TickTelegraph(float dt)
        {
            timer += dt;
            float t = TelegraphTime > 0f ? Mathf.Clamp01(timer / TelegraphTime) : 1f;
            poser.SetSpinImmediate(Mathf.Lerp(startSpin, spinSpeed, t));

            // 前 trackTime 秒跟著玩家轉，之後路徑鎖定不動
            if (timer < charge.trackTime)
            {
                Boss.FaceTarget(dt);
                Vector3 dir = Flat(Boss.transform.forward).normalized;
                path.MoveRect(OnGround(Boss.transform.position), dir, PathLength(dir), charge.pathWidth);
            }
            path.SetFill(t);
        }

        public override void BeginActive()
        {
            timer = 0f;
            direction = Flat(Boss.transform.forward).normalized;
            hitbox.SetCylinder(new Vector3(0f, charge.hitHeight * 0.5f, 0f), charge.hitRadius, charge.hitHeight);
            hitbox.Activate(CreateDamage().WithBlockStaminaMultiplier(charge.blockStaminaMultiplier));
            path.SetFill(1f);
            path.Strike();
            Boss.SetBodyCollision(false); // 穿過玩家，不把玩家推著走
        }

        public override bool TickActive(float dt)
        {
            timer += dt;
            Vector3 pos = Boss.transform.position;
            float step = charge.chargeSpeed * dt;
            float radius = Boss.BodyRadius;

            // 撞到外牆就停在牆前
            var arena = Boss.Arena;
            if (arena != null && Physics.SphereCast(pos + Vector3.up * CastHeight, radius, direction, out var hit, step,
                                                    arena.WallMask, QueryTriggerInteraction.Ignore))
            {
                Boss.transform.position = pos + direction * hit.distance;
                HitWall(OnGround(hit.point));
                return true;
            }

            // 保險：衝出場地半徑也當成撞牆（例如一開始就貼著牆，SphereCast 掃不到）
            Vector3 next = pos + direction * step;
            Vector3 clamped = Boss.ClampToArena(next);
            if (Flat(clamped - next).sqrMagnitude > 1e-6f)
            {
                Boss.transform.position = clamped;
                HitWall(OnGround(clamped + direction * radius));
                return true;
            }

            Boss.transform.position = next;
            return timer >= charge.maxChargeTime;
        }

        void HitWall(Vector3 point)
        {
            hitWall = true;
            hitbox.Deactivate();
            Boss.SetBodyCollision(true);
            path.Hide();
            poser.SetSpin(0f);
            poser.SetWobble(true);
            TimedFx.Spawn(impactPrefab, point, Quaternion.identity, impactColor, impactSize);
        }

        public override void BeginRecovery()
        {
            hitbox.Deactivate();
            Boss.SetBodyCollision(true);
            if (hitWall) return;
            path.Hide();
            poser.SetSpin(0f);
        }

        public override void End()
        {
            hitbox.Deactivate();
            Boss.SetBodyCollision(true);
            path.HideImmediate();
            poser.ResetPose();
        }

        float CastHeight => charge.hitHeight * 0.5f;

        // 從 Boss 往 dir 到外牆內側的距離；掃不到牆時用場地半徑算
        float PathLength(Vector3 dir)
        {
            Vector3 pos = Boss.transform.position;
            float radius = Boss.BodyRadius;
            var arena = Boss.Arena;
            if (arena == null) return charge.chargeSpeed * charge.maxChargeTime;

            float maxDistance = arena.radius * 2f;
            if (Physics.SphereCast(pos + Vector3.up * CastHeight, radius, dir, out var hit, maxDistance,
                                   arena.WallMask, QueryTriggerInteraction.Ignore))
                return hit.distance + radius;

            // 射線和場地圓周的交點
            Vector3 offset = Flat(pos - Boss.ArenaCenter);
            float b = Vector3.Dot(dir, offset);
            float c = offset.sqrMagnitude - arena.radius * arena.radius;
            return Mathf.Max(0f, -b + Mathf.Sqrt(Mathf.Max(0f, b * b - c)));
        }
    }
}
