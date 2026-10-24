using System.Collections.Generic;
using BossFight.Battle;
using BossFight.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
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
        Difficulty,
    }

    /// <summary>
    /// ESC 選單（T13）：Player/Pause（Esc、手把 Start）開關，開場、轉階段、勝敗畫面時都能開。開著時 GamePause 暫停遊戲；
    /// 玩家與鏡頭的輸入、游標由 BattleManager 看 GamePause 處理，Debug 面板由 DeveloperMode 藏起來。
    /// 頁面：主選單（繼續、設定、難度、重新挑戰、退出遊戲）→ 設定（按鍵設定、音量設定、開發者模式）→ 按鍵設定（鍵鼠、控制器，KeyBindingPageUI）／音量設定（主音量、音樂、音效）；
    /// 難度（困難、簡單，標出目前的難度；選另一個先確認，確定後存進 GameOptions 並重新挑戰，新的難度在重新載入的場景才套用）。
    /// 返回：Esc、Start、手把 B（UI/Cancel）回上一層，在主選單就關閉；改鍵等待按鍵時不算。重新挑戰、退出遊戲、換難度先問一次。
    /// 設定值都在 GameOptions：訂閱它的事件更新滑桿與開關，改的時候呼叫它的 Set。
    /// 開發者模式開關：只在有 Debug 程式的 build（Editor、Development Build 或定義了 BOSSFIGHT_DEV_TOOLS）才可能出現，而且要開發者模式已經開著，
    /// 或這次執行在選單裡輸入過秘技（PauseMenuData.developerUnlockCode，↑↑↓↓←←→→ A B；記在 BattleManager／BattleSession，重試後還在）；
    /// 出現之後這個場景裡就不再藏起來。沒顯示時「返回」補到它的位置。秘技輸到第一個 A／B 之前先取消選取，手把的 A、B 不會按下按鈕或返回。
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
        [Tooltip("目前用鍵鼠還是手把（每一頁下方的返回提示）；留空時一律鍵鼠")]
        [SerializeField] InputHintDevice hintDevice;
        [Tooltip("每一頁下方的返回提示")]
        [SerializeField] Text[] navigationHints;

        [Header("主選單")]
        [SerializeField] GameObject mainPage;
        [SerializeField] Button resumeButton;
        [SerializeField] Button settingsButton;
        [SerializeField] Button difficultyButton;
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

        [Header("難度")]
        [SerializeField] GameObject difficultyPage;
        [SerializeField] Button hardButton;
        [SerializeField] Text hardLabel;
        [SerializeField] Button easyButton;
        [SerializeField] Text easyLabel;
        [SerializeField] Button difficultyBackButton;

        [Header("秘技")]
        [Tooltip("解鎖開發者模式時顯示的提示（每一頁都看得到）")]
        [SerializeField] Text noticeText;

        [Header("確認")]
        [SerializeField] GameObject confirmPage;
        [SerializeField] Text confirmText;
        [SerializeField] Button confirmYesButton;
        [SerializeField] Button confirmNoButton;

        public PauseMenuPage Page { get; private set; }
        public bool IsOpen => Page != PauseMenuPage.None;

        enum ConfirmAction { Retry, Quit, Difficulty }

#if UNITY_EDITOR || DEVELOPMENT_BUILD || BOSSFIGHT_DEV_TOOLS
        static readonly bool HasDeveloperTools = true;
#else
        static readonly bool HasDeveloperTools = false; // 沒有 Debug 程式：開發者模式開關與秘技都沒有作用
