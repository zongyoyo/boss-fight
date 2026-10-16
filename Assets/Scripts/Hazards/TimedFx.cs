using UnityEngine;

namespace BossFight.Hazards
{
    /// <summary>
    /// 放一次就消失的特效（爪痕、衝擊環、閃電柱、命中火花）：duration 秒內依曲線縮放、淡出，結束時刪掉自己。
    /// 顏色用 MaterialPropertyBlock 改 _BaseColor（Particles/Unlit 加算材質，HDR 顏色會被 Bloom 放大）；有 Light 的話一起閃。
    /// 用 Spawn 生成；這裡的數字全是視覺參數，沒有判定。
    /// </summary>
    [DisallowMultipleComponent]
    public class TimedFx : MonoBehaviour
    {
        [SerializeField] Renderer[] renderers;
        [Tooltip("一起閃的燈（可留空）")]
        [SerializeField] Light flashLight;
        [Tooltip("持續時間（秒）")]
        [Min(0.01f)] [SerializeField] float duration = 0.3f;
        [Tooltip("開始與結束時的縮放（乘上 Spawn 的 size）")]
        [SerializeField] Vector3 startScale = Vector3.one;
        [SerializeField] Vector3 endScale = Vector3.one;
        [Tooltip("縮放進度：橫軸時間 0..1，縱軸 0 = startScale、1 = endScale")]
        [SerializeField] AnimationCurve scaleCurve = new AnimationCurve(new Keyframe(0f, 0f, 2f, 2f), new Keyframe(1f, 1f, 0f, 0f));
        [Tooltip("亮度：橫軸時間 0..1，縱軸乘在顏色上")]
        [SerializeField] AnimationCurve intensityCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f));
        [Tooltip("燈的最大亮度")]
        [Min(0f)] [SerializeField] float lightIntensity = 8f;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        MaterialPropertyBlock block;
        Color color = Color.white;
        float size = 1f;
        float timer;

        /// <summary>在 position 生成 prefab 並播放；size 乘在縮放上。</summary>
        public static TimedFx Spawn(TimedFx prefab, Vector3 position, Quaternion rotation, Color color, float size = 1f)
        {
            if (prefab == null) return null;
            var fx = Instantiate(prefab, position, rotation);
            fx.color = color;
            fx.size = size;
            fx.Apply(0f);
            return fx;
        }

        void Awake()
        {
            block = new MaterialPropertyBlock();
        }

        void Update()
        {
            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / duration);
            Apply(t);
            if (t >= 1f) Destroy(gameObject);
        }

        void Apply(float t)
        {
            transform.localScale = Vector3.LerpUnclamped(startScale, endScale, scaleCurve.Evaluate(t)) * size;
            float k = Mathf.Max(0f, intensityCurve.Evaluate(t));
            Color c = color * k;
            c.a = color.a * Mathf.Clamp01(k);
            foreach (var r in renderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(block);
                block.SetColor(BaseColorId, c);
                r.SetPropertyBlock(block);
            }
            if (flashLight != null)
            {
                float max = Mathf.Max(color.maxColorComponent, 1e-3f);
                flashLight.color = max > 1f ? color / max : color; // HDR 顏色換成 0..1，亮度交給 intensity
                flashLight.intensity = lightIntensity * k;
            }
        }
    }
}
