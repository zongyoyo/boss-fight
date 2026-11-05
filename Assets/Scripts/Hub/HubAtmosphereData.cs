using UnityEngine;

namespace BossFight.Hub
{
    /// <summary>
    /// 休息區的天色（T20）：傍晚暖色的太陽與環境光、薄霧、營火光。全是視覺數值；HubAtmosphere 在 Awake 套用（Builder 建場景時也先套一次），
    /// 營火光的閃爍由 FireFlicker 讀。資產在 Assets/ScriptableObjects/Hub/HubAtmosphere.asset。
    /// </summary>
    [CreateAssetMenu(fileName = "HubAtmosphere", menuName = "BossFight/Hub/Hub Atmosphere Data")]
    public class HubAtmosphereData : ScriptableObject
    {
        [Header("天空與太陽")]
        [Tooltip("天空盒（美術 Phase 6 起是三色漸層 Sky_HubDusk：天頂色、地平線色 = 霧色、地平線以下；Tools > Hub > Apply Atmosphere 寫入顏色）")]
        public Material skybox;
        [Tooltip("漸層天空的天頂色")]
        public Color skyTopColor = new Color(0.3f, 0.32f, 0.5f);
        [Tooltip("漸層天空地平線以下的顏色")]
        public Color skyBottomColor = new Color(0.3f, 0.25f, 0.24f);
        public Color sunColor = new Color(1f, 0.66f, 0.42f);
        [Min(0f)] public float sunIntensity = 0.9f;
        [Tooltip("太陽的角度（仰角, 方位）：傍晚的低角度")]
        public Vector2 sunAngles = new Vector2(16f, 250f);

        [Header("環境光（Trilight）")]
        public Color ambientSky = new Color(0.5f, 0.42f, 0.5f);
        public Color ambientEquator = new Color(0.55f, 0.38f, 0.3f);
        public Color ambientGround = new Color(0.18f, 0.15f, 0.12f);

        [Header("薄霧")]
        public bool fog = true;
        public Color fogColor = new Color(0.62f, 0.45f, 0.4f);
        [Tooltip("指數霧的濃度")]
        [Min(0f)] public float fogDensity = 0.018f;

        [Header("營火光")]
        public Color fireColor = new Color(1f, 0.55f, 0.22f);
        [Min(0f)] public float fireIntensity = 6f;
        [Min(0f)] public float fireRange = 9f;
        [Tooltip("閃爍幅度（強度的比例）")]
        [Range(0f, 1f)] public float fireFlicker = 0.25f;
        [Tooltip("閃爍速度（Perlin 雜訊每秒前進多少）")]
        [Min(0f)] public float fireFlickerSpeed = 6f;

        [Header("營火的火焰片（美術 Phase 6）")]
        [Tooltip("火焰片縮放閃爍的幅度（比例；高度晃這麼多，寬度晃一半）")]
        [Range(0f, 1f)] public float flameFlicker = 0.18f;
        [Tooltip("火焰片閃爍的速度（Perlin 雜訊每秒前進多少）")]
        [Min(0f)] public float flameFlickerSpeed = 5f;

        [Header("爐火光（鐵匠鋪）")]
        public Color forgeColor = new Color(1f, 0.42f, 0.15f);
        [Min(0f)] public float forgeIntensity = 3f;
        [Min(0f)] public float forgeRange = 5f;
    }
}
