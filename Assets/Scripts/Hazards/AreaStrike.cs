using System;
using BossFight.Core;
using UnityEngine;

namespace BossFight.Hazards
{
    public enum AreaStrikeStyle { Root, Vine, Leaf, Seed }

    /// <summary>
    /// 資料驅動範圍攻擊（大樹守衛）；地面預兆、填充與 Hitbox.Filter 共用 AttackArea。取消立即關判定及預警。
    /// 地面預兆用專案共用的 GroundTelegraph（URP Decal，序章修正 C1）：白色可擋、紅色不可擋、安全區綠色，顏色都在同一個 GroundTelegraph prefab；
    /// 填色一律由攻擊源往外推（C7），填滿那一刻開判定，範圍裁切在場地內；萬根齊發最後一波的安全圈挖空並另外畫綠圈。
    /// 移動的環波可以加一段波前預警帶（ShowLead，C6），飛行的葉刃下方有一個跟著走的小框（C5）。
    /// 翻滾閃避：目標在範圍裡翻滾、判定窗結束沒打中時，經 Hurtbox.Dodger 回報給被打方結算（A5），這裡不判斷算不算成功。
    /// </summary>
    public class AreaStrike : Hazard
    {
        [Tooltip("地面預兆（Prefabs/Hazards/GroundTelegraph）；執行時複製幾份當主範圍、波前預警帶與安全圈")]
        [SerializeField] GroundTelegraph telegraphPrefab;
        [SerializeField] TimedFx impactPrefab;
        [Tooltip("種子炸彈爆炸的特效")]
        [SerializeField] TimedFx seedBlastPrefab;
        [SerializeField] Transform projectileVisual;
        [SerializeField] AttackAreaFx areaFxPrefab;
        [Header("純視覺")]
        [Tooltip("爆炸、衝擊特效的最大大小倍率")]
        [Min(0f)] [SerializeField] float impactMaxSize = 3f;
        [Tooltip("種子爆炸特效相對爆炸半徑的倍率")]
        [Min(0f)] [SerializeField] float seedBlastScale = 1f;

        AttackAreaFx areaFx;
        AreaStrikeStyle visualStyle;
        /// <summary>只當安全指引（綠色），不是攻擊：萬根齊發前的 9 m 提示。</summary>
        public bool SafetyGuidance { get; set; }
        public event Action<AreaStrike> OnStrike;
        public AttackArea Area { get; private set; }
        public bool Resolved { get; private set; }
        DamageInfo damage;
        Collider target;
        IDodgeable dodger;
        GroundTelegraph telegraph, lead;
        GroundTelegraph[] safeMarks;
        float timer, warning, window, speed, endRadius, travelLeft, bandWidth, leadWidth, sweepSign = 1f;
        AttackArea collisionArea;
        bool begun, active, struck, damaging = true, threatened, failed, evaded, opened;
        int openedFrame;
        int begunFrame;
        bool completedTravel;
        bool contracting;

        Color Tint => SafetyGuidance ? telegraphPrefab.SafeColor : telegraphPrefab.ColorFor(damage.canBlock);

        public void Begin(AttackArea area, in DamageInfo info, float warningTime, float hitWindow,
            Collider targetCollider, bool guidanceOnly = false, AreaStrikeStyle style = AreaStrikeStyle.Root)
        {
            Area = area; damage = info; warning = warningTime; window = hitWindow;
            target = targetCollider; begun = true; damaging = !guidanceOnly;
            var hurtbox = targetCollider != null ? targetCollider.GetComponent<Hurtbox>() : null;
            dodger = hurtbox != null ? hurtbox.Dodger : null;
            begunFrame = Time.frameCount;
            visualStyle = style;
            timer = 0f;
            hitbox.Filter = col => target != null && col == target && collisionArea.Overlaps(col);
            transform.position = area.fieldCenter;
            ShowTelegraph();
            // 落葉在白圈倒數時下降，填滿那一刻落地；其餘立體效果在判定開啟時出現。
            if (damaging && style == AreaStrikeStyle.Leaf && area.shape == AttackAreaShape.Circle) CreateAreaFx(true);
            Draw(0f);
        }

