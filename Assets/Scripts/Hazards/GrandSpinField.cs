using System.Collections.Generic;
using BossFight.Core;
using UnityEngine;

namespace BossFight.Hazards
{
    /// <summary>
    /// OIIA 大迴旋的全場判定：Begin 時在指定位置放安全區（SafeZone），並讓魔法陣上這些位置的符文熄滅；倒數期間安全區的圓圈慢慢填滿。
    /// 出招方在 Boss 落地那一刻呼叫 Strike：以場地中心為圓心、半徑 fieldRadius 的圓柱判定開 hitWindow 秒，
    /// Hurtbox 中心站在任何一個安全區裡的目標不算打到（Hitbox.Filter）；同時整片地面閃紅光。之後安全區淡出、符文恢復，刪掉自己。
    /// 被收掉（Despawn）時也會把符文恢復。數值（半徑、高度、倒數、安全區）由出招方從 ScriptableObject 傳入，這裡只放純視覺的參數。
    /// </summary>
    [DisallowMultipleComponent]
    public class GrandSpinField : Hazard
    {
        [Tooltip("安全區（執行時每個生成一份，放在這個物件底下）")]
        [SerializeField] SafeZone zonePrefab;
        [Tooltip("落地時整片範圍閃一下的地面提示（子物件，不可擋的紅色）")]
        [SerializeField] GroundTelegraph flash;
        [Tooltip("符文熄滅、恢復的時間（秒）")]
        [Min(0.01f)] [SerializeField] float darkenTime = 0.35f;

        readonly List<SafeZone> zones = new List<SafeZone>();
        readonly List<Vector3> centers = new List<Vector3>();
        MagicCircle magicCircle;
        DamageInfo damage;
        float fieldSize, countdown, hitWindow, timer, strikeTimer;
        bool counting, struck, closed;

        /// <summary>安全區的圓心（地面高度）。</summary>
        public IReadOnlyList<Vector3> SafeCenters => centers;

        /// <summary>
        /// 開始倒數：在 safeCenters 放半徑 zoneRadius 的安全區（circle 不為 null 時同一塊的符文熄滅），countdownTime 秒後填滿；
        /// 落地時（Strike）的判定半徑 fieldRadius、高 hitHeight、持續 window 秒。
        /// </summary>
        public void Begin(in DamageInfo info, float fieldRadius, float hitHeight, float countdownTime, float window,
                          IReadOnlyList<Vector3> safeCenters, float zoneRadius, MagicCircle circle)
        {
            damage = info;
            countdown = countdownTime;
            hitWindow = window;
            magicCircle = circle;
            timer = 0f;
            counting = true;
            struck = closed = false;

            centers.Clear();
            for (int i = 0; i < safeCenters.Count; i++)
            {
                var zone = Instantiate(zonePrefab, transform);
                zone.Show(safeCenters[i], zoneRadius);
                zones.Add(zone);
                centers.Add(safeCenters[i]);
            }
            if (magicCircle != null)
            {
                magicCircle.SetDarkZones(centers, zoneRadius);
                magicCircle.SetDarkZoneStrength(0f);
            }

            hitbox.SetCylinder(new Vector3(0f, hitHeight * 0.5f, 0f), fieldRadius, hitHeight);
            hitbox.Filter = col => !InSafeZone(col.bounds.center);
            fieldSize = fieldRadius;
        }

        /// <summary>Boss 落地：全場判定（安全區除外），地面閃紅光、安全區閃一下後淡出。</summary>
        public void Strike()
        {
            if (struck) return;
            counting = false;
            struck = true;
            strikeTimer = 0f;
            hitbox.Activate(damage);
            foreach (var zone in zones) zone.Strike();
            flash.ShowCircle(transform.position, fieldSize, false);
            flash.SetFill(1f);
            flash.Strike();
            flash.Hide();
        }

        /// <summary>point（只看水平）在任何一個安全區裡。</summary>
        public bool InSafeZone(Vector3 point)
        {
            foreach (var zone in zones)
            {
                if (zone.Contains(point)) return true;
            }
            return false;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (counting)
            {
                timer += dt;
                float t = countdown > 0f ? Mathf.Clamp01(timer / countdown) : 1f;
                foreach (var zone in zones) zone.SetProgress(t);
                if (magicCircle != null) magicCircle.SetDarkZoneStrength(timer / darkenTime);
                return;
            }
            if (!struck) return;

            strikeTimer += dt;
            if (!closed && strikeTimer >= hitWindow)
            {
                closed = true;
                hitbox.Deactivate();
            }
            float restore = Mathf.Clamp01((strikeTimer - hitWindow) / darkenTime);
            if (magicCircle != null) magicCircle.SetDarkZoneStrength(1f - restore);
            if (!closed || restore < 1f || flash.IsVisible) return;
            foreach (var zone in zones)
            {
                if (zone.IsVisible) return;
            }
            Finish();
        }

        void OnDestroy()
        {
            if (magicCircle != null) magicCircle.ClearDarkZones();
        }
    }
}
