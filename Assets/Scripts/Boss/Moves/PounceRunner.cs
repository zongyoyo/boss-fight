using BossFight.Core;
using BossFight.Hazards;
using UnityEngine;

namespace BossFight.Boss.Moves
{
    /// <summary>
    /// 一次扭屁股飛撲：伏低扭屁股（地上的落點圈跟著玩家、慢慢填滿）→ 起跳時鎖定落點，拋物線飛 flightTime 秒 → 落地瞬間打一下圓柱判定 hitWindow 秒，
    /// 同時落點圈閃亮、出現衝擊環。落點圈在落地那一刻剛好填滿。落點停在玩家前方 landShort 公尺，不會走出場地。
    /// 扭屁股飛撲（Boss 本體）與幻影分身（本體與每隻幻影各一個）共用：飛的是 body，姿勢用 poser，判定用 hitbox，落點圈是自己的一份 GroundTelegraph。
    /// 數值讀 ButtPounceData；轉向由呼叫方處理。
    /// </summary>
    public class PounceRunner
    {
        public enum Stage
        {
            /// <summary>還沒開始，或已經收掉。</summary>
            Idle,
            /// <summary>伏低扭屁股。</summary>
            Windup,
            /// <summary>飛行中。</summary>
            Flight,
            /// <summary>落地判定中。</summary>
            Landed,
            /// <summary>落地判定結束。</summary>
            Done,
        }

        readonly BossController boss;
        readonly Transform body;
        readonly OiiaCatPoser poser;
        readonly Hitbox hitbox;
        readonly GroundTelegraph circle;
        readonly TimedFx impactPrefab;
        readonly Color impactColor;
        readonly float landingCrouch;
        readonly BossImpactKind landingImpact;

        ButtPounceData data;
        DamageInfo damage;
        Vector3 start, land;
        float windupTime, timer, flight, hitTimer;

        public Stage State { get; private set; }
        /// <summary>目前的落點（地面高度）。</summary>
        public Vector3 LandingPoint => land;
        public Transform Body => body;

        /// <param name="landingCrouch">落地後伏低的程度（0..1）。</param>
        /// <param name="landingImpact">落地時送出的衝擊（BossController.OnMoveImpact）：本體 PounceLanding、幻影 PhantomLanding。</param>
        public PounceRunner(BossController boss, Transform body, OiiaCatPoser poser, Hitbox hitbox, GroundTelegraph circle,
                            TimedFx impactPrefab, Color impactColor, float landingCrouch, BossImpactKind landingImpact)
        {
            this.boss = boss;
            this.body = body;
            this.poser = poser;
            this.hitbox = hitbox;
            this.circle = circle;
            this.impactPrefab = impactPrefab;
            this.impactColor = impactColor;
            this.landingCrouch = landingCrouch;
            this.landingImpact = landingImpact;
        }

        // 落點圈從伏低開始填，落地那一刻填滿
        float LeadTime => windupTime + data.flightTime;

        /// <summary>開始伏低扭屁股；windup 秒後要呼叫 Leap。落點圈可擋白、不可擋紅。</summary>
        public void BeginWindup(ButtPounceData pounce, float windup, bool blockable)
        {
            data = pounce;
            windupTime = windup;
            timer = 0f;
            State = Stage.Windup;
            poser.SetCrouch(1f);
            poser.SetWiggle(true);
            land = ComputeLanding();
            circle.ShowCircle(land, data.landingRadius, blockable);
            circle.SetFill(0f);
        }

        /// <summary>伏低中每幀呼叫：落點圈跟著玩家。</summary>
        public void TickWindup(float dt)
        {
            if (State != Stage.Windup) return;
            timer += dt;
            land = ComputeLanding();
            circle.MoveCircle(land, data.landingRadius);
            circle.SetFill(timer / LeadTime);
        }

        /// <summary>起跳：鎖定落點、面向落點，落地時打出 info。</summary>
        public void Leap(in DamageInfo info)
        {
            damage = info;
            start = OnGround(body.position);
            land = ComputeLanding();
            circle.MoveCircle(land, data.landingRadius);
            flight = hitTimer = 0f;
            Vector3 to = Flat(land - start);
            if (to.sqrMagnitude > 1e-4f) body.rotation = Quaternion.LookRotation(to);
            poser.SetWiggle(false);
            poser.SetCrouch(0f);
            State = Stage.Flight;
        }

        /// <summary>飛行與落地判定中每幀呼叫。</summary>
        /// <returns>落地判定結束（或已經結束）。</returns>
        public bool Tick(float dt)
        {
            switch (State)
            {
                case Stage.Flight:
                    flight += dt;
                    float t = Mathf.Clamp01(flight / data.flightTime);
                    body.position = Vector3.Lerp(start, land, t) + Vector3.up * (4f * data.leapHeight * t * (1f - t));
                    circle.SetFill((windupTime + flight) / LeadTime);
                    if (t >= 1f) Land();
                    return false;
                case Stage.Landed:
                    hitTimer += dt;
                    if (hitTimer < data.hitWindow) return false;
                    hitbox.Deactivate();
                    State = Stage.Done;
                    return true;
                default:
                    return State == Stage.Done;
            }
        }

        void Land()
        {
            State = Stage.Landed;
            body.position = land;
            hitbox.SetCylinder(new Vector3(0f, data.hitHeight * 0.5f, 0f), data.landingRadius, data.hitHeight);
            hitbox.Activate(damage);
            circle.SetFill(1f);
            circle.Strike();
            circle.Hide();
            poser.SetCrouch(landingCrouch);
            TimedFx.Spawn(impactPrefab, land, Quaternion.identity, impactColor, data.landingRadius);
            boss.NotifyImpact(landingImpact, land);
        }

        /// <summary>收掉：關判定、藏起落點圈、回到坐姿；在空中被打斷就直接落回地面。</summary>
        public void Stop()
        {
            hitbox.Deactivate();
            circle.HideImmediate();
            poser.ResetPose();
            body.position = OnGround(body.position);
            State = Stage.Idle;
        }

        // 玩家前方 landShort 公尺；玩家比這更近就原地跳
        Vector3 ComputeLanding()
        {
            Vector3 me = OnGround(body.position);
            if (boss.Target == null) return me;
            Vector3 to = Flat(boss.Target.position - me);
            float distance = to.magnitude;
            if (distance < 1e-3f) return me;
            return boss.ClampToArena(me + to / distance * Mathf.Max(0f, distance - data.landShort));
        }

        Vector3 OnGround(Vector3 v) => new Vector3(v.x, boss.ArenaCenter.y, v.z);

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
