using System;
using BossFight.Core;
using UnityEngine;

namespace BossFight.Hazards
{
    public enum AreaStrikeStyle { Root, Vine, Leaf }
    /// <summary>資料驅動範圍攻擊；預警、填充與 Hitbox.Filter 共用 AttackArea。取消立即關判定及預警。</summary>
    public class AreaStrike : Hazard
    {
        [SerializeField] MeshRenderer warningRenderer;
        [SerializeField] TimedFx impactPrefab;
        [SerializeField] Transform projectileVisual;
        [SerializeField] AttackAreaFx areaFxPrefab;
        AttackAreaFx areaFx;
        AreaStrikeStyle visualStyle;
        public bool SafetyGuidance { get; set; }
        [SerializeField] float visualHeight = 0.025f;
        [SerializeField] Color white = new Color(2f, 2f, 2f, 0.7f);
        [SerializeField] Color red = new Color(3f, 0.1f, 0.05f, 0.7f);
        [SerializeField] Color green = new Color(0.2f, 2f, 0.6f, 0.7f);
        public event Action<AreaStrike> OnStrike;
        public event Action<Vector3> OnAvoided;
        public AttackArea Area { get; private set; }
        public bool Resolved { get; private set; }
        MaterialPropertyBlock block;
        DamageInfo damage;
        Collider target;
        Func<bool> rolling;
        float timer, warning, window, speed, endRadius, travelLeft, bandWidth;
        AttackArea collisionArea;
        bool begun, active, struck, damaging = true, threatened, failed, evaded, opened;
        int openedFrame;
        int begunFrame;
        bool completedTravel;
        public void Begin(AttackArea area, in DamageInfo info, float warningTime, float hitWindow,
            Collider targetCollider, Func<bool> isRolling, bool guidanceOnly = false, AreaStrikeStyle style = AreaStrikeStyle.Root)
        {
            Area = area; damage = info; warning = warningTime; window = hitWindow;
            target = targetCollider; rolling = isRolling; begun = true; damaging = !guidanceOnly;
            begunFrame = Time.frameCount;
            visualStyle = style;
            timer = 0f; block = new MaterialPropertyBlock();
            hitbox.Filter = col => target != null && col == target && collisionArea.Overlaps(col);
            transform.position = area.fieldCenter + Vector3.up * visualHeight;
            warningRenderer.transform.localScale = new Vector3(area.fieldRadius * 2f, 1f, area.fieldRadius * 2f);
            // 落葉在白圈倒數時下降，填滿那一刻落地；其餘立體效果在判定開啟時出現。
            if (damaging && style == AreaStrikeStyle.Leaf && area.shape == AttackAreaShape.Circle) CreateAreaFx(true);
            Draw(0f);
        }
        /// <summary>出手後移動的環波；判定覆蓋上幀到本幀的環帶，避免穿透。</summary>
        public void Expand(float expandSpeed, float maximumRadius) { speed = expandSpeed; endRadius = maximumRadius; bandWidth = Area.radius - Area.innerRadius; }
        /// <summary>飛行葉刃；平移判定掃過的距離，沿用 Hitbox 的一次命中去重。</summary>
        public void Fly(float moveSpeed, float distance) { speed = moveSpeed; travelLeft = distance; endRadius = 0f; }
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
            if (report.result == DamageResult.Evaded && rolling != null && rolling()) evaded = true;
            base.HandleHit(report);
        }
        void Update()
        {
            if (!begun || Resolved || Time.deltaTime <= 0f || Time.frameCount == begunFrame) return;
            timer += Time.deltaTime;
            if (!struck)
            {
                threatened |= rolling != null && rolling() && Area.Overlaps(target);
                Draw(warning > 0f ? Mathf.Clamp01(timer / warning) : 1f);
                if (timer < warning || !damaging) return;
                struck = active = opened = true; openedFrame = Time.frameCount; timer = 0f;
                SetupHitbox(Area); hitbox.Activate(damage); OnStrike?.Invoke(this);
                if (areaFx == null && !(visualStyle == AreaStrikeStyle.Leaf && Area.shape == AttackAreaShape.Rectangle)) CreateAreaFx(false);
                if (areaFx != null) areaFx.Strike();
                Draw(1f);
                TimedFx.Spawn(impactPrefab, Area.center + Vector3.up * visualHeight, Quaternion.identity, damage.canBlock ? white : red, Mathf.Min(Area.radius > 0f ? Area.radius : Area.width, 3f));
            }
            if (!active) return;
            if (completedTravel) { Close(); return; }
            bool rollingNow = rolling != null && rolling();
            evaded |= threatened && rollingNow;
            threatened |= rollingNow && Area.Overlaps(target);
            if (speed > 0f)
            {
                var a = Area;
                if (endRadius > 0f)
                {
                    float prev = a.radius;
                    a.radius = Mathf.Min(endRadius, a.radius + speed * Time.deltaTime);
                    a.innerRadius = Mathf.Max(0f, prev - bandWidth); SetupHitbox(a);
                    a.innerRadius = Mathf.Max(0f, a.radius - bandWidth); Area = a; Draw(1f);
                    // 視覺下一幀仍用固定環寬；本幀命中留足掃掠寬度。
                    if (a.radius >= endRadius) completedTravel = true;
                }
                else
                {
                    float step = Mathf.Min(travelLeft, speed * Time.deltaTime);
                    float original = a.length; a.length += step; Area = a; SetupHitbox(a); Draw(1f);
                    a.center += a.forward * step; a.length = original; Area = a;
                    travelLeft -= step;
                    if (projectileVisual != null) projectileVisual.position += a.forward * step;
                    if (travelLeft <= 0f) completedTravel = true;
                }
            }
            else if (timer >= window && Time.frameCount > openedFrame) Close();
        }
        void SetupHitbox(AttackArea a)
        {
            collisionArea = a;
            // Hitbox 不掛在縮放的預警平面下；世界座標幾何透過寬相位再過濾。
            hitbox.transform.SetPositionAndRotation(a.fieldCenter, Quaternion.identity);
            hitbox.transform.localScale = Vector3.one;
            hitbox.SetCylinder(new Vector3(0f, a.height * 0.5f, 0f), a.fieldRadius, a.height);
        }
        void Close()
        {
            active = false; hitbox.Deactivate(); Resolved = true; warningRenderer.enabled = false;
            if (areaFx != null) { areaFx.Release(); areaFx = null; }
            if (opened && !failed && evaded && target != null) OnAvoided?.Invoke(target.transform.position);
            Finish();
        }
        public void SetGuidanceProgress(float progress) => Draw(progress);
        void CreateAreaFx(bool prewarning)
        {
            if (areaFxPrefab == null) return;
            areaFx = Instantiate(areaFxPrefab);
            areaFx.Show(Area, visualStyle, damage.canBlock ? white : red, prewarning);
        }
        void Draw(float fill)
        {
            var a = Area;
            warningRenderer.enabled = true;
            block.SetVector("_Origin", a.center);
            block.SetVector("_Forward", a.forward);
            block.SetVector("_Field", new Vector4(a.fieldCenter.x, a.fieldCenter.z, a.fieldRadius, a.safeRadius));
            block.SetVector("_Shape", new Vector4((float)a.shape, a.radius, a.innerRadius, a.angle));
            block.SetVector("_Size", new Vector4(a.length, a.width, fill, a.safeCenters != null ? a.safeCenters.Length : 0));
            block.SetColor("_Color", SafetyGuidance ? green : damage.canBlock ? white : red);
            block.SetVector("_Visual", new Vector4(struck ? 1f : 0f, 0f, 0f, 0f));
            for (int i = 0; i < 3; i++) block.SetVector("_Safe" + i, a.safeCenters != null && i < a.safeCenters.Length ? a.safeCenters[i] : new Vector3(float.MaxValue, 0f, float.MaxValue));
            warningRenderer.SetPropertyBlock(block);
            if (areaFx != null) areaFx.SetArea(a, fill);
        }
        protected override void OnDisable()
        {
            base.OnDisable();
            if (warningRenderer != null) warningRenderer.enabled = false;
            if (areaFx != null)
            {
                if (!struck) Destroy(areaFx.gameObject);
                else areaFx.Release();
                areaFx = null;
            }
            // 判定物件是同一個 prefab 的兄弟，不另外留下場景物件。
        }
    }
}
