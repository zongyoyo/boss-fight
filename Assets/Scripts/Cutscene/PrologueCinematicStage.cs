using BossFight.Core;
using BossFight.Player;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace BossFight.Cutscene
{
    /// <summary>
    /// 序章演出（守衛登場、轉階段、擊倒、勝利、休息區醒來）的 Timeline 表現接收者（CinematicValueTrack 綁這裡）：守衛模型的升起與樹冠枯萎、裂縫亮度、
    /// 天色、全螢幕閃光與黑幕、後處理（獨立的 Volume 複本）、低通、粒子、玩家躺姿與推開。只改表現，不參與戰鬥與存檔；數值都在 PrologueCutsceneData。
    /// </summary>
    [DefaultExecutionOrder(950)]
    public class PrologueCinematicStage : MonoBehaviour, IPlayerBinding
    {
        [SerializeField] PrologueCutsceneData data;
        [SerializeField] Transform guardianModel;
        [SerializeField] Transform guardianCrown;
        [SerializeField] Transform guardianOrigin;
        [SerializeField] Renderer[] cracks = new Renderer[0];
        [SerializeField] Light sun;
        [SerializeField] Volume volume;
        [SerializeField] CanvasGroup redFlash, whiteFlash, black, title;
        [SerializeField] Text saveFailure;
        public void ShowSaveFailure(bool visible) { if (saveFailure != null) saveFailure.gameObject.SetActive(visible); }
        [SerializeField] CinemachineBasicMultiChannelPerlin[] noise = new CinemachineBasicMultiChannelPerlin[0];
        [SerializeField] ParticleSystem debris, birds;
        [SerializeField] Transform wave;
        [SerializeField] GameObject groundCracks;
        [Tooltip("北側露出地面的焦黑樹根（H7 伏筆）；守衛破土（Rise > 0）時收掉")]
        [SerializeField] GameObject foreshadowRoots;
        [SerializeField] AudioLowPassFilter[] filters = new AudioLowPassFilter[0];
        [SerializeField] GamePause pause;
        [SerializeField] HitStop hitStop;
        [Tooltip("每次播放依玩家位置重擺的鏡頭舞台（擊倒／醒來）")]
        [SerializeField] Transform playerStage;
        PlayerRig rig;
        VolumeProfile runtimeProfile;
        DepthOfField dof;
        Vignette vignette;
        ColorAdjustments colors;
        MaterialPropertyBlock block;
        Vector3 modelPosition, crownScale;
        Color sunColor, fogColor, ambient;
        float sunIntensity, fogDensity, wither, darkness, rise = 1f, glow = 1f;
        float[] filterCutoffs;
        bool[] filterEnabled;
        bool initialized, ownsScale, debrisOn, birdsOn;
        float previousScale = 1f;
        public void BindPlayer(PlayerRig value) => rig = value;
        void Awake()
        {
            block = new MaterialPropertyBlock();
            if (guardianModel != null) modelPosition = guardianModel.localPosition;
            if (guardianCrown != null) crownScale = guardianCrown.localScale;
            if (sun != null) { sunColor = sun.color; sunIntensity = sun.intensity; }
            fogColor = RenderSettings.fogColor; fogDensity = RenderSettings.fogDensity; ambient = RenderSettings.ambientLight;
            filterCutoffs = new float[filters.Length]; filterEnabled = new bool[filters.Length];
            for (int i = 0; i < filters.Length; i++) if (filters[i] != null) { filterCutoffs[i] = filters[i].cutoffFrequency; filterEnabled[i] = filters[i].enabled; }
            if (volume != null && volume.sharedProfile != null)
            {
                // Volume.profile 的 getter 會逐一 Instantiate 子元件；只 Instantiate(profile) 會共用子資產。
                runtimeProfile = volume.profile; runtimeProfile.hideFlags = HideFlags.DontSave;
                foreach (var component in runtimeProfile.components) component.hideFlags = HideFlags.DontSave;
                runtimeProfile.TryGet(out dof); runtimeProfile.TryGet(out vignette); runtimeProfile.TryGet(out colors);
            }
            initialized = true; ResetTransient();
            if (groundCracks != null) groundCracks.SetActive(false);
        }
        /// <summary>擊倒與醒來的相對鏡頭在播放前跟著真正玩家，不能留在 EditorOnly 預覽模型的位置。</summary>
        public void AlignToPlayer()
        {
            if (playerStage != null && rig != null) playerStage.SetPositionAndRotation(rig.transform.position, rig.transform.rotation);
        }
        public void Apply(CinematicValue channel, float value)
        {
            if (!initialized) return;
            float t = Mathf.Clamp01(value);
            switch (channel)
            {
                case CinematicValue.Darkness:
                    darkness = t;
                    if (sun != null) { sun.color = Color.Lerp(sunColor, data.darkLightColor, t); sun.intensity = Mathf.Lerp(sunIntensity, data.darkLightIntensity, t); }
                    RenderSettings.fogColor = Color.Lerp(fogColor, data.darkFog, t);
                    RenderSettings.fogDensity = Mathf.Lerp(fogDensity, data.darkFogDensity, t);
                    if (colors != null) { colors.saturation.Override(data.darkSaturation * t); colors.colorFilter.Override(Color.Lerp(Color.white, data.darkColorFilter, t)); }
                    break;
                case CinematicValue.Rise:
                    rise = t;
                    if (guardianModel != null) guardianModel.gameObject.SetActive(t > 0f);
                    if (foreshadowRoots != null) foreshadowRoots.SetActive(t <= 0f);
                    break;
                case CinematicValue.GroundCrack: if (groundCracks != null) groundCracks.SetActive(t > 0f); break;
                case CinematicValue.Wither: wither = t; break;
                case CinematicValue.Glow: glow = t; break;
                case CinematicValue.RedFlash: Set(redFlash, t); break;
                case CinematicValue.WhiteFlash: Set(whiteFlash, t); break;
                case CinematicValue.Black: Set(black, t); break;
                case CinematicValue.Title: Set(title, t); break;
                case CinematicValue.Blur:
                    if (dof != null) { dof.active = t > 0f; dof.mode.Override(DepthOfFieldMode.Bokeh); dof.focusDistance.Override(Mathf.Lerp(data.blurStartFocus, data.blurFocus, t)); dof.aperture.Override(data.blurAperture); dof.focalLength.Override(data.blurFocalLength); }
                    break;
                case CinematicValue.Vignette: if (vignette != null) vignette.intensity.Override(t * data.vignetteIntensity); break;
                case CinematicValue.LowPass:
                    for (int i = 0; i < filters.Length; i++) if (filters[i] != null) { filters[i].enabled = t > 0f || filterEnabled[i]; filters[i].cutoffFrequency = Mathf.Lerp(filterCutoffs[i], data.lowPassFrequency, t); }
                    break;
                case CinematicValue.Lying: if (rig != null && rig.Animator != null) rig.Animator.SetCinematicLying(t); break;
                case CinematicValue.Shake: foreach (var n in noise) if (n != null) n.AmplitudeGain = value; break;
                case CinematicValue.Debris: Toggle(debris, t > 0f, ref debrisOn); break;
                case CinematicValue.Birds: Toggle(birds, t > 0f, ref birdsOn); break;
                case CinematicValue.Wave:
                    if (wave != null) { wave.gameObject.SetActive(t > 0f && t < 1f); wave.localScale = new Vector3(t * data.pushRadius * 2f, data.waveHeight, t * data.pushRadius * 2f); }
                    break;
                case CinematicValue.Push:
                    if (rig != null && guardianOrigin != null)
                    {
                        Vector3 to = guardianOrigin.position + Vector3.back * data.pushRadius; to.y = rig.transform.position.y;
                        // 起點由上層已核定的守衛南側站位取得；在第一個有效片段幀記錄。
                        if (!pushing) { pushing = true; pushFrom = rig.transform.position; }
                        rig.Controller.ScriptedMoveTo(Vector3.Lerp(pushFrom, to, t), guardianOrigin.position - to);
                    }
                    break;
                case CinematicValue.Slow:
                    if (t > 0f && !ownsScale) { previousScale = Time.timeScale > 0f ? Time.timeScale : 1f; ownsScale = true; }
                    if (ownsScale && !(pause != null && pause.IsPaused) && !(hitStop != null && hitStop.IsStopped))
                    { if (t > 0f) Time.timeScale = data.slowScale; else ReleaseScale(); }
                    break;
            }
        }
        bool pushing;
        Vector3 pushFrom;
        void LateUpdate()
        {
            if (!initialized) return;
            if (guardianModel != null) guardianModel.localPosition = modelPosition + Vector3.down * (data.buriedDepth * (1f - rise));
            if (guardianCrown != null && wither > 0f)
            {
                guardianCrown.localScale = Vector3.Scale(crownScale, Vector3.Lerp(Vector3.one, data.witherScale, wither));
                guardianCrown.localRotation = Quaternion.Euler(wither * data.witherTilt, 0f, 0f);
            }
            if (glow < 1f) for (int i = 0; i < cracks.Length; i++)
            {
                var r = cracks[i];
                if (r == null) continue;
                float brightness = Mathf.Clamp01(glow * cracks.Length - i);
                r.GetPropertyBlock(block); block.SetColor("_BaseColor", Color.red * brightness); block.SetColor("_EmissionColor", Color.red * brightness); r.SetPropertyBlock(block);
            }
        }
        static void Set(CanvasGroup g, float t) { if (g != null) { g.alpha = t; g.blocksRaycasts = false; } }
        static void Toggle(ParticleSystem ps, bool on, ref bool was)
        { if (on == was) return; was = on; if (ps == null) return; if (on) ps.Play(); else ps.Stop(true, ParticleSystemStopBehavior.StopEmitting); }
        void ReleaseScale()
        {
            if (!ownsScale) return;
            if (Mathf.Approximately(Time.timeScale, data.slowScale)) Time.timeScale = previousScale;
            ownsScale = false;
        }
        public void ResetTransient()
        {
            if (!initialized) return;
            foreach (var v in new[] { CinematicValue.RedFlash, CinematicValue.WhiteFlash, CinematicValue.Black, CinematicValue.Title, CinematicValue.Blur, CinematicValue.Vignette, CinematicValue.LowPass, CinematicValue.Lying, CinematicValue.Shake, CinematicValue.Debris, CinematicValue.Birds, CinematicValue.Wave }) Apply(v, 0f);
            Apply(CinematicValue.Vignette, darkness * data.battleVignette);
            ReleaseScale(); pushing = false;
        }
        void OnDisable()
        {
            if (!initialized) return;
            ResetTransient();
            // 此場景退出時還原自己改過的全域天色；下一場景會再套自己的天色。
            if (sun != null) { sun.color = sunColor; sun.intensity = sunIntensity; }
            RenderSettings.fogColor = fogColor; RenderSettings.fogDensity = fogDensity; RenderSettings.ambientLight = ambient;
        }
        void OnDestroy()
        {
            if (runtimeProfile == null) return;
            foreach (var component in runtimeProfile.components) if (component != null) Destroy(component);
            Destroy(runtimeProfile);
        }
    }
}
