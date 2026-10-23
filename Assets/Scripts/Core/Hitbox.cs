using System;
using System.Collections.Generic;
using UnityEngine;

namespace BossFight.Core
{
    /// <summary>
    /// 攻擊判定範圍。Activate 之後每幀（LateUpdate）掃 targetMask 上的 Hurtbox，直到 Deactivate；
    /// 每次啟動對同一個 IDamageable 只結算一次，結果用 OnHit 回報給攻擊方。
    /// 送出的 DamageInfo 會填入 attacker 的位置，被打方用它判斷攻擊是不是從正面來（格擋、盾反）。
    /// 形狀用本物件的區域座標；玩法數值由攻擊方從 ScriptableObject 讀出後用 SetBox / SetSphere / SetCylinder 設定。
    /// Cylinder 是直立的圓柱（假設物件沒有傾斜），內半徑 &gt; 0 時中間是空的，變成環（衝擊波）；
    /// 判斷用 Hurtbox 的 bounds 在水平面上和 [內半徑, 半徑] 有沒有重疊。
    /// </summary>
    [DisallowMultipleComponent]
    public class Hitbox : MonoBehaviour
    {
        public enum Shape { Box, Sphere, Cylinder }

        [Tooltip("掃描的 Layer（Hurtbox）")]
        [SerializeField] LayerMask targetMask;
        [Tooltip("攻擊方的根物件：它底下的 Hurtbox 不會被打到，格擋判定也以它的位置為攻擊來源；留空用這個物件")]
        [SerializeField] Transform attacker;

        [Header("形狀（區域座標，通常由攻擊方在出手時設定）")]
        [SerializeField] Shape shape = Shape.Box;
        [SerializeField] Vector3 center;
        [Tooltip("Box 的完整尺寸")]
        [SerializeField] Vector3 size = Vector3.one;
        [Tooltip("Sphere、Cylinder 的半徑")]
        [Min(0f)] [SerializeField] float radius = 0.5f;
        [Tooltip("Cylinder 的高度（center 在圓柱中間）")]
        [Min(0f)] [SerializeField] float height = 1f;
        [Tooltip("Cylinder 的內半徑；0 = 實心圓柱，大於 0 = 環")]
        [Min(0f)] [SerializeField] float innerRadius;

        /// <summary>打到一個目標並結算完。</summary>
        public event Action<HitReport> OnHit;

        public bool IsActive { get; private set; }

        /// <summary>
        /// 額外的命中條件：回傳 false 的 Hurtbox Collider 不算打到（例如大迴旋排除站在安全區裡的目標、幻影不打到 Boss 本體）。null = 不篩選。
        /// </summary>
        public Func<Collider, bool> Filter { get; set; }

        readonly HashSet<IDamageable> alreadyHit = new HashSet<IDamageable>();
        readonly Collider[] overlaps = new Collider[16];
        DamageInfo info;

        Transform Attacker => attacker != null ? attacker : transform;

        public void SetBox(Vector3 localCenter, Vector3 localSize)
        {
            shape = Shape.Box;
            center = localCenter;
            size = localSize;
        }

        public void SetSphere(Vector3 localCenter, float sphereRadius)
        {
            shape = Shape.Sphere;
            center = localCenter;
            radius = sphereRadius;
        }

        /// <summary>直立圓柱；inner &gt; 0 時是環，只打得到離中心 inner 到 outer 之間的目標。</summary>
        public void SetCylinder(Vector3 localCenter, float outer, float cylinderHeight, float inner = 0f)
        {
            shape = Shape.Cylinder;
            center = localCenter;
            radius = outer;
            height = cylinderHeight;
            innerRadius = Mathf.Clamp(inner, 0f, outer);
        }

        /// <summary>執行期才生成的判定（Hazard）用：指定攻擊方的根物件。</summary>
        public void SetAttacker(Transform root) => attacker = root;

        /// <summary>開始判定；同一個目標在下次 Activate 之前只會被打一次。</summary>
        public void Activate(in DamageInfo damage)
        {
            info = damage;
            alreadyHit.Clear();
            IsActive = true;
        }

