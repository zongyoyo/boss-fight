using BossFight.Core;
using BossFight.Hazards;
using UnityEngine;

namespace BossFight.Boss.Moves
{
    /// <summary>
    /// 扭屁股飛撲（第一階段，中距離）：預兆伏低扭屁股（白光），地上的落點圈跟著玩家；起跳時鎖定落點，
    /// 拋物線飛 flightTime 秒，落地瞬間打一下圓柱判定，同時落點圈閃亮、出現衝擊環。落點圈在落地那一刻剛好填滿。
    /// 落點停在玩家前方 landShort 公尺，不會走出場地。可擋、可盾反。數值讀 ButtPounceData。
    /// </summary>
    [DisallowMultipleComponent]
    public class ButtPounceMove : BossMove
    {
        [Tooltip("攻擊判定，attacker 指向 Boss 根物件")]
        [SerializeField] Hitbox hitbox;
        [SerializeField] OiiaCatPoser poser;
        [Tooltip("落點圈（執行時生成一份）")]
        [SerializeField] GroundTelegraph telegraphPrefab;
        [Tooltip("落地的衝擊環")]
        [SerializeField] TimedFx impactPrefab;
        [SerializeField] [ColorUsage(true, true)] Color impactColor = new Color(2.6f, 2.6f, 2.6f, 1f);
        [Tooltip("落地後伏低的程度（0..1）")]
        [Range(0f, 1f)] [SerializeField] float landingCrouch = 0.6f;
        [Tooltip("破綻時伏低的程度（0..1）")]
        [Range(0f, 1f)] [SerializeField] float recoveryCrouch = 0.3f;

        ButtPounceData pounce;
        GroundTelegraph circle;
        Vector3 start, land;
        float timer, flight, hitTimer;
        bool landed;

        // 落點圈從預兆開始填，落地那一刻填滿
        float LeadTime => TelegraphTime + pounce.flightTime;

        void Awake()
        {
            pounce = Data as ButtPounceData;
            if (pounce == null) Debug.LogError($"[Boss] {name} 的資料不是 ButtPounceData。", this);
            circle = Instantiate(telegraphPrefab);
            circle.name = $"{name}_Telegraph";
        }

        void OnDestroy()
        {
            if (circle != null) Destroy(circle.gameObject);
        }

        void OnEnable() => hitbox.OnHit += ReportHit;

        void OnDisable() => hitbox.OnHit -= ReportHit;

        public override void BeginTelegraph()
        {
            timer = 0f;
            poser.SetCrouch(1f);
            poser.SetWiggle(true);
            land = LandingPoint();
            circle.ShowCircle(land, pounce.landingRadius, Data.canBlock);
        }

        public override void TickTelegraph(float dt)
        {
            base.TickTelegraph(dt);
            timer += dt;
            land = LandingPoint();
            circle.MoveCircle(land, pounce.landingRadius);
            circle.SetFill(timer / LeadTime);
        }

        public override void BeginActive()
        {
            start = OnGround(Boss.transform.position);
            land = LandingPoint();
            circle.MoveCircle(land, pounce.landingRadius);
            flight = hitTimer = 0f;
            landed = false;
            Vector3 to = Flat(land - start);
            if (to.sqrMagnitude > 1e-4f) Boss.transform.rotation = Quaternion.LookRotation(to);
            poser.SetWiggle(false);
            poser.SetCrouch(0f);
        }

        public override bool TickActive(float dt)
        {
            if (!landed)
            {
                flight += dt;
                float t = Mathf.Clamp01(flight / pounce.flightTime);
                Boss.transform.position = Vector3.Lerp(start, land, t) + Vector3.up * (4f * pounce.leapHeight * t * (1f - t));
                circle.SetFill((TelegraphTime + flight) / LeadTime);
                if (t >= 1f) Land();
                return false;
            }

            hitTimer += dt;
            if (hitTimer < pounce.hitWindow) return false;
            hitbox.Deactivate();
            return true;
        }

        void Land()
        {
            landed = true;
            Boss.transform.position = land;
            hitbox.SetCylinder(new Vector3(0f, pounce.hitHeight * 0.5f, 0f), pounce.landingRadius, pounce.hitHeight);
            hitbox.Activate(CreateDamage());
            circle.SetFill(1f);
            circle.Strike();
            circle.Hide();
            poser.SetCrouch(landingCrouch);
            TimedFx.Spawn(impactPrefab, land, Quaternion.identity, impactColor, pounce.landingRadius);
        }

        public override void BeginRecovery()
        {
            hitbox.Deactivate();
            poser.SetCrouch(recoveryCrouch);
        }

        public override void End()
        {
            hitbox.Deactivate();
            circle.HideImmediate();
            poser.ResetPose();
            // 在空中被打斷（頭暈、轉階段）就直接落回地面
            Boss.transform.position = OnGround(Boss.transform.position);
        }

        // 玩家前方 landShort 公尺；玩家比這更近就原地跳
        Vector3 LandingPoint()
        {
            Vector3 me = OnGround(Boss.transform.position);
            if (Boss.Target == null) return me;
            Vector3 to = Flat(Boss.Target.position - me);
            float distance = to.magnitude;
            if (distance < 1e-3f) return me;
            return Boss.ClampToArena(me + to / distance * Mathf.Max(0f, distance - pounce.landShort));
        }
    }
}
