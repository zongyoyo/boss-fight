using UnityEngine;

namespace BossFight.Hazards
{
    /// <summary>
    /// OIIA 大迴旋的一個安全區（佔位外觀）：地上的圓圈（GroundTelegraph，顏色另外指定）標出範圍，填色跟著倒數由 0 填滿，
    /// 魔法陣上同一塊的符文由 GrandSpinField 讓它熄滅。Contains 判斷一個點在不在圈內（只看水平距離）。
    /// 大小與位置由出招方從 ScriptableObject 決定後傳入；這裡只放純視覺的參數。
    /// </summary>
    [DisallowMultipleComponent]
    public class SafeZone : MonoBehaviour
    {
        [Tooltip("地上的圓圈（子物件）")]
        [SerializeField] GroundTelegraph circle;
        [Tooltip("圓圈顏色（HDR，A 是整體不透明度）")]
        [SerializeField] [ColorUsage(true, true)] Color color = new Color(0.35f, 1.6f, 2.2f, 0.95f);

        public float Radius { get; private set; }
        public Vector3 Center => transform.position;

        /// <summary>在 groundCenter 顯示半徑 radius 的安全區，填色從 0 開始。</summary>
        public void Show(Vector3 groundCenter, float radius)
        {
            transform.position = groundCenter;
            Radius = radius;
            circle.ShowCircle(groundCenter, radius, color);
            circle.SetFill(0f);
        }

        /// <summary>倒數進度 0..1（1 = 落地判定的瞬間）。</summary>
        public void SetProgress(float t) => circle.SetFill(t);

        /// <summary>判定發生：圈內閃亮一下再淡出。</summary>
        public void Strike()
        {
            circle.SetFill(1f);
            circle.Strike();
            circle.Hide();
        }

        public bool IsVisible => circle.IsVisible;

        /// <summary>point 在圈內（水平距離 ≤ 半徑）。</summary>
        public bool Contains(Vector3 point)
        {
            Vector3 d = point - transform.position;
            d.y = 0f;
            return d.sqrMagnitude <= Radius * Radius;
        }
    }
}
