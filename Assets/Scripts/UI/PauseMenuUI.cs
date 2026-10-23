using BossFight.Battle;
using BossFight.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace BossFight.UI
{
    /// <summary>ESC 選單的頁面。</summary>
    public enum PauseMenuPage
    {
        /// <summary>選單關著。</summary>
        None,
        Main,
        Settings,
        Controls,
        Keyboard,
        Gamepad,
        Volume,
        Confirm,
    }

    /// <summary>
    /// ESC 選單（T13）：Player/Pause（Esc、手把 Start）開關，開場、轉階段、勝敗畫面時都能開。開著時 GamePause 暫停遊戲；
    /// 玩家與鏡頭的輸入、游標由 BattleManager 看 GamePause 處理，Debug 面板由 DeveloperMode 藏起來。
    /// 頁面：主選單（繼續、設定、重新挑戰、退出遊戲）→ 設定（按鍵設定、音量設定、開發者模式）→ 按鍵設定（鍵鼠、控制器，KeyBindingPageUI）／音量設定（主音量、音樂、音效）。
    /// 返回：Esc、Start、手把 B（UI/Cancel）回上一層，在主選單就關閉；改鍵等待按鍵時不算。重新挑戰、退出遊戲先問一次。
    /// 設定值都在 GameOptions：訂閱它的事件更新滑桿與開關，改的時候呼叫它的 Set。開發者模式這一項只在 Editor 與 Development Build 出現（正式 build 沒有 Debug 程式）。
    /// 文字、顏色、改鍵清單在 PauseMenuData；頁面上固定的文字由 PauseMenuBuilder 建立時寫入。
    /// 關掉時把選取還給開選單前的物件（勝敗畫面的按鈕）。
    /// </summary>
    [DisallowMultipleComponent]
    public class PauseMenuUI : MonoBehaviour
    {
        [SerializeField] PauseMenuData data;
        [SerializeField] GameOptions options;
        [SerializeField] GamePause pause;
        [Tooltip("重新挑戰")]
        [SerializeField] BattleManager battle;
        [Tooltip("調音效音量時試聽；可留空")]
        [SerializeField] AudioManager audioManager;
        [SerializeField] EventSystem eventSystem;
        [Tooltip("整個選單（暗幕與所有頁面）")]
        [SerializeField] CanvasGroup group;

        [Header("主選單")]
        [SerializeField] GameObject mainPage;
        [SerializeField] Button resumeButton;
        [SerializeField] Button settingsButton;
        [SerializeField] Button retryButton;
        [SerializeField] Button quitButton;

        [Header("設定")]
        [SerializeField] GameObject settingsPage;
        [SerializeField] Button controlsButton;
        [SerializeField] Button volumeButton;
        [SerializeField] Button developerButton;
        [SerializeField] Text developerLabel;
        [SerializeField] Button settingsBackButton;

        [Header("按鍵設定")]
        [SerializeField] GameObject controlsPage;
        [SerializeField] Button keyboardButton;
        [SerializeField] Button gamepadButton;
        [SerializeField] Button controlsBackButton;
        [SerializeField] KeyBindingPageUI keyboardPage;
        [SerializeField] KeyBindingPageUI gamepadPage;

        [Header("音量設定")]
        [SerializeField] GameObject volumePage;
        [SerializeField] Slider masterSlider;
        [SerializeField] Slider musicSlider;
        [SerializeField] Slider sfxSlider;
        [SerializeField] Text masterValue;
        [SerializeField] Text musicValue;
        [SerializeField] Text sfxValue;
        [SerializeField] Button volumeBackButton;

        [Header("確認")]
        [SerializeField] GameObject confirmPage;
        [SerializeField] Text confirmText;
        [SerializeField] Button confirmYesButton;
        [SerializeField] Button confirmNoButton;

        public PauseMenuPage Page { get; private set; }
        public bool IsOpen => Page != PauseMenuPage.None;

        InputAction pauseAction, cancelAction;
        GameObject previousSelection;
        bool developerMode;
        bool confirmQuit;
        float nextPreviewTime;

        bool Busy => (Page == PauseMenuPage.Keyboard && keyboardPage.IsBusy) || (Page == PauseMenuPage.Gamepad && gamepadPage.IsBusy);

        void Awake()
        {
            var actions = InputSystem.actions;
            if (actions != null)
            {
                pauseAction = actions.FindAction("Player/Pause", true);
                cancelAction = actions.FindAction("UI/Cancel", false);
            }
            else
            {
                Debug.LogError("[PauseMenu] 找不到 Project-wide Input Actions，ESC 選單不能用。", this);
            }

            resumeButton.onClick.AddListener(Close);
            settingsButton.onClick.AddListener(() => ShowPage(PauseMenuPage.Settings, controlsButton));
            retryButton.onClick.AddListener(() => ShowConfirm(false));
            quitButton.onClick.AddListener(() => ShowConfirm(true));
            controlsButton.onClick.AddListener(() => ShowPage(PauseMenuPage.Controls, keyboardButton));
            volumeButton.onClick.AddListener(() => ShowPage(PauseMenuPage.Volume, masterSlider));
            developerButton.onClick.AddListener(ToggleDeveloperMode);
            settingsBackButton.onClick.AddListener(Back);
            // 改鍵頁第一次打開時才 Awake、建立各列，第一格在換頁之後才拿得到（DefaultSelectable）
            keyboardButton.onClick.AddListener(() => ShowPage(PauseMenuPage.Keyboard, null));
            gamepadButton.onClick.AddListener(() => ShowPage(PauseMenuPage.Gamepad, null));
            controlsBackButton.onClick.AddListener(Back);
            volumeBackButton.onClick.AddListener(Back);
            confirmYesButton.onClick.AddListener(Confirm);
            confirmNoButton.onClick.AddListener(Back);
            masterSlider.onValueChanged.AddListener(v => SetVolume(VolumeChannel.Master, v));
            musicSlider.onValueChanged.AddListener(v => SetVolume(VolumeChannel.Music, v));
            sfxSlider.onValueChanged.AddListener(v => SetVolume(VolumeChannel.Sfx, v));
#if !(UNITY_EDITOR || DEVELOPMENT_BUILD)
            developerButton.gameObject.SetActive(false); // 正式 build 沒有 Debug 程式
#endif

            SetVisible(false);
            ShowPage(PauseMenuPage.None, null);
        }

        void OnEnable()
        {
            pauseAction?.Enable();
            cancelAction?.Enable();
            if (options != null)
            {
                options.OnVolumeChanged += HandleVolumeChanged;
                options.OnDeveloperModeChanged += HandleDeveloperModeChanged;
            }
            keyboardPage.OnBack += Back;
            gamepadPage.OnBack += Back;
        }

        void OnDisable()
        {
            if (options != null)
            {
                options.OnVolumeChanged -= HandleVolumeChanged;
                options.OnDeveloperModeChanged -= HandleDeveloperModeChanged;
            }
            keyboardPage.OnBack -= Back;
            gamepadPage.OnBack -= Back;
            if (IsOpen) Close();
        }

        void Update()
        {
            bool pausePressed = pauseAction != null && pauseAction.WasPressedThisFrame();
            if (!IsOpen)
            {
                if (pausePressed) Open();
                return;
            }
            if (Busy) return;
            if (pausePressed || (cancelAction != null && cancelAction.WasPressedThisFrame()))
            {
                Back();
                return;
            }
            // 滑鼠點到空白處會取消選取：選回這一頁的預設項目，鍵盤、手把才導覽得下去
            if (eventSystem != null && eventSystem.currentSelectedGameObject == null) Select(DefaultSelectable(Page));
        }

        // ---------- 開關、換頁 ----------

        void Open()
        {
            previousSelection = eventSystem != null ? eventSystem.currentSelectedGameObject : null;
            if (pause != null) pause.SetPaused(true);
            SetVisible(true);
            ShowPage(PauseMenuPage.Main, resumeButton);
        }

        void Close()
        {
            ShowPage(PauseMenuPage.None, null);
            SetVisible(false);
            if (pause != null) pause.SetPaused(false);
            if (eventSystem != null)
                eventSystem.SetSelectedGameObject(previousSelection != null && previousSelection.activeInHierarchy ? previousSelection : null);
            previousSelection = null;
        }

        void Back()
        {
            switch (Page)
            {
                case PauseMenuPage.Main: Close(); break;
                case PauseMenuPage.Settings: ShowPage(PauseMenuPage.Main, settingsButton); break;
                case PauseMenuPage.Controls: ShowPage(PauseMenuPage.Settings, controlsButton); break;
                case PauseMenuPage.Volume: ShowPage(PauseMenuPage.Settings, volumeButton); break;
                case PauseMenuPage.Keyboard: ShowPage(PauseMenuPage.Controls, keyboardButton); break;
                case PauseMenuPage.Gamepad: ShowPage(PauseMenuPage.Controls, gamepadButton); break;
                case PauseMenuPage.Confirm: ShowPage(PauseMenuPage.Main, confirmQuit ? quitButton : retryButton); break;
            }
        }

        void ShowPage(PauseMenuPage page, Selectable select)
        {
            Page = page;
            mainPage.SetActive(page == PauseMenuPage.Main);
            settingsPage.SetActive(page == PauseMenuPage.Settings);
            controlsPage.SetActive(page == PauseMenuPage.Controls);
            keyboardPage.gameObject.SetActive(page == PauseMenuPage.Keyboard);
            gamepadPage.gameObject.SetActive(page == PauseMenuPage.Gamepad);
            volumePage.SetActive(page == PauseMenuPage.Volume);
            confirmPage.SetActive(page == PauseMenuPage.Confirm);
            if (page != PauseMenuPage.None) Select(select != null ? select : DefaultSelectable(page));
        }

        Selectable DefaultSelectable(PauseMenuPage page)
        {
            switch (page)
            {
                case PauseMenuPage.Main: return resumeButton;
                case PauseMenuPage.Settings: return controlsButton;
                case PauseMenuPage.Controls: return keyboardButton;
                case PauseMenuPage.Keyboard: return keyboardPage.FirstSelectable;
                case PauseMenuPage.Gamepad: return gamepadPage.FirstSelectable;
                case PauseMenuPage.Volume: return masterSlider;
                case PauseMenuPage.Confirm: return confirmNoButton;
                default: return null;
            }
        }

        void Select(Selectable selectable)
        {
            if (eventSystem != null && selectable != null) eventSystem.SetSelectedGameObject(selectable.gameObject);
        }

        void SetVisible(bool visible)
        {
            group.alpha = visible ? 1f : 0f;
            group.interactable = visible;
            group.blocksRaycasts = visible;
        }

        // ---------- 重新挑戰、退出 ----------

        void ShowConfirm(bool quit)
        {
            confirmQuit = quit;
            confirmText.text = quit ? data.quitConfirm : data.retryConfirm;
            ShowPage(PauseMenuPage.Confirm, confirmNoButton);
        }

        void Confirm()
        {
            if (confirmQuit)
            {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
                return;
            }
            if (pause != null) pause.SetPaused(false);
            if (battle != null) battle.Retry(); // 和勝敗畫面的按鈕同一條路：重新載入場景，開場可以長按跳過
        }

        // ---------- 設定 ----------

        void ToggleDeveloperMode()
        {
            if (options != null) options.SetDeveloperMode(!developerMode);
        }

        void HandleDeveloperModeChanged(bool on)
        {
            developerMode = on;
            developerLabel.text = string.Format(data.developerModeFormat, on ? data.onText : data.offText);
        }

        void SetVolume(VolumeChannel channel, float value)
        {
            if (options != null) options.SetVolume(channel, value);
            if (channel != VolumeChannel.Sfx || audioManager == null || data.sfxPreview == null || Time.unscaledTime < nextPreviewTime) return;
            nextPreviewTime = Time.unscaledTime + data.sfxPreviewInterval;
            audioManager.PlayUiSfx(data.sfxPreview, data.sfxPreviewVolume);
        }

        void HandleVolumeChanged(VolumeChannel channel, float value)
        {
            Slider slider;
            Text label;
            switch (channel)
            {
                case VolumeChannel.Music:
                    slider = musicSlider;
                    label = musicValue;
                    break;
                case VolumeChannel.Sfx:
                    slider = sfxSlider;
                    label = sfxValue;
                    break;
                default:
                    slider = masterSlider;
                    label = masterValue;
                    break;
            }
            slider.SetValueWithoutNotify(value);
            label.text = string.Format(data.percentFormat, Mathf.RoundToInt(value * 100f));
        }
    }
}
