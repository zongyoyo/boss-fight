using UnityEngine;

namespace BossFight.Cutscene
{
    /// <summary>
    /// 開場劈中法陣中央的那道閃電：從天上到地面的閃電柱加上一盞閃光燈。CurveTrack 的值是亮度（0 = 看不見，曲線做出閃爍）。
    /// 顏色用 MaterialPropertyBlock 改 _BaseColor（和落雷的 Fx_Bolt 同一個加算材質）。全是視覺參數。
    /// </summary>
    [DisallowMultipleComponent]
    public class IntroLightning : CurveTarget
    {
        [SerializeField] Renderer[] bolts;
        [SerializeField] Light flashLight;
        [SerializeField] [ColorUsage(true, true)] Color color = new Color(3f, 2.6f, 4f, 1f);
        [Tooltip("亮度 1 時燈的強度")]
        [Min(0f)] [SerializeField] float lightIntensity = 60f;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        MaterialPropertyBlock block;

        void Awake()
        {
            block = new MaterialPropertyBlock();
            Apply(0f);
        }

        public override void Apply(float value)
        {
            bool on = value > 1e-3f;
            Color c = color * value;
            c.a = Mathf.Clamp01(color.a * value);
            foreach (var r in bolts)
            {
                if (r == null) continue;
                r.enabled = on;
                r.GetPropertyBlock(block);
                block.SetColor(BaseColorId, c);
                r.SetPropertyBlock(block);
            }
            if (flashLight != null)
            {
                flashLight.enabled = on;
                float max = Mathf.Max(color.maxColorComponent, 1e-3f);
                flashLight.color = max > 1f ? color / max : color;
                flashLight.intensity = lightIntensity * value;
            }
        }
    }
}
