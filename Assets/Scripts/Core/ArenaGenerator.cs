using UnityEngine;

namespace BossFight.Core
{
    /// <summary>
    /// 場景中的競技場根物件。尺寸全部來自 ArenaData；實際建模在 Editor/ArenaBuilder.cs：
    /// 在 Inspector 按「重建競技場」，或選單 Tools > Arena > Build Arena。
    /// </summary>
    [DisallowMultipleComponent]
    public class ArenaGenerator : MonoBehaviour
    {
        [Tooltip("競技場尺寸與配置")]
        public ArenaData data;

        void OnDrawGizmosSelected()
        {
            if (data == null) return;

            var prev = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix;

            Gizmos.color = new Color(1f, 0.8f, 0.1f);
            DrawCircle(data.radius, 0.1f);

            Gizmos.color = new Color(0.3f, 0.8f, 1f);
            if (data.gates != null)
            {
                foreach (var g in data.gates)
                    Gizmos.DrawLine(Vector3.up * 0.1f, ArenaData.Dir(g.angle) * data.radius + Vector3.up * 0.1f);
            }

            Gizmos.matrix = prev;
        }

        static void DrawCircle(float radius, float y)
        {
            const int n = 96;
            Vector3 prev = ArenaData.Dir(0f) * radius + Vector3.up * y;
            for (int i = 1; i <= n; i++)
            {
                Vector3 next = ArenaData.Dir(360f * i / n) * radius + Vector3.up * y;
                Gizmos.DrawLine(prev, next);
                prev = next;
            }
        }
    }
}