        /// <summary>出手後移動的環波；判定覆蓋上幀到本幀的環帶，避免穿透。</summary>
        public void Expand(float expandSpeed, float maximumRadius) { speed = expandSpeed; endRadius = maximumRadius; bandWidth = Area.radius - Area.innerRadius; }
        /// <summary>反向環波，掃過前後兩幀環帶；沿用同段 Hitbox 去重。</summary>
        public void Contract(float moveSpeed, float minimumOuterRadius)
        { Expand(moveSpeed, minimumOuterRadius); contracting = true; }
        /// <summary>飛行葉刃；平移判定掃過的距離，沿用 Hitbox 的一次命中去重。</summary>
        public void Fly(float moveSpeed, float distance) { speed = moveSpeed; travelLeft = distance; endRadius = 0f; }
        /// <summary>移動中的環波前方加一段寬 width 的預警帶（擴散時在外側、往內收時在內側），只是提示、沒有判定。</summary>
        public void ShowLead(float width) => leadWidth = width;
        /// <summary>藤蔓橫掃的方向（1 = 從左掃到右、-1 = 反過來），只影響藤蔓的掃動外觀。</summary>
        public void SetSweepDirection(float sign) => sweepSign = sign < 0f ? -1f : 1f;

        public void SetProjectileVisual(float centerHeight)
        {
            if (projectileVisual == null) return;
            projectileVisual.gameObject.SetActive(true);
            projectileVisual.position = Area.center + Area.forward * Area.length * 0.5f + Vector3.up * centerHeight;
            if (Area.forward.sqrMagnitude > 0f) projectileVisual.rotation = Quaternion.LookRotation(Area.forward);
        }

        protected override void HandleHit(HitReport report)
        {
            if (report.result == DamageResult.Hit || report.result == DamageResult.Blocked || report.result == DamageResult.Parried || report.result == DamageResult.GuardBroken) failed = true;
            base.HandleHit(report);
        }

        void Update()
        {
            if (!begun || Resolved || Time.deltaTime <= 0f || Time.frameCount == begunFrame) return;
            timer += Time.deltaTime;
            if (!struck)
            {
                threatened |= Dodging && Area.Overlaps(target);
                Draw(warning > 0f ? Mathf.Clamp01(timer / warning) : 1f);
                if (timer < warning || !damaging) return;
                struck = active = opened = true; openedFrame = Time.frameCount; timer = 0f;
                SetupHitbox(Area); hitbox.Activate(damage); OnStrike?.Invoke(this);
                if (areaFx == null && !(visualStyle == AreaStrikeStyle.Leaf && Area.shape == AttackAreaShape.Rectangle)) CreateAreaFx(false);
                if (areaFx != null) areaFx.Strike();
                Draw(1f);
                telegraph.Strike();
                SpawnImpact();
            }
            if (!active) return;
            if (completedTravel) { Close(); return; }
            bool rollingNow = Dodging;
            evaded |= threatened && rollingNow;
            threatened |= rollingNow && Area.Overlaps(target);
            if (speed > 0f)
            {
                var a = Area;
                if (endRadius > 0f)
                {
                    float prev = a.radius;
                    a.radius = Mathf.MoveTowards(a.radius, endRadius, speed * Time.deltaTime);
                    a.innerRadius = Mathf.Max(0f, (contracting ? a.radius : prev) - bandWidth);
                    float drawn = a.radius;
                    if (contracting) a.radius = prev;
                    SetupHitbox(a); a.radius = drawn;
                    a.innerRadius = Mathf.Max(0f, a.radius - bandWidth); Area = a; Draw(1f);
                    // 視覺下一幀仍用固定環寬；本幀命中留足掃掠寬度。
                    if (a.radius == endRadius) completedTravel = true;
                }
                else
                {
                    float step = Mathf.Min(travelLeft, speed * Time.deltaTime);
                    float original = a.length; a.length += step; Area = a; SetupHitbox(a);
                    a.center += a.forward * step; a.length = original; Area = a; Draw(1f);
                    travelLeft -= step;
                    if (projectileVisual != null) projectileVisual.position += a.forward * step;
                    if (travelLeft <= 0f) completedTravel = true;
                }
            }
            else if (timer >= window && Time.frameCount > openedFrame) Close();
        }

        bool Dodging => dodger != null && dodger.IsDodging;

        void SetupHitbox(AttackArea a)
        {
            collisionArea = a;
            // Hitbox 是整個場地的圓柱，實際形狀由 Filter 用世界座標幾何再過濾。
            hitbox.transform.SetPositionAndRotation(a.fieldCenter, Quaternion.identity);
            hitbox.transform.localScale = Vector3.one;
            hitbox.SetCylinder(new Vector3(0f, a.height * 0.5f, 0f), a.fieldRadius, a.height);
        }

        void SpawnImpact()
        {
            var a = Area;
            float extent = a.radius > 0f ? a.radius : a.width;
            if (visualStyle == AreaStrikeStyle.Seed)
            {
                TimedFx.Spawn(seedBlastPrefab, a.center + Vector3.up * a.height * 0.25f, Quaternion.identity, telegraphPrefab.ColorFor(damage.canBlock), extent * seedBlastScale);
                return;
            }
            TimedFx.Spawn(impactPrefab, a.center, Quaternion.identity, telegraphPrefab.ColorFor(damage.canBlock), Mathf.Min(extent, impactMaxSize));
        }