#endif

        InputAction pauseAction, cancelAction;
        GameObject previousSelection;
        bool developerMode, developerVisible;
        ConfirmAction confirmAction;
        Difficulty difficulty, pendingDifficulty;
        float nextPreviewTime;
        Vector2 settingsBackPosition, developerPosition;

        // 秘技：最近的輸入（最多 code 的長度）、目前對到第幾個、輸入中暫時取消的選取
        readonly List<MenuCodeInput> codeInputs = new List<MenuCodeInput>();
        int codeProgress;
        GameObject codeSavedSelection;
        float noticeShownAt = float.NegativeInfinity;

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
            difficultyButton.onClick.AddListener(() => ShowPage(PauseMenuPage.Difficulty, null));
            retryButton.onClick.AddListener(() => ShowConfirm(ConfirmAction.Retry));
            quitButton.onClick.AddListener(() => ShowConfirm(ConfirmAction.Quit));
            hardButton.onClick.AddListener(() => ChooseDifficulty(Difficulty.Hard));
            easyButton.onClick.AddListener(() => ChooseDifficulty(Difficulty.Easy));
            difficultyBackButton.onClick.AddListener(Back);
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
            settingsBackPosition = ((RectTransform)settingsBackButton.transform).anchoredPosition;
            developerPosition = ((RectTransform)developerButton.transform).anchoredPosition;
            ApplyDeveloperVisible();
            if (noticeText != null) noticeText.enabled = false;

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
                options.OnDifficultyChanged += HandleDifficultyChanged;
            }
            if (battle != null) battle.OnDeveloperOptionsUnlocked += HandleDeveloperOptionsUnlocked;
            keyboardPage.OnBack += Back;
            gamepadPage.OnBack += Back;
            if (hintDevice != null) hintDevice.OnDeviceChanged += HandleDeviceChanged;
        }

        void OnDisable()
        {
            if (options != null)
            {
                options.OnVolumeChanged -= HandleVolumeChanged;
                options.OnDeveloperModeChanged -= HandleDeveloperModeChanged;
                options.OnDifficultyChanged -= HandleDifficultyChanged;
            }
            if (battle != null) battle.OnDeveloperOptionsUnlocked -= HandleDeveloperOptionsUnlocked;
            keyboardPage.OnBack -= Back;
            gamepadPage.OnBack -= Back;
            if (hintDevice != null) hintDevice.OnDeviceChanged -= HandleDeviceChanged;
            if (IsOpen) Close();
        }

        void HandleDeviceChanged(bool gamepad)
        {
            if (navigationHints == null) return;
            string text = gamepad ? data.navigationHintGamepad : data.navigationHintKeyboard;
            foreach (var hint in navigationHints)
            {
                if (hint != null) hint.text = text;
            }
        }

        void Update()
        {
            UpdateNotice();
            bool pausePressed = pauseAction != null && pauseAction.WasPressedThisFrame();
            if (!IsOpen)
            {
                if (pausePressed) Open();
                return;
            }
            if (Busy) return;
            bool codeConsumed = UpdateCode();
            bool codeArmed = CodeArmed;
            // 秘技輸到 A／B 那一段：手把的 B（UI/Cancel）算秘技、不返回（輸完或輸錯的那一下也是）；Esc、Start 不在秘技裡，UpdateCode 已經把進度歸零
            if (pausePressed || (!codeArmed && !codeConsumed && cancelAction != null && cancelAction.WasPressedThisFrame()))
            {
                Back();
                return;
            }
            // 滑鼠點到空白處會取消選取：選回這一頁的預設項目，鍵盤、手把才導覽得下去（秘技輸入中故意不選）
            if (!codeArmed && eventSystem != null && eventSystem.currentSelectedGameObject == null) Select(DefaultSelectable(Page));
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
            ResetCode(false);
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
                case PauseMenuPage.Difficulty: ShowPage(PauseMenuPage.Main, difficultyButton); break;
                case PauseMenuPage.Confirm:
                    switch (confirmAction)
                    {
                        case ConfirmAction.Quit: ShowPage(PauseMenuPage.Main, quitButton); break;
                        case ConfirmAction.Difficulty: ShowPage(PauseMenuPage.Difficulty, pendingDifficulty == Difficulty.Easy ? easyButton : hardButton); break;
                        default: ShowPage(PauseMenuPage.Main, retryButton); break;
                    }
                    break;
            }
        }

        void ShowPage(PauseMenuPage page, Selectable select)
        {
            ResetCode(false);
            Page = page;
            mainPage.SetActive(page == PauseMenuPage.Main);
            settingsPage.SetActive(page == PauseMenuPage.Settings);
            controlsPage.SetActive(page == PauseMenuPage.Controls);
            keyboardPage.gameObject.SetActive(page == PauseMenuPage.Keyboard);
            gamepadPage.gameObject.SetActive(page == PauseMenuPage.Gamepad);
            volumePage.SetActive(page == PauseMenuPage.Volume);
            confirmPage.SetActive(page == PauseMenuPage.Confirm);
            difficultyPage.SetActive(page == PauseMenuPage.Difficulty);
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
                case PauseMenuPage.Difficulty: return difficulty == Difficulty.Easy ? easyButton : hardButton;
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

        // ---------- 重新挑戰、退出、換難度 ----------

        void ShowConfirm(ConfirmAction action)
        {
            confirmAction = action;
            switch (action)
            {
                case ConfirmAction.Quit: confirmText.text = data.quitConfirm; break;
                case ConfirmAction.Difficulty: confirmText.text = string.Format(data.difficultyConfirmFormat, DifficultyName(pendingDifficulty)); break;
                default: confirmText.text = data.retryConfirm; break;
            }
            ShowPage(PauseMenuPage.Confirm, confirmNoButton);
        }

        void Confirm()
        {
            if (confirmAction == ConfirmAction.Difficulty && options != null) options.SetDifficulty(pendingDifficulty);
            if (confirmAction == ConfirmAction.Quit)
            {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
                return;
            }
            if (pause != null) pause.SetPaused(false);
            if (battle != null) battle.Retry(); // 和勝敗畫面的按鈕同一條路：重新載入場景，開場可以長按跳過；新的難度在新場景的 BattleManager.Awake 套用
        }

        // ---------- 難度 ----------

        void ChooseDifficulty(Difficulty next)
        {
            if (next == difficulty) return; // 已經是這個難度
            pendingDifficulty = next;
            ShowConfirm(ConfirmAction.Difficulty);
        }

        void HandleDifficultyChanged(Difficulty value)
        {
            difficulty = value;
            hardLabel.text = value == Difficulty.Hard ? string.Format(data.currentDifficultyFormat, data.hardLabel) : data.hardLabel;
            easyLabel.text = value == Difficulty.Easy ? string.Format(data.currentDifficultyFormat, data.easyLabel) : data.easyLabel;
        }

        string DifficultyName(Difficulty value) => value == Difficulty.Easy ? data.easyLabel : data.hardLabel;

        // ---------- 設定 ----------

        void ToggleDeveloperMode()
        {
            if (options != null) options.SetDeveloperMode(!developerMode);
        }

        void HandleDeveloperModeChanged(bool on)
        {
            developerMode = on;
            developerLabel.text = string.Format(data.developerModeFormat, on ? data.onText : data.offText);
            if (on) ShowDeveloperOption();
        }

        void HandleDeveloperOptionsUnlocked() => ShowDeveloperOption();

        // 開發者模式開關一旦出現，這個場景裡就不再藏起來（在選單裡關掉開發者模式時不會突然消失）
        void ShowDeveloperOption()
        {
            if (developerVisible || !HasDeveloperTools) return;
            developerVisible = true;
            ApplyDeveloperVisible();
        }

        void ApplyDeveloperVisible()
        {
            developerButton.gameObject.SetActive(developerVisible);
            ((RectTransform)settingsBackButton.transform).anchoredPosition = developerVisible ? settingsBackPosition : developerPosition;
        }

        // ---------- 秘技 ----------

        // 秘技裡第一個 A／B 的位置：對到這裡（方向都輸完）之後，接下來的 A、B 是秘技，不是確認、返回
        int ArmLength
        {
            get
            {
                var code = data.developerUnlockCode;
                for (int i = 0; i < code.Length; i++)
                {
                    if (code[i] == MenuCodeInput.A || code[i] == MenuCodeInput.B) return i;
                }
                return code.Length;
            }
        }

        bool CodeArmed => codeProgress > 0 && codeProgress >= ArmLength;

        // 回傳這一幀的輸入是不是被秘技吃掉（方向都輸完之後按的 A、B）：吃掉時不當成返回
        bool UpdateCode()
        {
            var code = data.developerUnlockCode;
            if (!HasDeveloperTools || code == null || code.Length == 0) return false;
            if (!ReadCodeInput(out var input))
            {
                if (OtherKeyPressed()) ResetCode(true);
                return false;
            }

            codeInputs.Add(input);
            if (codeInputs.Count > code.Length) codeInputs.RemoveAt(0);
            // 最近的輸入結尾最長能對上秘技開頭幾個（打錯不用整串重來，例如 ↑↑↑ 還是對到前兩個）
            int match = 0;
            for (int length = codeInputs.Count; length > 0 && match == 0; length--)
            {
                bool same = true;
                for (int i = 0; i < length && same; i++)
                    same = codeInputs[codeInputs.Count - length + i] == code[i];
                if (same) match = length;
            }
            bool wasArmed = CodeArmed;
            codeProgress = match;
            if (codeProgress == code.Length)
            {
                ResetCode(true);
                Unlock();
                return true;
            }
            if (CodeArmed && !wasArmed)
            {
                // 先取消選取：手把的 A（確認）不會按下按鈕
                codeSavedSelection = eventSystem != null ? eventSystem.currentSelectedGameObject : null;
                if (eventSystem != null) eventSystem.SetSelectedGameObject(null);
            }
            else if (!CodeArmed && wasArmed)
            {
                RestoreCodeSelection();
            }
            return wasArmed;
        }

        void ResetCode(bool restoreSelection)
        {
            bool wasArmed = CodeArmed;
            codeInputs.Clear();
            codeProgress = 0;
            if (wasArmed && restoreSelection) RestoreCodeSelection();
            codeSavedSelection = null;
        }

        void RestoreCodeSelection()
        {
            if (eventSystem == null) return;
            if (codeSavedSelection != null && codeSavedSelection.activeInHierarchy) eventSystem.SetSelectedGameObject(codeSavedSelection);
            else Select(DefaultSelectable(Page));
            codeSavedSelection = null;
        }

        // 這一幀按下的秘技輸入：鍵盤方向鍵、A、B 字母鍵，手把十字鍵、A（下）、B（右）
        static bool ReadCodeInput(out MenuCodeInput input)
        {
            var k = Keyboard.current;
            var g = Gamepad.current;
            input = default;
            if (Pressed(k?.upArrowKey) || Pressed(g?.dpad.up)) input = MenuCodeInput.Up;
            else if (Pressed(k?.downArrowKey) || Pressed(g?.dpad.down)) input = MenuCodeInput.Down;
            else if (Pressed(k?.leftArrowKey) || Pressed(g?.dpad.left)) input = MenuCodeInput.Left;
            else if (Pressed(k?.rightArrowKey) || Pressed(g?.dpad.right)) input = MenuCodeInput.Right;
            else if (Pressed(k?.aKey) || Pressed(g?.buttonSouth)) input = MenuCodeInput.A;
            else if (Pressed(k?.bKey) || Pressed(g?.buttonEast)) input = MenuCodeInput.B;
            else return false;
            return true;
        }

        // 秘技以外的按鍵（鍵盤任一鍵、手把其他按鍵）：進度歸零
        static bool OtherKeyPressed()
        {
            var k = Keyboard.current;
            if (k != null && k.anyKey.wasPressedThisFrame) return true;
            var g = Gamepad.current;
            if (g == null) return false;
            return Pressed(g.buttonWest) || Pressed(g.buttonNorth) || Pressed(g.leftShoulder) || Pressed(g.rightShoulder) ||
                   Pressed(g.leftTrigger) || Pressed(g.rightTrigger) || Pressed(g.startButton) || Pressed(g.selectButton) ||
                   Pressed(g.leftStickButton) || Pressed(g.rightStickButton);
        }

        static bool Pressed(ButtonControl button) => button != null && button.wasPressedThisFrame;

        void Unlock()
        {
            noticeShownAt = Time.unscaledTime;
            if (noticeText != null)
            {
                noticeText.text = data.developerUnlockedText;
                noticeText.enabled = true;
            }
            if (audioManager != null && data.developerUnlockSfx != null) audioManager.PlayUiSfx(data.developerUnlockSfx, data.developerUnlockVolume);
            ShowDeveloperOption();
            if (battle != null) battle.UnlockDeveloperOptions(); // 記在 BattleSession：重試後選單還是顯示開關
        }

        void UpdateNotice()
        {
            if (noticeText == null || !noticeText.enabled) return;
            float elapsed = Time.unscaledTime - noticeShownAt;
            if (elapsed >= data.noticeTime || !IsOpen)
            {
                noticeText.enabled = false;
                return;
            }
            var color = noticeText.color;
            color.a = Mathf.Clamp01((data.noticeTime - elapsed) / data.noticeFadeTime);
            noticeText.color = color;
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
