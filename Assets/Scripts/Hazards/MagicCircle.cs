using BossFight.Core;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace BossFight.Hazards
{
    /// <summary>
    /// 場地中央的邪惡魔法陣：URP Decal Projector 投在地上，圖案由 Shaders/MagicCircleDecal 用 UV 畫出（外圈雙環與刻度、符文帶、倒五芒星、內圈、中心）。
    /// 出現方式分兩段：先由中心往外描繪到外圈內側（drawProgress），再把外圈分 segments 段依序點亮（segmentProgress）；
    /// 開場第 2 鏡由 Timeline 的 MagicCircleTrack 推進度，SetRevealProgress 把一個 0..1 的進度依 drawPortion 拆成這兩段。
    /// 脈動（SetPulse）是隨節拍一下一下變亮再衰減，第 6 鏡音樂開始後打開，之後戰鬥中一直維持。
    /// 半徑讀 ArenaData.magicCircleRadius；轉階段的「由紫轉紅、擴大到場地邊緣」由 Timeline 的 MagicCircleTrack 推 SetColorBlend（0 紫 → 1 紅）
    /// 與 SetExpand（0 原本半徑 → 1 場地邊緣），第二階段維持紅色、全場大小。
    /// 每個實例用自己的材質複本。這裡的數字全是視覺參數。
    /// </summary>
    [DisallowMultipleComponent]
    public class MagicCircle : MonoBehaviour
    {
        [SerializeField] DecalProjector projector;
        [Tooltip("半徑讀 magicCircleRadius")]
        [SerializeField] ArenaData arena;

        [Header("外觀")]
        [Tooltip("顏色（HDR，A 是不透明度）")]
        [SerializeField] [ColorUsage(true, true)] Color color = new Color(0.62f, 0.18f, 1f, 1f);
        [Tooltip("第二階段的顏色（SetColorBlend = 1）")]
        [SerializeField] [ColorUsage(true, true)] Color phase2Color = new Color(1f, 0.1f, 0.06f, 1f);
        [Tooltip("SetExpand = 1 時外圈離場地外牆多遠 (m)；留一點才不會染到牆腳")]
        [Min(0f)] [SerializeField] float expandedEdgeInset = 0.25f;
        [Tooltip("線寬 (m)")]
        [Min(0.005f)] [SerializeField] float lineWidth = 0.07f;
        [Tooltip("發光強度")]
        [Min(0f)] [SerializeField] float emission = 2.5f;
        [Tooltip("外圈分幾段點亮")]
        [Range(1, 32)] [SerializeField] int segments = 8;
        [Tooltip("投影深度 (m)：一半在地面上、一半在地面下")]
        [Min(0.05f)] [SerializeField] float depth = 0.3f;

        [Header("出現")]
        [Tooltip("SetRevealProgress 的進度裡，前面多少比例用來從中心往外描繪；剩下的用來點亮外圈各段")]
        [Range(0.05f, 0.95f)] [SerializeField] float drawPortion = 0.68f;

        [Header("脈動")]
        [Tooltip("每秒脈動幾下（OIIA 音樂的 BPM ÷ 60；音樂放進來後再對）")]
        [Min(0.01f)] [SerializeField] float pulseRate = 2f;
        [Tooltip("每一下最亮時額外增加的亮度（倍率）")]
        [Min(0f)] [SerializeField] float pulseStrength = 1.2f;
        [Tooltip("每一下的衰減速度（越大越短促）")]
        [Min(0.1f)] [SerializeField] float pulseDecay = 6f;

        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int ParamsId = Shader.PropertyToID("_Params");
        static readonly int StyleId = Shader.PropertyToID("_Style");
        static readonly int SizeId = Shader.PropertyToID("_Size");

        Material material;
        float draw, lit, pulseWeight, pulseTime, radius, colorBlend;

        /// <summary>目前半徑 (m)。</summary>
        public float Radius => radius;
        /// <summary>描繪進度 0..1。</summary>
        public float DrawProgress => draw;
        /// <summary>外圈點亮進度 0..1。</summary>
        public float SegmentProgress => lit;

        void Awake()
        {
            material = new Material(projector.material);
            projector.material = material;
            radius = arena != null ? arena.magicCircleRadius : projector.size.x * 0.5f;
            ApplySize();
            ApplyColor();
            // 預設是開場結束後的樣子（全亮、脈動）；有開場時第一幀就被 Timeline 蓋成看不見
            SetReveal(1f, 1f);
            SetPulse(1f);
            Apply();
        }

        void OnDestroy()
        {
            if (material != null) Destroy(material);
        }

        /// <summary>出現進度 0..1：前 drawPortion 由中心往外描繪，之後外圈逐段點亮。</summary>
        public void SetRevealProgress(float t)
        {
            t = Mathf.Clamp01(t);
            SetReveal(t / drawPortion, (t - drawPortion) / (1f - drawPortion));
        }

        /// <summary>分別設定描繪進度與外圈點亮進度（0..1）。</summary>
        public void SetReveal(float drawProgress, float segmentProgress)
        {
            draw = Mathf.Clamp01(drawProgress);
            lit = Mathf.Clamp01(segmentProgress);
        }

        /// <summary>隨節拍脈動的強度 0..1（0 = 不脈動）。</summary>
        public void SetPulse(float weight) => pulseWeight = Mathf.Clamp01(weight);

        public void SetColor(Color c)
        {
            color = c;
            ApplyColor();
        }

        /// <summary>顏色由原本的顏色（0，紫）漸變到第二階段的顏色（1，紅）。</summary>
        public void SetColorBlend(float t)
        {
            colorBlend = Mathf.Clamp01(t);
            ApplyColor();
        }

        /// <summary>半徑由原本的大小（0，ArenaData.magicCircleRadius）擴大到場地邊緣（1，ArenaData.radius）。</summary>
        public void SetExpand(float t)
        {
            if (arena == null) return;
            float edge = Mathf.Max(arena.magicCircleRadius, arena.radius - expandedEdgeInset);
            SetRadius(Mathf.Lerp(arena.magicCircleRadius, edge, Mathf.Clamp01(t)));
        }

        void ApplyColor() => material.SetColor(ColorId, Color.Lerp(color, phase2Color, colorBlend));

        public void SetRadius(float r)
        {
            radius = Mathf.Max(0.1f, r);
            ApplySize();
        }

        void Update()
        {
            pulseTime += Time.deltaTime;
            Apply();
        }

        void ApplySize()
        {
            // 投影器朝下（+Z = 世界下方），+Y 朝北；UV 的 U 朝東、V 朝北
            transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
            projector.size = new Vector3(radius * 2f, radius * 2f, depth);
            projector.pivot = Vector3.zero;
            material.SetVector(SizeId, new Vector4(radius * 2f, 0f, 0f, 0f));
        }

        void Apply()
        {
            bool visible = draw > 0f || lit > 0f;
            projector.enabled = visible;
            if (!visible) return;

            float beat = Mathf.Repeat(pulseTime * pulseRate, 1f);
            float intensity = 1f + pulseWeight * pulseStrength * Mathf.Exp(-beat * pulseDecay);
            material.SetVector(ParamsId, new Vector4(draw, lit, intensity, 0f));
            material.SetVector(StyleId, new Vector4(lineWidth, 1f, emission, segments));
        }
    }
}