        void Close()
        {
            active = false; hitbox.Deactivate(); Resolved = true;
            HideTelegraphs();
            if (areaFx != null) { areaFx.Release(); areaFx = null; }
            // 翻滾時在範圍裡、判定窗結束沒打中：回報給被打方，算不算成功閃避由它決定
            if (opened && !failed && evaded && dodger != null) dodger.NotifyNearMiss(damage.WithSource(hitbox.transform.position, gameObject));
            Finish();
        }

        public void SetGuidanceProgress(float progress) => Draw(progress);

        void CreateAreaFx(bool prewarning)
        {
            if (visualStyle == AreaStrikeStyle.Seed) return;
            if (areaFxPrefab == null) return;
            areaFx = Instantiate(areaFxPrefab);
            areaFx.Show(Area, visualStyle, telegraphPrefab.ColorFor(damage.canBlock), prewarning, window, sweepSign);
        }

        // 依形狀建立地面預兆；場地外的部分裁掉
        void ShowTelegraph()
        {
            var a = Area;
            telegraph = Instantiate(telegraphPrefab, transform);
            Color tint = Tint;
            switch (a.shape)
            {
                case AttackAreaShape.Circle: telegraph.ShowCircle(a.center, a.radius, tint); break;
                case AttackAreaShape.Ring: telegraph.ShowCircle(a.center, a.radius, tint, a.innerRadius); break;
                case AttackAreaShape.Sector: telegraph.ShowSector(a.center, a.forward, a.radius, a.angle, tint); break;
                case AttackAreaShape.Rectangle: telegraph.ShowRect(a.center, a.forward, a.length, a.width, tint); break;
                case AttackAreaShape.Cross: telegraph.ShowCross(a.center, a.width, FarthestFieldDistance(a.center), tint); break;
                default: telegraph.ShowField(a.fieldCenter, a.fieldRadius, a.center, tint); break;
            }
            telegraph.SetClip(a.fieldCenter, a.fieldRadius);
            if (a.safeCenters != null && a.safeCenters.Length > 0)
            {
                telegraph.SetHoles(a.safeCenters, a.safeRadius);
                safeMarks = new GroundTelegraph[a.safeCenters.Length];
                for (int i = 0; i < safeMarks.Length; i++)
                {
                    safeMarks[i] = Instantiate(telegraphPrefab, transform);
                    safeMarks[i].ShowCircle(a.safeCenters[i], a.safeRadius, telegraphPrefab.SafeColor);
                    safeMarks[i].SetFill(1f);
                }
            }
        }

        float FarthestFieldDistance(Vector3 from)
        {
            Vector3 v = from - Area.fieldCenter;
            v.y = 0f;
            return v.magnitude + Area.fieldRadius;
        }

        void Draw(float fill)
        {
            var a = Area;
            if (telegraph == null) return;
            if (a.shape == AttackAreaShape.Ring) telegraph.MoveCircle(a.center, a.radius, a.innerRadius);
            else if (a.shape == AttackAreaShape.Circle) telegraph.MoveCircle(a.center, a.radius);
            else if (a.shape == AttackAreaShape.Rectangle) telegraph.MoveRect(a.center, a.forward, a.length, a.width);
            telegraph.SetFill(fill);
            DrawLead();
            if (areaFx != null) areaFx.SetArea(a, fill);
        }

        // 移動中的環波前方的預警帶：擴散時是外側一圈，往內收時是內側一圈；沒有填色，只有外框與底色
        void DrawLead()
        {
            if (leadWidth <= 0f || !struck || Area.shape != AttackAreaShape.Ring) return;
            if (lead == null)
            {
                lead = Instantiate(telegraphPrefab, transform);
                lead.ShowCircle(Area.center, Area.radius + leadWidth, Tint, Area.radius);
                lead.SetClip(Area.fieldCenter, Area.fieldRadius);
            }
            if (contracting)
            {
                float outer = Area.innerRadius;
                lead.MoveCircle(Area.center, outer, Mathf.Max(0f, outer - leadWidth));
            }
            else lead.MoveCircle(Area.center, Area.radius + leadWidth, Area.radius);
        }

        void HideTelegraphs()
        {
            if (telegraph != null) telegraph.HideImmediate();
            if (lead != null) lead.HideImmediate();
            if (safeMarks != null) foreach (var mark in safeMarks) if (mark != null) mark.HideImmediate();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            HideTelegraphs();
            if (areaFx != null)
            {
                if (!struck) Destroy(areaFx.gameObject);
                else areaFx.Release();
                areaFx = null;
            }
            // 預兆是本物件的子物件，跟著一起刪掉，不另外留下場景物件。
        }
    }
}
