using UnityEngine;
using UnityEngine.Rendering;

namespace BossFight.Hub
{
    /// <summary>
    /// 套用休息區的天色（HubAtmosphereData）：天空盒、太陽、Trilight 環境光、指數霧。Awake 時套用一次，改資產後重進 Play 就會生效；
    /// HubBuilder 建場景時也呼叫 Apply，編輯模式看到的跟執行時一樣。
    /// </summary>
    [DisallowMultipleComponent]
    public class HubAtmosphere : MonoBehaviour
    {
        [SerializeField] HubAtmosphereData data;
        [Tooltip("主光源（太陽）")]
        [SerializeField] Light sun;

        void Awake() => Apply();

        public void Apply()
        {
            if (data == null) return;
            if (data.skybox != null) RenderSettings.skybox = data.skybox;
            if (sun != null)
            {
                sun.color = data.sunColor;
                sun.intensity = data.sunIntensity;
                sun.transform.rotation = Quaternion.Euler(data.sunAngles.x, data.sunAngles.y, 0f);
                RenderSettings.sun = sun;
            }
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = data.ambientSky;
            RenderSettings.ambientEquatorColor = data.ambientEquator;
            RenderSettings.ambientGroundColor = data.ambientGround;
            RenderSettings.fog = data.fog;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = data.fogColor;
            RenderSettings.fogDensity = data.fogDensity;
        }
    }
}
