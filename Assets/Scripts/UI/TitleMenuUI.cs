using BossFight.Core;
using BossFight.Title;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace BossFight.UI
{
    /// <summary>
    /// 標題畫面的介面（T32、T33）：左上遊戲名、左下主選單（開始遊戲、設定、退出遊戲）。
    /// 開始遊戲 → 存檔欄位面板（四格，ProfileStore 的摘要）：點一格選取（疊上 chosenSlotColor），下方出現「載入」「刪除」；
    /// 空欄位時「載入」變「新遊戲」、「刪除」不能按；在已選取的欄位上再點一下（滑鼠或 Enter／手把 A）直接載入或開新遊戲。刪除先確認。
    /// 設定 → ESC 選單（PauseMenuUI）的設定頁（settingsOnly：不暫停、沒有主選單、不顯示重置存檔），關掉時選回「設定」。
    /// 返回讀 UI/Cancel（Esc、手把 B）：確認框 → 欄位面板 → 主選單。只呼叫 TitleManager 的方法、訂閱它的事件。
    /// 滑鼠點到空白處取消選取時，選回目前這一頁的預設按鈕，鍵盤與手把才導覽得下去。
    /// </summary>
    [DisallowMultipleComponent]
    public class TitleMenuUI : MonoBehaviour
    {
        enum TitlePage { Main, Slots, Confirm }

        [SerializeField] TitleUIData data;
        [SerializeField] TitleManager title;
        [SerializeField] EventSystem eventSystem;
        [Tooltip("ESC 選單（標題版，只開設定頁）")]
        [SerializeField] PauseMenuUI pauseMenu;
        [Tooltip("返回提示的文字（navigationHintKeyboard／Gamepad）")]
        [SerializeField] PauseMenuData pauseData;
        [Tooltip("目前用鍵鼠還是手把；留空時一律鍵鼠")]
        [SerializeField] InputHintDevice hintDevice;

        [Header("遊戲名")]
        [SerializeField] Text titleText;

        [Header("主選單")]
        [SerializeField] GameObject mainPage;
        [SerializeField] Button startButton;
        [SerializeField] Button settingsButton;
        [SerializeField] Button quitButton;

        [Header("存檔欄位")]
        [SerializeField] GameObject slotsPage;
        [SerializeField] Text slotsTitleText;
        [Tooltip("每一格的按鈕（照欄位順序）")]
        [SerializeField] Button[] slotButtons;
        [SerializeField] Text[] slotNames;
        [SerializeField] Text[] slotSummaries;
        [Tooltip("選取中的欄位疊上的底色")]
        [SerializeField] Image[] slotChosenMarks;
        [SerializeField] Button loadButton;
        [SerializeField] Text loadLabel;
        [SerializeField] Button deleteButton;
        [SerializeField] Button slotsBackButton;
        [SerializeField] Text slotsHint;

        [Header("刪除確認")]
        [SerializeField] GameObject confirmPage;
        [SerializeField] Text confirmText;
        [SerializeField] Button confirmYesButton;
        [SerializeField] Button confirmNoButton;

        TitlePage page;
        int chosen = -1;
        InputAction cancelAction;

        void Awake()
        {
            var actions = InputSystem.actions;
            if (actions != null) cancelAction = actions.FindAction("UI/Cancel", false);
            if (data != null)
            {
                if (titleText != null) titleText.text = data.gameTitle;
                SetLabel(startButton, data.startLabel);
                SetLabel(settingsButton, data.settingsLabel);
                SetLabel(quitButton, data.quitLabel);
                if (slotsTitleText != null) slotsTitleText.text = data.slotsTitle;
                SetLabel(deleteButton, data.deleteLabel);
                SetLabel(slotsBackButton, data.backLabel);
                SetLabel(confirmYesButton, data.confirmYes);
                SetLabel(confirmNoButton, data.confirmNo);
            }
            startButton.onClick.AddListener(OpenSlots);
            settingsButton.onClick.AddListener(OpenSettings);
            quitButton.onClick.AddListener(() => title.Quit());
            for (int i = 0; i < slotButtons.Length; i++)
            {
                int slot = i;
                slotButtons[i].onClick.AddListener(() => ClickSlot(slot));
            }
            loadButton.onClick.AddListener(() => { if (chosen >= 0) title.Play(chosen); });
            deleteButton.onClick.AddListener(ShowDeleteConfirm);
            slotsBackButton.onClick.AddListener(Back);
            confirmYesButton.onClick.AddListener(ConfirmDelete);
            confirmNoButton.onClick.AddListener(Back);
        }

        void OnEnable()
        {
            cancelAction?.Enable();
            if (title != null)
            {
                title.OnLeaving += HandleLeaving;
                title.OnSlotChanged += HandleSlotChanged;
            }
            if (pauseMenu != null) pauseMenu.OnClosed += HandleSettingsClosed;
            if (hintDevice != null) hintDevice.OnDeviceChanged += HandleDeviceChanged;
        }

        void OnDisable()
        {
            if (title != null)
            {
                title.OnLeaving -= HandleLeaving;
                title.OnSlotChanged -= HandleSlotChanged;
            }
            if (pauseMenu != null) pauseMenu.OnClosed -= HandleSettingsClosed;
            if (hintDevice != null) hintDevice.OnDeviceChanged -= HandleDeviceChanged;
        }

        void Start()
        {
            HandleDeviceChanged(false);
            ShowPage(TitlePage.Main, startButton);
        }

        void Update()
        {
            if (title != null && title.IsLeaving) return;
            if (pauseMenu != null && pauseMenu.IsOpen) return; // 設定頁開著：返回、選取都交給它
            if (cancelAction != null && cancelAction.WasPressedThisFrame())
            {
                Back();
                return;
            }
            if (eventSystem != null && eventSystem.currentSelectedGameObject == null) Select(DefaultSelectable());
        }

        // ---------- 換頁 ----------

        void ShowPage(TitlePage next, Selectable select)
        {
            page = next;
            mainPage.SetActive(next == TitlePage.Main);
            slotsPage.SetActive(next != TitlePage.Main);
            confirmPage.SetActive(next == TitlePage.Confirm);
            SetSlotsInteractable(next == TitlePage.Slots);
            Select(select != null ? select : DefaultSelectable());
        }

        Selectable DefaultSelectable()
        {
            switch (page)
            {
                case TitlePage.Slots: return slotButtons[chosen >= 0 ? chosen : 0];
                case TitlePage.Confirm: return confirmNoButton;
                default: return startButton;
            }
        }

        void Back()
        {
            switch (page)
            {
                case TitlePage.Confirm: ShowPage(TitlePage.Slots, deleteButton); break;
                case TitlePage.Slots: ShowPage(TitlePage.Main, startButton); break;
            }
        }

        void OpenSlots()
        {
            chosen = -1;
            RefreshSlots();
            ShowPage(TitlePage.Slots, slotButtons[0]);
        }

        void OpenSettings()
        {
            if (pauseMenu != null) pauseMenu.OpenSettings();
        }

        void HandleSettingsClosed() => Select(settingsButton);

        // ---------- 存檔欄位 ----------

        // 點一下選取；在已選取的欄位上再點一下直接載入（空欄位開新遊戲）
        void ClickSlot(int slot)
        {
            if (title.IsLeaving) return;
            if (slot == chosen)
            {
                title.Play(slot);
                return;
            }
            chosen = slot;
            RefreshSlots();
            Select(slotButtons[slot]); // 鍵盤、手把的選取跟著選取中的欄位（再按一次確認就載入）
        }

        void RefreshSlots()
        {
            for (int i = 0; i < slotButtons.Length; i++)
            {
                var info = title.GetSlot(i);
                slotNames[i].text = string.Format(data.slotNameFormat, i + 1);
                slotSummaries[i].text = Summary(info);
                slotChosenMarks[i].enabled = i == chosen;
            }
            bool any = chosen >= 0;
            loadButton.gameObject.SetActive(any);
            deleteButton.gameObject.SetActive(any);
            if (!any) return;
            bool empty = title.GetSlot(chosen).IsEmpty;
            loadLabel.text = empty ? data.newGameLabel : data.loadLabel;
            deleteButton.interactable = !empty && page != TitlePage.Confirm;
        }

        string Summary(ProfileSlotInfo info)
        {
            if (info.IsEmpty) return data.emptySlotText;
            if (info.corrupt || info.data == null) return data.corruptSlotText;
            string progress = info.data.prologueDone ? string.Format(data.progressHubFormat, info.data.defeatedBosses.Count) : data.progressPrologue;
            string time = info.lastSaved.HasValue ? info.lastSaved.Value.ToString(data.timeFormat) : data.noTimeText;
            return string.Format(data.slotSummaryFormat, title.CharacterName(info.data), progress, time);
        }

        void SetSlotsInteractable(bool on)
        {
            foreach (var button in slotButtons) button.interactable = on;
            loadButton.interactable = on;
            slotsBackButton.interactable = on;
            deleteButton.interactable = on && chosen >= 0 && !title.GetSlot(chosen).IsEmpty;
        }

        void HandleSlotChanged(int slot) => RefreshSlots();

        // ---------- 刪除 ----------

        void ShowDeleteConfirm()
        {
            if (chosen < 0 || title.GetSlot(chosen).IsEmpty) return;
            confirmText.text = string.Format(data.deleteConfirmFormat, chosen + 1);
            ShowPage(TitlePage.Confirm, confirmNoButton);
        }

        void ConfirmDelete()
        {
            int slot = chosen;
            ShowPage(TitlePage.Slots, slotButtons[slot]);
            title.Delete(slot);
            SetSlotsInteractable(true);
        }

        // ---------- 其他 ----------

        void HandleLeaving()
        {
            // 換場景中不再接受點擊
            foreach (var button in GetComponentsInChildren<Button>(true)) button.interactable = false;
        }

        void HandleDeviceChanged(bool gamepad)
        {
            if (slotsHint != null && pauseData != null) slotsHint.text = gamepad ? pauseData.navigationHintGamepad : pauseData.navigationHintKeyboard;
        }

        void Select(Selectable target)
        {
            if (eventSystem != null && target != null) eventSystem.SetSelectedGameObject(target.gameObject);
        }

        static void SetLabel(Button button, string label)
        {
            if (button == null) return;
            var text = button.GetComponentInChildren<Text>(true);
            if (text != null) text.text = label;
        }
    }
}