        public void Deactivate()
        {
            IsActive = false;
        }

        void OnDisable()
        {
            IsActive = false;
        }

        void LateUpdate()
        {
            if (!IsActive) return;

            Transform from = Attacker;
            int count = Overlap();
            for (int i = 0; i < count && IsActive; i++)
            {
                var hurtbox = overlaps[i].GetComponent<Hurtbox>();
                if (hurtbox == null || hurtbox.Owner == null) continue;
                if (hurtbox.transform.IsChildOf(from)) continue;
                if (shape == Shape.Cylinder && !InCylinder(overlaps[i])) continue;
                if (Filter != null && !Filter(overlaps[i])) continue;
                if (!alreadyHit.Add(hurtbox.Owner)) continue;

                var sent = info.WithSource(from.position, from.gameObject);
                var outcome = hurtbox.Owner.TakeDamage(sent);
                OnHit?.Invoke(new HitReport(hurtbox, hurtbox.Owner, sent, outcome));
            }
        }

        int Overlap()
        {
            Vector3 c = transform.TransformPoint(center);
            Vector3 s = transform.lossyScale;
            if (shape == Shape.Sphere)
            {
                float r = radius * Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));
                return Physics.OverlapSphereNonAlloc(c, r, overlaps, targetMask, QueryTriggerInteraction.Collide);
            }
            Vector3 half = shape == Shape.Cylinder
                ? new Vector3(WorldRadius(radius), height * Mathf.Abs(s.y) * 0.5f, WorldRadius(radius))
                : Vector3.Scale(size, s) * 0.5f;
            half = new Vector3(Mathf.Abs(half.x), Mathf.Abs(half.y), Mathf.Abs(half.z));
            Quaternion rot = shape == Shape.Cylinder ? Quaternion.identity : transform.rotation;
            return Physics.OverlapBoxNonAlloc(c, half, overlaps, rot, targetMask, QueryTriggerInteraction.Collide);
        }

        float WorldRadius(float r)
        {
            Vector3 s = transform.lossyScale;
            return r * Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.z));
        }

        // 目標的 bounds 在水平面上離圓柱中心最近、最遠的距離，和 [內半徑, 半徑] 有重疊才算打到
        bool InCylinder(Collider col)
        {
            Vector3 c = transform.TransformPoint(center);
            Bounds b = col.bounds;
            float d = new Vector2(b.center.x - c.x, b.center.z - c.z).magnitude;
            float e = Mathf.Max(b.extents.x, b.extents.z);
            return d - e <= WorldRadius(radius) && d + e >= WorldRadius(innerRadius);
        }

        void OnDrawGizmos()
        {
            if (!IsActive) return;
            DrawShape(new Color(1f, 0.2f, 0.2f, 0.9f));
        }

        void OnDrawGizmosSelected()
        {
            if (IsActive) return;
            DrawShape(new Color(1f, 0.85f, 0.2f, 0.6f));
        }

        void DrawShape(Color color)
        {
            Gizmos.color = color;
            if (shape == Shape.Cylinder)
            {
                Vector3 c = transform.TransformPoint(center);
                float h = height * Mathf.Abs(transform.lossyScale.y) * 0.5f;
                foreach (float r in new[] { WorldRadius(radius), WorldRadius(innerRadius) })
                {
                    if (r <= 0f) continue;
                    DrawCircle(c + Vector3.up * h, r);
                    DrawCircle(c - Vector3.up * h, r);
                }
                return;
            }
            Gizmos.matrix = transform.localToWorldMatrix;
            if (shape == Shape.Sphere) Gizmos.DrawWireSphere(center, radius);
            else Gizmos.DrawWireCube(center, size);
            Gizmos.matrix = Matrix4x4.identity;
        }

        static void DrawCircle(Vector3 c, float r)
        {
            const int segments = 32;
            Vector3 prev = c + new Vector3(r, 0f, 0f);
            for (int i = 1; i <= segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                Vector3 p = c + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                Gizmos.DrawLine(prev, p);
                prev = p;
            }
        }
    }
}
