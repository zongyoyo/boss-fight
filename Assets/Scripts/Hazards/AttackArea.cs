using UnityEngine;

namespace BossFight.Hazards
{
    public enum AttackAreaShape { Circle, Ring, Sector, Rectangle, Cross, Field }

    /// <summary>預警與判定共用的世界座標幾何。場地半徑覆蓋真正牆界，安全圈採既有 Hurtbox 中心規則。</summary>
    public struct AttackArea
    {
        public AttackAreaShape shape;
        public Vector3 center, forward, fieldCenter;
        public float radius, innerRadius, angle, length, width, height, fieldRadius, safeRadius;
        public Vector3[] safeCenters;
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
                case AttackAreaShape.Ring: return d <= radius + margin && d + margin >= innerRadius;
                case AttackAreaShape.Sector:
                    return d <= radius + margin && (d <= margin || Vector3.Angle(f, v) <= angle * 0.5f + Mathf.Asin(Mathf.Clamp01(margin / Mathf.Max(d, margin))) * Mathf.Rad2Deg);
                case AttackAreaShape.Rectangle: return Mathf.Abs(x) <= width * 0.5f + margin && z >= -margin && z <= length + margin;
                case AttackAreaShape.Cross: return Mathf.Abs(v.x) <= width * 0.5f + margin || Mathf.Abs(v.z) <= width * 0.5f + margin;
                default: return true;
            }
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
