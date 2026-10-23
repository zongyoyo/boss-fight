using System.Collections.Generic;
using BossFight.Hazards;
using UnityEngine;

namespace BossFight.Boss.Moves
{
    /// <summary>
    /// OIIA 大迴旋（終極技）：狂暴的進場招式（BossPhaseData.entryMove），血量首次 ≤ 400 時由 BossController 直接出，只會出一次。
    /// 預兆（紅光）：一邊加速旋轉，一邊從目前位置飛到場地中央（法陣）上空 floatHeight。
    /// 出招：放出全場判定（Hazards/GrandSpinField），場上隨機 safeZoneCount 個安全區、魔法陣上那幾塊的符文熄滅；
    /// 頭上每秒跳一個倒數數字，倒數的最後 fallTime 秒砸下來，倒數結束那一刻落地：安全區以外全場 damage，不可擋（跳不過，翻滾的無敵可以閃）。
    /// 落地判定結束後直接頭暈 landingStunTime 秒（BossController.Stun：受傷 ×1.5、可以處決）；頭暈結束後 BossController 才套用狂暴。
    /// 浮空到落地期間 Boss 無敵、關掉本體碰撞。安全區的規則：圓心離場地中心 safeZoneMinRadius–safeZoneMaxRadius，彼此盡量相隔 safeZoneMinSpacing。
    /// 數值讀 GrandSpinData。
    /// </summary>
    [DisallowMultipleComponent]
    public class GrandSpinMove : BossMove
    {
        [SerializeField] OiiaCatPoser poser;
        [SerializeField] GrandSpinField fieldPrefab;
        [Tooltip("安全區熄滅符文的魔法陣（場景物件，可留空）")]
        [SerializeField] MagicCircle magicCircle;
        [Tooltip("落地的衝擊環")]
        [SerializeField] TimedFx impactPrefab;
        [SerializeField] [ColorUsage(true, true)] Color impactColor = new Color(3f, 0.6f, 0.4f, 1f);
        [Min(0f)] [SerializeField] float impactSize = 6f;
        [Tooltip("倒數數字（DamageNumber prefab）與面向的鏡頭")]
        [SerializeField] DamageNumber countdownPrefab;
        [SerializeField] Transform viewCamera;
        [SerializeField] Color countdownColor = new Color(1f, 0.3f, 0.25f, 1f);
        [Min(0f)] [SerializeField] float countdownSize = 3f;
        [Tooltip("倒數數字在 Boss 腳底上方多高 (m)")]
        [SerializeField] float countdownHeight = 3.4f;
        [Tooltip("浮空時的轉速（度/秒）")]
        [SerializeField] float spinSpeed = 2160f;
        [Tooltip("找安全區位置的嘗試次數（純效能參數）")]
        [Min(1)] [SerializeField] int placementAttempts = 16;

        GrandSpinData spin;
        GrandSpinField field;
        readonly List<Vector3> zones = new List<Vector3>();
        Vector3 start, center;
        float timer, hitTimer;
        int shownCount;
        bool landed;

        void Awake()
        {
            spin = Data as GrandSpinData;
            if (spin == null) Debug.LogError($"[Boss] {name} 的資料不是 GrandSpinData。", this);
        }

        float FloatY => GroundY + spin.floatHeight;

        public override void BeginTelegraph()
        {
            timer = 0f;
            landed = false;
            start = Boss.transform.position;
            center = OnGround(Boss.ArenaCenter);
            SetInvulnerable(true);
            Boss.SetBodyCollision(false);
            poser.SetSpin(spinSpeed);
            poser.SetLookUp(1f);
        }

        // 飛到場地中央上空（慢進慢出）
        public override void TickTelegraph(float dt)
        {
            timer += dt;
            float t = TelegraphTime > 0f ? Mathf.Clamp01(timer / TelegraphTime) : 1f;
            float s = t * t * (3f - 2f * t);
            Vector3 p = Vector3.Lerp(start, center, s);
            p.y = Mathf.Lerp(start.y, FloatY, s);
            Boss.transform.position = p;
            Boss.FaceTarget(dt);
        }

