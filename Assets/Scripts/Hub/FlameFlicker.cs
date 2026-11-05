using UnityEngine;

namespace BossFight.Hub
{
    /// <summary>
    /// 營火模型的火焰片閃爍（美術 Phase 6）：每一層火焰片各自用 Perlin 雜訊縮放，高度晃 flameFlicker、寬度晃一半；
    /// 幅度與速度在 HubAtmosphereData。純視覺，HubSceneryBuilder 掛在營火模型上並接好火焰片。
    /// </summary>
    [DisallowMultipleComponent]
    public class FlameFlicker : MonoBehaviour
    {
        [SerializeField] HubAtmosphereData data;
        [Tooltip("火焰片（軸心在火焰底部）")]
        [SerializeField] Transform[] flames;

        Vector3[] baseScales;
        float[] seeds;

        void Awake()
        {
            baseScales = new Vector3[flames.Length];
            seeds = new float[flames.Length];
            for (int i = 0; i < flames.Length; i++)
            {
                baseScales[i] = flames[i] != null ? flames[i].localScale : Vector3.one;
                seeds[i] = Random.value * 100f;
            }
        }

        void Update()
        {
            if (data == null) return;
            float t = Time.time * data.flameFlickerSpeed;
            for (int i = 0; i < flames.Length; i++)
            {
                if (flames[i] == null) continue;
                float tall = Mathf.PerlinNoise(seeds[i], t) * 2f - 1f;
                float wide = Mathf.PerlinNoise(seeds[i] + 37f, t) * 2f - 1f;
                float h = 1f + tall * data.flameFlicker;
                float w = 1f + wide * data.flameFlicker * 0.5f;
                var s = baseScales[i];
                flames[i].localScale = new Vector3(s.x * w, s.y * h, s.z * w);
            }
        }
    }
}
