using UnityEngine;

namespace BossFight.Cutscene
{
    /// <summary>
    /// 閃電劈下後包住貓的煙霧（佔位：幾顆半透明灰球）。CurveTrack 的值是散去的進度：0 看不見，剛大於 0 時最濃，
    /// 往 1 推進時往外飄、往上升、變大、變淡，到 1 消失。每顆球場景裡的位置是一開始的位置。全是視覺參數。
    /// </summary>
    [DisallowMultipleComponent]
    public class SmokePuffs : CurveTarget
    {
        [SerializeField] Renderer[] puffs;
        [Tooltip("顏色（A 是最濃時的不透明度）")]
        [SerializeField] Color color = new Color(0.32f, 0.31f, 0.36f, 0.9f);
        [Tooltip("散去時大小變成幾倍")]
        [Min(0f)] [SerializeField] float endScale = 2.2f;
        [Tooltip("散去時往外飄多遠 (m)")]
        [Min(0f)] [SerializeField] float spread = 2.5f;
        [Tooltip("散去時往上升多高 (m)")]
        [Min(0f)] [SerializeField] float rise = 1.5f;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        MaterialPropertyBlock block;
        Vector3[] home;
        Vector3[] scale;

        void Awake()
        {
            block = new MaterialPropertyBlock();
            home = new Vector3[puffs.Length];
            scale = new Vector3[puffs.Length];
            for (int i = 0; i < puffs.Length; i++)
            {
                home[i] = puffs[i].transform.localPosition;
                scale[i] = puffs[i].transform.localScale;
            }
            Apply(0f);
        }

        public override void Apply(float value)
        {
            bool on = value > 0f && value < 1f;
            float t = Mathf.Clamp01(value);
            Color c = color;
            c.a = color.a * (1f - t) * (1f - t);
            for (int i = 0; i < puffs.Length; i++)
            {
                var r = puffs[i];
                r.enabled = on;
                if (!on) continue;
                Vector3 outward = new Vector3(home[i].x, 0f, home[i].z);
                outward = outward.sqrMagnitude > 1e-4f ? outward.normalized : Vector3.zero;
                r.transform.localPosition = home[i] + outward * (spread * t) + Vector3.up * (rise * t);
                r.transform.localScale = scale[i] * Mathf.Lerp(1f, endScale, t);
                r.GetPropertyBlock(block);
                block.SetColor(BaseColorId, c);
                r.SetPropertyBlock(block);
            }
        }
    }
}
