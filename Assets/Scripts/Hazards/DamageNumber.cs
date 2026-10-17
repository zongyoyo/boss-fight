using UnityEngine;

namespace BossFight.Hazards
{
    /// <summary>
    /// 跳出來的傷害數字（一次性特效）：生成時彈大一下，往上飄、淡出，lifetime 秒後刪掉自己；永遠面向鏡頭。
    /// 用 TextMesh（內建字型），shadow 是往後偏一點的黑色副本，在亮的背景上也看得到。
    /// 用 Spawn 生成；這裡的數字全是視覺參數。
    /// </summary>
    [DisallowMultipleComponent]
    public class DamageNumber : MonoBehaviour
    {
        [SerializeField] TextMesh text;
        [Tooltip("陰影（黑色副本），可留空")]
        [SerializeField] TextMesh shadow;
        [Tooltip("持續時間（秒）")]
        [Min(0.05f)] [SerializeField] float lifetime = 0.8f;
        [Tooltip("往上飄的速度 (m/s)，會慢慢減速")]
        [SerializeField] float riseSpeed = 1.6f;
        [Tooltip("生成時放大到這個倍率再縮回 1")]
        [Min(1f)] [SerializeField] float popScale = 1.6f;
        [Tooltip("放大再縮回的時間（秒）")]
        [Min(0.01f)] [SerializeField] float popTime = 0.12f;
        [Tooltip("從這個時間比例開始淡出")]
        [Range(0f, 1f)] [SerializeField] float fadeStart = 0.55f;

        Transform view;
        Color color = Color.white;
        Color shadowColor;
        Vector3 baseScale;
        float size = 1f;
        float timer;

        /// <summary>在 position 生成一個數字；view 是鏡頭（面向它），size 乘在大小上。</summary>
        public static DamageNumber Spawn(DamageNumber prefab, Vector3 position, string label, Color color, float size, Transform view)
        {
            if (prefab == null) return null;
            var n = Instantiate(prefab, position, view != null ? view.rotation : Quaternion.identity);
            n.view = view;
            n.color = color;
            n.size = size;
            n.text.text = label;
            if (n.shadow != null) n.shadow.text = label;
            n.Apply(0f);
            return n;
        }

        void Awake()
        {
            baseScale = transform.localScale;
            if (shadow != null) shadowColor = shadow.color;
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            timer += dt;
            float t = Mathf.Clamp01(timer / lifetime);
            transform.position += Vector3.up * (riseSpeed * (1f - t) * dt);
            Apply(t);
            if (t >= 1f) Destroy(gameObject);
        }

        void Apply(float t)
        {
            if (view != null) transform.rotation = view.rotation;
            float pop = Mathf.Lerp(popScale, 1f, Mathf.Clamp01(timer / popTime));
            transform.localScale = baseScale * (size * pop);

            float alpha = 1f - Mathf.Clamp01((t - fadeStart) / Mathf.Max(1e-4f, 1f - fadeStart));
            var c = color;
            c.a *= alpha;
            text.color = c;
            if (shadow != null)
            {
                var s = shadowColor;
                s.a *= alpha;
                shadow.color = s;
            }
        }
    }
}
