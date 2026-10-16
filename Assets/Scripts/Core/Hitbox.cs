using System;
using System.Collections.Generic;
using UnityEngine;

namespace BossFight.Core
{
    /// <summary>
    /// 攻擊判定範圍。Activate 之後每幀（LateUpdate）掃 targetMask 上的 Hurtbox，直到 Deactivate；
    /// 每次啟動對同一個 IDamageable 只結算一次，結果用 OnHit 回報給攻擊方。
    /// 送出的 DamageInfo 會填入 attacker 的位置，被打方用它判斷攻擊是不是從正面來（格擋、盾反）。
    /// 形狀用本物件的區域座標；玩法數值由攻擊方從 ScriptableObject 讀出後用 SetBox / SetSphere 設定。
    /// </summary>
    [DisallowMultipleComponent]
    public class Hitbox : MonoBehaviour
    {
        public enum Shape { Box, Sphere }

        [Tooltip("掃描的 Layer（Hurtbox）")]
        [SerializeField] LayerMask targetMask;
        [Tooltip("攻擊方的根物件：它底下的 Hurtbox 不會被打到，格擋判定也以它的位置為攻擊來源；留空用這個物件")]
        [SerializeField] Transform attacker;

        [Header("形狀（區域座標，通常由攻擊方在出手時設定）")]
        [SerializeField] Shape shape = Shape.Box;
        [SerializeField] Vector3 center;
        [Tooltip("Box 的完整尺寸")]
        [SerializeField] Vector3 size = Vector3.one;
        [Tooltip("Sphere 的半徑")]
        [Min(0f)] [SerializeField] float radius = 0.5f;

        /// <summary>打到一個目標並結算完。</summary>
        public event Action<HitReport> OnHit;

        public bool IsActive { get; private set; }

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
                if (!alreadyHit.Add(hurtbox.Owner)) continue;

                var sent = info.WithSource(from.position, from.gameObject);
                var result = hurtbox.Owner.TakeDamage(sent);
                OnHit?.Invoke(new HitReport(hurtbox, hurtbox.Owner, sent, result));
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
            Vector3 half = Vector3.Scale(size, s) * 0.5f;
            half = new Vector3(Mathf.Abs(half.x), Mathf.Abs(half.y), Mathf.Abs(half.z));
            return Physics.OverlapBoxNonAlloc(c, half, overlaps, transform.rotation, targetMask, QueryTriggerInteraction.Collide);
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
            Gizmos.matrix = transform.localToWorldMatrix;
            if (shape == Shape.Sphere) Gizmos.DrawWireSphere(center, radius);
            else Gizmos.DrawWireCube(center, size);
            Gizmos.matrix = Matrix4x4.identity;
        }
    }
}