        public override void BeginActive()
        {
            timer = 0f;
            shownCount = 0;
            Boss.transform.position = new Vector3(center.x, FloatY, center.z);
            PickZones();
            float fieldRadius = Boss.Arena != null ? Boss.Arena.radius : spin.safeZoneMaxRadius + spin.safeZoneRadius;
            field = SpawnHazard(fieldPrefab, center, Quaternion.identity);
            field.Begin(CreateDamage(), fieldRadius, spin.hitHeight, spin.countdownTime, spin.hitWindow, zones, spin.safeZoneRadius, magicCircle);
        }

        public override bool TickActive(float dt)
        {
            timer += dt;
            if (landed)
            {
                hitTimer += dt;
                return hitTimer >= spin.hitWindow;
            }

            // 每秒跳一個數字：4、3、2、1
            int total = Mathf.CeilToInt(spin.countdownTime);
            while (shownCount < total && timer >= shownCount)
            {
                DamageNumber.Spawn(countdownPrefab, Boss.transform.position + Vector3.up * countdownHeight,
                                   (total - shownCount).ToString(), countdownColor, countdownSize, viewCamera);
                shownCount++;
            }

            Boss.FaceTarget(dt);
            float fallStart = spin.countdownTime - spin.fallTime;
            float u = timer <= fallStart ? 0f : Mathf.Clamp01((timer - fallStart) / spin.fallTime);
            Boss.transform.position = new Vector3(center.x, Mathf.Lerp(FloatY, GroundY, u * u), center.z);
            if (timer >= spin.countdownTime) Land();
            return false;
        }

        void Land()
        {
            landed = true;
            hitTimer = 0f;
            Boss.transform.position = center;
            field.Strike();
            poser.SetSpin(0f);
            poser.SetLookUp(0f);
            TimedFx.Spawn(impactPrefab, center, Quaternion.identity, impactColor, impactSize);
        }

        // 落地判定結束：直接頭暈（BossController 會先 Interrupt 這招，再進入 Stunned）
        public override void BeginRecovery()
        {
            SetInvulnerable(false);
            Boss.SetBodyCollision(true);
            Boss.Stun(spin.landingStunTime);
        }

        /// <summary>落地之後被打斷（落地頭暈）時，全場判定照常跑完（淡出、符文恢復）；還沒落地就被打斷才一起收掉。</summary>
        public override void Interrupt()
        {
            if (landed) End();
            else base.Interrupt();
        }

        public override void End()
        {
            SetInvulnerable(false);
            Boss.SetBodyCollision(true);
            Boss.transform.position = OnGround(Boss.transform.position);
            poser.ResetPose();
            field = null;
        }

        // 圓心離場地中心 min–max，彼此盡量相隔 minSpacing；找不到夠遠的位置時用最後一次的
        void PickZones()
        {
            zones.Clear();
            float maxR = Mathf.Max(spin.safeZoneMinRadius, spin.safeZoneMaxRadius);
            for (int i = 0; i < spin.safeZoneCount; i++)
            {
                Vector3 candidate = center;
                for (int attempt = 0; attempt < placementAttempts; attempt++)
                {
                    float angle = Random.Range(0f, 360f);
                    float distance = Random.Range(spin.safeZoneMinRadius, maxR);
                    candidate = Boss.ClampToArena(center + Quaternion.Euler(0f, angle, 0f) * Vector3.forward * distance, spin.safeZoneRadius);
                    if (FarFromOthers(candidate, spin.safeZoneMinSpacing)) break;
                }
                zones.Add(candidate);
            }
        }

        bool FarFromOthers(Vector3 p, float gap)
        {
            foreach (var q in zones)
            {
                if (Flat(p - q).sqrMagnitude < gap * gap) return false;
            }
            return true;
        }
    }
}
