using UnityEngine;

namespace BossFight.Hazards
{
    public enum AttackAreaShape { Circle, Ring, Sector, Rectangle, Cross, Field }

    /// <summary>
    /// 預警與判定共用的世界座標幾何。場地半徑覆蓋真正牆界，安全圈採既有 Hurtbox 中心規則。
    /// 環可以分段（segments &gt; 0，樹根環障一根刺一段）：只有離最近一段中心線的弧長在 segmentHalfWidth 內才算，段與段之間的空隙打不到。
    /// </summary>
    public struct AttackArea
    {
        public AttackAreaShape shape;
        public Vector3 center, forward, fieldCenter;
        public float radius, innerRadius, angle, length, width, height, fieldRadius, safeRadius;
        public Vector3[] safeCenters;
        /// <summary>環的段數（0 = 整圈）；第一段在世界角度 segmentPhase 度（0 = 北、順時針），每段沿弧長半寬 segmentHalfWidth (m)。</summary>
        public int segments;
        public float segmentPhase, segmentHalfWidth;
        public bool Contains(Vector3 point, float margin = 0f)
        {
            Vector3 v = point - center; v.y = 0f;
            Vector3 f = forward.sqrMagnitude > 0f ? forward.normalized : Vector3.forward;
            float x = Vector3.Dot(v, Vector3.Cross(Vector3.up, f)), z = Vector3.Dot(v, f), d = v.magnitude;
            if (Vector2.Distance(new Vector2(point.x, point.z), new Vector2(fieldCenter.x, fieldCenter.z)) > fieldRadius + margin) return false;
            if (safeCenters != null)
                foreach (var safe in safeCenters)
                    if (new Vector2(point.x - safe.x, point.z - safe.z).sqrMagnitude <= safeRadius * safeRadius) return false;
            switch (shape)
            {
                case AttackAreaShape.Circle: return d <= radius + margin;
                case AttackAreaShape.Ring: return d <= radius + margin && d + margin >= innerRadius && InSegment(v, d, margin);
                case AttackAreaShape.Sector:
                    return d <= radius + margin && (d <= margin || Vector3.Angle(f, v) <= angle * 0.5f + Mathf.Asin(Mathf.Clamp01(margin / Mathf.Max(d, margin))) * Mathf.Rad2Deg);
                case AttackAreaShape.Rectangle: return Mathf.Abs(x) <= width * 0.5f + margin && z >= -margin && z <= length + margin;
                case AttackAreaShape.Cross: return Mathf.Abs(v.x) <= width * 0.5f + margin || Mathf.Abs(v.z) <= width * 0.5f + margin;
                default: return true;
            }
        }
        /// <summary>第 index 段中心的世界角度（度）。</summary>
        public float SegmentAngle(int index) => segmentPhase + index * 360f / Mathf.Max(1, segments);

        // 離最近一段中心線的弧長（在這個半徑上量）不超過半寬＋margin
        bool InSegment(Vector3 flat, float d, float margin)
        {
            if (segments <= 0) return true;
            float step = 360f / segments;
            float angle = Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg - segmentPhase;
            float offset = Mathf.Abs(Mathf.DeltaAngle(0f, angle - Mathf.Round(angle / step) * step));
            return offset * Mathf.Deg2Rad * d <= segmentHalfWidth + margin;
        }

        public bool Overlaps(Collider target)
        {
            if (target == null || !target.enabled) return false;
            Bounds b = target.bounds;
            if (b.max.y < center.y || b.min.y > center.y + height) return false;
            return Contains(b.center, Mathf.Max(b.extents.x, b.extents.z));
        }
    }
}
