using System;
using BossFight.Core;
using UnityEngine;

namespace BossFight.Hazards
{
    /// <summary>
    /// 一道落雷：先在地上出現紅圈（GroundTelegraph），填色在 warningTime 秒內填滿；填滿那一刻落雷，
    /// 圓柱判定開 hitWindow 秒，同時紅圈閃亮、出現閃電柱與閃光（TimedFx）。紅圈淡出後刪掉自己。
    /// 數值（半徑、警告時間、判定時間與高度）由出招方從 ScriptableObject 傳入。
    /// </summary>
    [DisallowMultipleComponent]
    public class LightningStrike : Hazard
    {
        [Tooltip("地上的紅圈（子物件）")]
        [SerializeField] GroundTelegraph circle;
        [Tooltip("落雷瞬間的閃電柱與閃光")]
        [SerializeField] TimedFx boltPrefab;
        [SerializeField] [ColorUsage(true, true)] Color boltColor = new Color(3f, 2.6f, 4f, 1f);
        [Tooltip("落雷瞬間地上的衝擊環（可留空）")]
        [SerializeField] TimedFx impactPrefab;
        [SerializeField] [ColorUsage(true, true)] Color impactColor = new Color(2.5f, 1.6f, 3f, 1f);

        /// <summary>落雷（判定開始）的那一刻。</summary>
        public event Action<LightningStrike> OnStrike;

        DamageInfo damage;
        float radius, warningTime, hitWindow, height;
        float timer;
        bool started, struck, closed;

        /// <summary>出現紅圈；warning 秒後落雷，判定半徑 strikeRadius、高 hitHeight，持續 window 秒。</summary>
        public void Begin(in DamageInfo info, float strikeRadius, float warning, float window, float hitHeight)
        {
            damage = info;
            radius = strikeRadius;
            warningTime = warning;
            hitWindow = window;
            height = hitHeight;
            timer = 0f;
            started = true;
            struck = closed = false;
            circle.ShowCircle(transform.position, radius, false);
        }

        void Update()
        {
            if (!started) return;
            timer += Time.deltaTime;
            if (!struck)
            {
                circle.SetFill(warningTime > 0f ? timer / warningTime : 1f);
                if (timer >= warningTime) Strike();
            }
            else if (!closed && timer >= warningTime + hitWindow)
            {
                closed = true;
                hitbox.Deactivate();
            }
            else if (closed && !circle.IsVisible)
            {
                Finish();
            }
        }

        void Strike()
        {
            struck = true;
            hitbox.SetCylinder(new Vector3(0f, height * 0.5f, 0f), radius, height);
            hitbox.Activate(damage);
            circle.SetFill(1f);
            circle.Strike();
            circle.Hide();
            TimedFx.Spawn(boltPrefab, transform.position, Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f), boltColor);
            TimedFx.Spawn(impactPrefab, transform.position, Quaternion.identity, impactColor, radius);
            OnStrike?.Invoke(this);
        }
    }
}
