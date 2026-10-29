using System;
using BossFight.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BossFight.Progression
{
    /// <summary>
    /// 換場景（T17）：休息區、Boss 戰、序章都放一個（開機分流在 Boot 場景的 BootRouter）。GoToBattle（休息區的傳送門）、ReturnFromBattle（勝敗畫面與 ESC 選單的「返回休息區」）、
    /// ResetProfile（開發者模式的「重置存檔」：清空存檔、回到休息區的啟動出生點）。
    /// 換場景：黑幕用真實時間淡出（暫停中也會動），全黑時解除暫停、timeScale 回 1，把出生點 id 交給 ProfileStore，再載入新場景；換場景中的其他請求不理。
    /// 新場景的 PlayerSpawner 在 Awake 依出生點 id 生成玩家（T18），SceneFlow 在 Start 取走出生點，從全黑淡入；
    /// 直接進 Play 或重試（BattleManager.Retry）不是換場景進來的，不淡入。
    /// 黑幕是全螢幕黑色 Image 的 CanvasGroup（SceneFlowBuilder 建立，sortingOrder 1000），淡出時擋住滑鼠。
    /// 淡入淡出累加每幀的真實時間，但一幀最多算 MaxFadeStep 秒：載入新場景後的頭幾幀常卡上半秒，直接用時間差的話淡入會整段被吃掉。
    /// Battle 與 Hub 互不引用，兩邊都只靠這裡換場景。
    /// </summary>
    [DefaultExecutionOrder(-1100)]
    [DisallowMultipleComponent]
    public class SceneFlow : MonoBehaviour
    {
        const float MaxFadeStep = 1f / 30f; // 純視覺：一幀最多推進的淡入淡出秒數

        [SerializeField] SceneFlowData data;
        [Tooltip("黑幕：全螢幕黑色 Image 的 CanvasGroup")]
        [SerializeField] CanvasGroup fade;
        [Tooltip("換場景前解除暫停（ESC 選單開著時）；可留空")]
        [SerializeField] GamePause pause;
        [SerializeField] GameOptions options;
        [Tooltip("只在序章打開：開發者模式勾了「略過序章」時直接回休息區（保留序章未完成）")]
        [SerializeField] bool prologueSkippable;

        public event Action<bool> OnLoadingChanged;
        /// <summary>Start 取走 arrival 後送出；訂閱方在 OnEnable 訂閱，不要另行 TakeArrival。</summary>
        public event Action<string> OnArrived;

        /// <summary>換場景中（淡出到載入新場景之間）。</summary>
        public bool IsLoading { get; private set; }

        string targetScene, targetArrival;
        float fadeElapsed, fadeFrom, fadeTo;
        float fadeDuration = -1f; // < 0 = 沒有在淡入淡出
        bool holdArrivalFade;

        void Awake()
        {
            SetFade(ProfileStore.PeekArrival(out _) ? 1f : 0f);
        }

        void OnEnable()
        {
            if (!prologueSkippable || options == null) return;
            options.OnSkipPrologueChanged += HandleSkipOption;
            options.OnDeveloperModeChanged += HandleSkipOption;
        }

        void OnDisable()
        {
            if (options == null) return;
            options.OnSkipPrologueChanged -= HandleSkipOption;
            options.OnDeveloperModeChanged -= HandleSkipOption;
        }

        // 序章裡在 ESC 選單勾「略過序章」（或開著它打開開發者模式）時直接回休息區
        void HandleSkipOption(bool _)
        {
            if (prologueSkippable) SkipPrologue();
        }

        void Start()
        {
            if (!ProfileStore.TakeArrival(out string arrival)) return;
            // 從全黑淡入
            SetFade(1f);
            OnArrived?.Invoke(arrival);
            if (!holdArrivalFade) RevealArrival();
        }

        /// <summary>T30 醒來演出可在 OnArrived 中保留黑幕，再以 RevealArrival 交還淡入。</summary>
        public void HoldArrivalFade() => holdArrivalFade = true;
        /// <summary>醒來 Timeline 已鋪好自己的黑幕後同步移交；Start 不再自動淡入。</summary>
        public void TransferArrivalFade()
        { holdArrivalFade = true; fadeDuration = -1f; SetFade(0f); }

        public void RevealArrival()
        {
            holdArrivalFade = false;
            BeginFade(0f, data != null ? data.fadeInTime : 0f);
        }

        void Update()
        {
            if (fadeDuration < 0f) return;
            fadeElapsed += Mathf.Min(Time.unscaledDeltaTime, MaxFadeStep);
            float t = fadeDuration > 0f ? Mathf.Clamp01(fadeElapsed / fadeDuration) : 1f;
            SetFade(Mathf.Lerp(fadeFrom, fadeTo, t));
            if (t < 1f) return;
            fadeDuration = -1f;
            if (IsLoading) LoadTarget();
        }

        /// <summary>從休息區進 Boss 戰（傳送門）：scene 是 BossDefinition.sceneName；空的時候用 SceneFlowData.battleScene。</summary>
        public void GoToBattle(string scene = null)
        {
            if (data != null) Load(string.IsNullOrEmpty(scene) ? data.battleScene : scene, string.Empty);
        }

        /// <summary>從 Boss 戰回休息區，出生在傳送門前。</summary>
        public void ReturnFromBattle()
        {
            if (data != null) Load(data.hubScene, data.portalArrival);
        }

        /// <summary>序章結局演出寫完存檔後返回 Hub；此方法本身不寫完成紀錄。</summary>
        public void ReturnFromPrologue(bool wake)
        {
            if (data != null) Load(data.hubScene, wake ? data.wakeArrival : data.startArrival);
        }

        /// <summary>開發者略過，回 Hub 一般出生點，保留序章未完成狀態。</summary>
        public void SkipPrologue()
        {
            if (options != null && options.CanSkipPrologue) ReturnFromPrologue(false);
        }

        /// <summary>清空存檔，回到休息區的啟動出生點（開發者模式的「重置存檔」）。</summary>
        public void ResetProfile()
        {
            if (data == null || IsLoading) return;
            ProfileStore.ResetProfile();
            Load(data.hubScene, data.startArrival);
        }

        void Load(string scene, string arrival)
        {
            if (IsLoading || string.IsNullOrEmpty(scene)) return;
            IsLoading = true;
            targetScene = scene;
            targetArrival = arrival;
            OnLoadingChanged?.Invoke(true);
            BeginFade(1f, data.fadeOutTime);
        }

        void LoadTarget()
        {
            if (pause != null) pause.SetPaused(false);
            Time.timeScale = 1f; // 慢動作、畫面停頓中離開時不要把 timeScale 帶到新場景
            ProfileStore.SetArrival(targetArrival);
            SceneManager.LoadScene(targetScene);
        }

        void BeginFade(float to, float duration)
        {
            fadeElapsed = 0f;
            fadeFrom = fade != null ? fade.alpha : to;
            fadeTo = to;
            fadeDuration = Mathf.Max(0f, duration);
            if (fade != null) fade.blocksRaycasts = to > 0f || IsLoading;
        }

        void SetFade(float alpha)
        {
            if (fade == null) return;
            fade.alpha = alpha;
            fade.blocksRaycasts = alpha > 0f || IsLoading;
        }
    }
}
