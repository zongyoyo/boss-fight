using UnityEngine;

namespace BossFight.Cutscene
{
    /// <summary>
    /// 演出用的無傷害衝擊波（轉階段第 5 步）：CurveTrack 的值是擴散進度，0 看不見，(0, 1] 時環牆從 startRadius 長到 endRadius、越外面越淡。
    /// 外觀和招式的衝擊波一樣（半徑 1、高 1 的開口圓柱環牆，加算材質），但沒有判定；把玩家推開的是 ActorPushTrack。
    /// 這裡的數字全是視覺參數；endRadius 由 Builder 設成場地半徑。
    /// </summary>
    [DisallowMultipleComponent]
    public class ExpandingRing : CurveTarget
    {
        [Tooltip("環牆（半徑 1、高 1 的開口圓柱），依半徑與高度縮放")]
        [SerializeField] Transform wall;
        [SerializeField] Renderer wallRenderer;
        [SerializeField] [ColorUsage(true, true)] Color color = new Color(3f, 0.4f, 0.25f, 1f);
        [Tooltip("開始與結束的半徑 (m)")]
        [Min(0f)] [SerializeField] float startRadius = 1.2f;
        [Min(0f)] [SerializeField] float endRadius = 12f;
        [Tooltip("環牆高度 (m)")]
        [Min(0.05f)] [SerializeField] float height = 1.2f;
        [Tooltip("擴散到最後時剩下的亮度比例")]
        [Range(0f, 1f)] [SerializeField] float endIntensity = 0.15f;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        MaterialPropertyBlock block;

        void Awake()
        {
            block = new MaterialPropertyBlock();
            Apply(0f);
        }

        public override void Apply(float value)
        {
            bool visible = value > 0f;
            wallRenderer.enabled = visible;
            if (!visible) return;

            float t = Mathf.Clamp01(value);
            float r = Mathf.Lerp(startRadius, endRadius, t);
            wall.localScale = new Vector3(r, height, r);
            float k = Mathf.Lerp(1f, endIntensity, t);
            Color c = color * k;
            c.a = color.a * k;
            wallRenderer.GetPropertyBlock(block);
            block.SetColor(BaseColorId, c);
            wallRenderer.SetPropertyBlock(block);
        }
    }
}
