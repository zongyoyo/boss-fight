using UnityEngine;

namespace BossFight.Core
{
    /// <summary>
    /// 競技場的天色：主光源、環境光（Trilight）、天空盒與雷雲，依暴風進度在 AtmosphereData 的「平靜」與「暴風雨」之間插值。
    /// 開場第 3 鏡由 Timeline 的 StormTrack 把進度從 0 推到 1；沒有開場時（或開場結束後）維持 1。
    /// 雷雲是子物件，場景裡擺的位置就是聚集後的位置；進度 0 時往外推到 cloudScatter 倍遠、縮小，進度 1 時回到原位。
    /// 天空盒用執行期的複本插值，不會改到材質資產。全是視覺效果，沒有判定。
    /// </summary>
    [DisallowMultipleComponent]
    public class ArenaAtmosphere : MonoBehaviour
    {
        [SerializeField] AtmosphereData data;
        [Tooltip("主光源（Directional Light）")]
        [SerializeField] Light sun;
        [Tooltip("雷雲；場景裡的位置是聚集後的位置（以這個物件為場地中心）")]
        [SerializeField] Transform[] clouds;

        /// <summary>目前的暴風進度：0 = 平靜，1 = 暴風雨。</summary>
        public float Storm { get; private set; } = -1f;

        Material sky;
        Vector3[] cloudHome;
        Vector3[] cloudScale;

        void Awake()
        {
            if (data == null || data.calm.skybox == null || data.storm.skybox == null)
            {
                Debug.LogError("[Atmosphere] AtmosphereData 或兩組天空盒沒有設定。", this);
                enabled = false;
                return;
            }
            sky = new Material(data.storm.skybox) { name = "Sky_Runtime" };
            RenderSettings.skybox = sky;

            int n = clouds != null ? clouds.Length : 0;
            cloudHome = new Vector3[n];
            cloudScale = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                if (clouds[i] == null) continue;
                cloudHome[i] = clouds[i].localPosition;
                cloudScale[i] = clouds[i].localScale;
            }
            SetStorm(1f);
        }

        void OnDestroy()
        {
            if (sky != null) Destroy(sky);
        }

        /// <summary>暴風進度 0..1。</summary>
        public void SetStorm(float t)
        {
            if (!enabled) return;
            t = Mathf.Clamp01(t);
            if (Mathf.Approximately(t, Storm)) return;
            Storm = t;

            var a = data.calm;
            var b = data.storm;
            if (sun != null)
            {
                sun.color = Color.Lerp(a.sunColor, b.sunColor, t);
                sun.intensity = Mathf.Lerp(a.sunIntensity, b.sunIntensity, t);
            }
            RenderSettings.ambientSkyColor = Color.Lerp(a.ambientSky, b.ambientSky, t);
            RenderSettings.ambientEquatorColor = Color.Lerp(a.ambientEquator, b.ambientEquator, t);
            RenderSettings.ambientGroundColor = Color.Lerp(a.ambientGround, b.ambientGround, t);
            sky.Lerp(a.skybox, b.skybox, t);

            for (int i = 0; i < cloudHome.Length; i++)
            {
                var cloud = clouds[i];
                if (cloud == null) continue;
                Vector3 home = cloudHome[i];
                Vector3 far = new Vector3(home.x * data.cloudScatter, home.y, home.z * data.cloudScatter);
                cloud.localPosition = Vector3.Lerp(far, home, t);
                cloud.localScale = cloudScale[i] * Mathf.Lerp(data.cloudStartScale, 1f, t);
            }
        }
    }
}
