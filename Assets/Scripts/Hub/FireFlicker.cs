using UnityEngine;

namespace BossFight.Hub
{
    /// <summary>營火光的閃爍：同物件的點光源強度用 Perlin 雜訊在 HubAtmosphereData 的 fireIntensity 上下晃。純視覺。</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Light))]
    public class FireFlicker : MonoBehaviour
    {
        [SerializeField] HubAtmosphereData data;

        Light fireLight;
        float seed;

        void Awake()
        {
            fireLight = GetComponent<Light>();
            seed = Random.value * 100f;
            if (data == null) return;
            fireLight.color = data.fireColor;
            fireLight.range = data.fireRange;
        }

        void Update()
        {
            if (data == null) return;
            float noise = Mathf.PerlinNoise(seed, Time.time * data.fireFlickerSpeed) * 2f - 1f;
            fireLight.intensity = data.fireIntensity * (1f + noise * data.fireFlicker);
        }
    }
}
