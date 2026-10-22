using UnityEngine;

namespace BossFight.Hazards
{
    /// <summary>電光的擺法。</summary>
    public enum ArcLayout
    {
        /// <summary>沿著圓柱面由下往上、一邊繞圈的長電弧（纏在 Boss 身上）。</summary>
        Wrap,
        /// <summary>圓周上隨機位置往上冒的短電弧（魔法陣、場地邊緣）。</summary>
        Rim,
    }

    /// <summary>
    /// 佔位的電光：幾條鋸齒狀的 LineRenderer 在一個圓柱面上閃爍，每 flickerInterval 秒重新隨機形狀與位置。
    /// SetIntensity(0..1) 控制亮度與同時出現的條數，0 = 全部隱藏。線在自己的區域座標裡，跟著物件移動。
    /// 第二階段 Boss 身上的紅色電光（BossPhaseVisual、轉階段演出的 ElectricArcsTrack）與轉階段時場地邊緣的電光共用。
    /// 線在 Awake 時依 arcCount 建立（子物件），顏色用 MaterialPropertyBlock 改 _BaseColor（加算材質，HDR 顏色會被 Bloom 放大）。
    /// 這裡的數字全是視覺參數。
    /// </summary>
    [DisallowMultipleComponent]
    public class ElectricArcs : MonoBehaviour
    {
        [Tooltip("加算、吃頂點色的材質（Fx_Additive）")]
        [SerializeField] Material material;
        [SerializeField] ArcLayout layout = ArcLayout.Wrap;
        [SerializeField] [ColorUsage(true, true)] Color color = new Color(4f, 0.35f, 0.25f, 1f);
        [Tooltip("電弧條數（強度 1 時全部出現）")]
        [Range(1, 64)] [SerializeField] int arcCount = 5;
        [Tooltip("每條電弧的折點數")]
        [Range(2, 32)] [SerializeField] int points = 10;
        [Tooltip("圓柱半徑 (m)")]
        [Min(0f)] [SerializeField] float radius = 1f;
        [Tooltip("電弧底部離地高度 (m)")]
        [SerializeField] float baseHeight = 0.2f;
        [Tooltip("電弧高度 (m)；Rim 時每條在一半到這個高度之間隨機")]
        [Min(0.05f)] [SerializeField] float height = 2.2f;
        [Tooltip("Wrap：每條電弧由下往上繞多少度")]
        [SerializeField] float wrapAngle = 140f;
        [Tooltip("折點左右亂跳的幅度 (m)")]
        [Min(0f)] [SerializeField] float jitter = 0.18f;
        [Tooltip("線寬 (m)")]
        [Min(0.001f)] [SerializeField] float width = 0.05f;
        [Tooltip("多久重新隨機一次形狀（秒）")]
        [Min(0.01f)] [SerializeField] float flickerInterval = 0.06f;
        [Tooltip("每次重新隨機時，一條電弧出現的機率（強度 1 時）")]
        [Range(0f, 1f)] [SerializeField] float showChance = 0.7f;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        LineRenderer[] lines;
        MaterialPropertyBlock block;
        Vector3[] buffer;
        float intensity, timer;

        public float Intensity => intensity;

        void Awake()
        {
            block = new MaterialPropertyBlock();
            buffer = new Vector3[points];
            lines = new LineRenderer[arcCount];
            for (int i = 0; i < arcCount; i++)
            {
                var go = new GameObject($"Arc{i}");
                go.transform.SetParent(transform, false);
                var lr = go.AddComponent<LineRenderer>();
                lr.sharedMaterial = material;
                lr.useWorldSpace = false;
                lr.positionCount = points;
                lr.widthMultiplier = width;
                lr.numCapVertices = 0;
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lr.receiveShadows = false;
                lr.enabled = false;
                lines[i] = lr;
            }
        }

        /// <summary>強度 0..1：亮度與同時出現的條數；0 = 全部隱藏。</summary>
        public void SetIntensity(float value)
        {
            float next = Mathf.Clamp01(value);
            if (Mathf.Approximately(next, intensity)) return;
            bool wasOff = intensity <= 0f;
            intensity = next;
            if (intensity <= 0f) HideAll();
            else if (wasOff) Regenerate();
        }

        /// <summary>圓柱半徑 (m)（例如跟著魔法陣變大）。</summary>
        public void SetRadius(float r) => radius = Mathf.Max(0f, r);

        void Update()
        {
            if (intensity <= 0f) return;
            timer += Time.deltaTime;
            if (timer >= flickerInterval) Regenerate();
        }

        void Regenerate()
        {
            timer = 0f;
            float chance = showChance * intensity;
            Color c = color * intensity;
            foreach (var lr in lines)
            {
                bool show = Random.value < chance;
                lr.enabled = show;
                if (!show) continue;
                Shape(lr);
                lr.GetPropertyBlock(block);
                block.SetColor(BaseColorId, c);
                lr.SetPropertyBlock(block);
            }
        }

        void Shape(LineRenderer lr)
        {
            float start = Random.Range(0f, 360f);
            float sweep = layout == ArcLayout.Wrap ? wrapAngle * (Random.value < 0.5f ? -1f : 1f) : 0f;
            float h = layout == ArcLayout.Wrap ? height : Random.Range(height * 0.5f, height);
            for (int i = 0; i < points; i++)
            {
                float t = points > 1 ? i / (float)(points - 1) : 0f;
                float a = (start + sweep * t) * Mathf.Deg2Rad;
                // 兩端貼在圓柱面上，中間亂跳
                float j = (i == 0 || i == points - 1) ? 0f : jitter;
                float r = radius + Random.Range(-j, j);
                Vector3 p = new Vector3(Mathf.Sin(a) * r, baseHeight + h * t, Mathf.Cos(a) * r);
                p += new Vector3(Random.Range(-j, j), 0f, Random.Range(-j, j));
                buffer[i] = p;
            }
            lr.SetPositions(buffer);
        }

        void HideAll()
        {
            if (lines == null) return;
            foreach (var lr in lines) lr.enabled = false;
        }
    }
}
