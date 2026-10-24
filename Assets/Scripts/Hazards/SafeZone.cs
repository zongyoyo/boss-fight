using UnityEngine;

namespace BossFight.Hazards
{
    /// <summary>
    /// OIIA 大迴旋的一個安全區（佔位外觀）：地上的圓圈（GroundTelegraph，顏色另外指定）標出範圍，填色跟著倒數由 0 填滿，
    /// 魔法陣上同一塊的符文由 GrandSpinField 讓它熄滅。圈的正中央浮著一面發光盾牌（佔位標記，HazardBuilder 拼的），上下浮動、慢慢自轉，判定時消失。
    /// Contains 判斷一個點在不在圈內（只看水平距離）。大小與位置由出招方從 ScriptableObject 決定後傳入；這裡只放純視覺的參數。
    /// </summary>
    [DisallowMultipleComponent]
    public class SafeZone : MonoBehaviour
    {
        [Tooltip("地上的圓圈（子物件）")]
        [SerializeField] GroundTelegraph circle;
        [Tooltip("圓圈顏色（HDR，A 是整體不透明度）")]
        [SerializeField] [ColorUsage(true, true)] Color color = new Color(0.35f, 1.6f, 2.2f, 0.95f);

        [Header("漂浮盾牌")]
        [Tooltip("盾牌（子物件）；留空時沒有")]
        [SerializeField] Transform shield;
        [Tooltip("離地高度 (m)")]
        [Min(0f)] [SerializeField] float shieldHeight = 1.6f;
        [Tooltip("上下浮動的幅度 (m)")]
        [Min(0f)] [SerializeField] float shieldBob = 0.12f;
        [Tooltip("上下浮動每秒幾下")]
        [Min(0f)] [SerializeField] float shieldBobRate = 0.8f;
        [Tooltip("自轉速度（度/秒）")]
        [SerializeField] float shieldSpin = 60f;
        [Tooltip("出現時從 0 放大到原本大小的時間（秒）")]
        [Min(0.01f)] [SerializeField] float shieldPopTime = 0.3f;

        Vector3 shieldScale = Vector3.one;
        float shieldTimer;

        void Awake()
        {
            if (shield == null) return;
            shieldScale = shield.localScale;
            shield.gameObject.SetActive(false);
        }

        public float Radius { get; private set; }
        public Vector3 Center => transform.position;

        /// <summary>在 groundCenter 顯示半徑 radius 的安全區，填色從 0 開始。</summary>
        public void Show(Vector3 groundCenter, float radius)
        {
            transform.position = groundCenter;
            Radius = radius;
            circle.ShowCircle(groundCenter, radius, color);
            circle.SetFill(0f);
            shieldTimer = 0f;
            if (shield != null)
            {
                shield.gameObject.SetActive(true);
                PlaceShield();
            }
        }

        /// <summary>倒數進度 0..1（1 = 落地判定的瞬間）。</summary>
        public void SetProgress(float t) => circle.SetFill(t);

        /// <summary>判定發生：圈內閃亮一下再淡出。</summary>
        public void Strike()
        {
            circle.SetFill(1f);
            circle.Strike();
            circle.Hide();
            if (shield != null) shield.gameObject.SetActive(false);
        }

        void LateUpdate()
        {
            if (shield == null || !shield.gameObject.activeSelf) return;
            shieldTimer += Time.deltaTime;
            PlaceShield();
        }

        void PlaceShield()
        {
            float bob = Mathf.Sin(shieldTimer * shieldBobRate * Mathf.PI * 2f) * shieldBob;
            shield.localPosition = Vector3.up * (shieldHeight + bob);
            shield.localRotation = Quaternion.Euler(0f, shieldTimer * shieldSpin, 0f);
            shield.localScale = shieldScale * Mathf.SmoothStep(0f, 1f, shieldTimer / shieldPopTime);
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
