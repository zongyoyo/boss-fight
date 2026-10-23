using BossFight.Hazards;
using UnityEngine;

namespace BossFight.Boss.Moves
{
    /// <summary>
    /// OIIA 龍捲風（第二階段新招）：預兆原地加速旋轉（紅光），身上長出漩渦（Hazards/TornadoVortex），地上的紅圈填滿；
    /// 出招時以 chaseSpeed（略慢於玩家）直線追蹤玩家 duration 秒，漩渦每 tickInterval 秒打一次、不可擋（跳不過，翻滾的無敵可以閃）。
    /// 追蹤期間關掉本體碰撞，貓會穿過玩家，不會把玩家推著走。結束時漩渦散掉、貓停轉搖晃，硬直 recoveryTime 秒，
    /// 硬直一開始用 AddImbalance 加 endImbalance（加滿就直接頭暈，硬直被打斷）。數值讀 TornadoData。
    /// </summary>
    [DisallowMultipleComponent]
    public class TornadoMove : BossMove
    {
        [SerializeField] OiiaCatPoser poser;
        [SerializeField] TornadoVortex vortexPrefab;
        [Tooltip("預兆結束時的轉速（度/秒）")]
        [SerializeField] float spinSpeed = 1800f;

        TornadoData tornado;
        TornadoVortex vortex;
        float timer, startSpin;

        void Awake()
        {
            tornado = Data as TornadoData;
            if (tornado == null) Debug.LogError($"[Boss] {name} 的資料不是 TornadoData。", this);
        }

        public override void BeginTelegraph()
        {
            timer = 0f;
            startSpin = poser.SpinSpeed;
            Boss.SetBodyCollision(false);
            vortex = SpawnHazard(vortexPrefab, OnGround(Boss.transform.position), Quaternion.identity);
            vortex.Form(Boss.transform, tornado.hitRadius, tornado.hitHeight, TelegraphTime);
        }

        public override void TickTelegraph(float dt)
        {
            base.TickTelegraph(dt);
            timer += dt;
            float t = TelegraphTime > 0f ? Mathf.Clamp01(timer / TelegraphTime) : 1f;
            poser.SetSpinImmediate(Mathf.Lerp(startSpin, spinSpeed, t));
        }

        public override void BeginActive()
        {
            timer = 0f;
            poser.SetSpinImmediate(spinSpeed);
            vortex.Begin(CreateDamage(), tornado.tickInterval);
            Impact(BossImpactKind.Tornado, OnGround(Boss.transform.position));
        }

        public override bool TickActive(float dt)
        {
            timer += dt;
            var target = Boss.Target;
            if (target != null)
            {
                Vector3 to = Flat(target.position - Boss.transform.position);
                float distance = to.magnitude;
                if (distance > 1e-3f)
                {
                    Vector3 dir = to / distance;
                    Boss.transform.position = Boss.ClampToArena(Boss.transform.position + dir * Mathf.Min(tornado.chaseSpeed * dt, distance));
                    Boss.FaceTarget(dt);
                }
            }
            return timer >= tornado.duration;
        }

        public override void BeginRecovery()
        {
            StopVortex();
            Boss.SetBodyCollision(true);
            poser.SetSpin(0f);
            poser.SetWobble(true);
            AddImbalance(tornado.endImbalance); // 加滿會直接頭暈，這招會被 Interrupt
        }

        public override void End()
        {
            StopVortex();
            Boss.SetBodyCollision(true);
            poser.ResetPose();
        }

        void StopVortex()
        {
            if (vortex != null) vortex.Dissipate();
            vortex = null;
        }
    }
}
