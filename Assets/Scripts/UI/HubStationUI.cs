using System.Collections.Generic;
using BossFight.Hub;
using BossFight.Progression;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace BossFight.UI
{
    /// <summary>
    /// 傳送門的挑戰清單（T20 的站點面板；T21–T24 營火、鐵匠鋪、村長各有自己的介面，這裡只剩傳送門）：HubManager 打開傳送門時出現在畫面右邊。
    /// 清單照 Portal.OnBossesChanged（T24 起讀 GameDatabase.bosses）一隻 Boss 一列（rowTemplate 複製，擊敗過的照 defeatedBossFormat），
    /// 選到的那一列在下方顯示 BossDefinition.portalDescription；點了跳確認框 → Portal.Depart(boss) 換場景（黑幕淡出期間面板維持開著、不再收輸入）。
    /// 返回讀 UI/Cancel（Esc、手把 B）：確認框開著時回到清單，否則呼叫 HubManager.CloseStation。面板下方的返回提示依 InputHintDevice 換鍵鼠或手把的字。
    /// T34：列的底色照 BossDefinition.portalRowColor（alpha 0 = 樣板的顏色）；上鎖的列（BossEntry.locked）不能選，列上的 PortalLockUI 顯示鎖與鎖鏈；
    /// BossEntry.playUnlock 的列在面板出現後播解鎖演出，播完才變成可以選（並選到它）。
    /// 由 HubUIBuilder 建立與接線。
    /// </summary>
    [DisallowMultipleComponent]
    public class HubStationUI : MonoBehaviour
    {
        [SerializeField] HubUIData data;
        [SerializeField] HubManager hub;
        [SerializeField] Portal portal;
        [SerializeField] EventSystem eventSystem;
        [Tooltip("目前用鍵鼠還是手把（返回提示）；留空時一律鍵鼠")]
        [SerializeField] InputHintDevice hintDevice;
        [Tooltip("整個面板")]
        [SerializeField] CanvasGroup group;
        [SerializeField] Text titleText;
        [SerializeField] Text closeHint;

        [Header("挑戰清單")]
        [SerializeField] GameObject portalList;
        [Tooltip("一列的樣板（停用的按鈕，底下有 Text）")]
        [SerializeField] Button rowTemplate;
        [SerializeField] RectTransform rowContainer;
        [Tooltip("選到的那隻 Boss 的說明")]
        [SerializeField] Text descriptionText;

        [Header("確認框")]
        [SerializeField] GameObject confirmBox;
        [SerializeField] Text confirmText;
        [SerializeField] Button confirmYesButton;
        [SerializeField] Button confirmNoButton;

        readonly List<Button> rows = new List<Button>();
        readonly List<BossDefinition> rowBosses = new List<BossDefinition>();
        readonly List<int> pendingUnlocks = new List<int>();
        ColorBlock templateColors;
        InputAction cancelAction;
        BossDefinition chosen, described;
        bool visible, confirming, departing;
        int chosenRow;

        void Awake()
        {
            var actions = InputSystem.actions;
            if (actions != null) cancelAction = actions.FindAction("UI/Cancel", false);
            rowTemplate.gameObject.SetActive(false);
            templateColors = rowTemplate.colors;
            confirmYesButton.onClick.AddListener(Depart);
            confirmNoButton.onClick.AddListener(HideConfirm);
            SetVisible(false);
        }

        void OnEnable()
        {
            cancelAction?.Enable();
            if (hub != null)
            {
                hub.OnStationOpened += HandleStationOpened;
                hub.OnStationClosed += HandleStationClosed;
            }
            if (portal != null) portal.OnBossesChanged += HandleBossesChanged;
            if (hintDevice != null) hintDevice.OnDeviceChanged += HandleDeviceChanged;
        }

        void OnDisable()
        {
            if (hub != null)
            {
                hub.OnStationOpened -= HandleStationOpened;
                hub.OnStationClosed -= HandleStationClosed;
            }
            if (portal != null) portal.OnBossesChanged -= HandleBossesChanged;
            if (hintDevice != null) hintDevice.OnDeviceChanged -= HandleDeviceChanged;
        }

        void Start()
        {
            if (hintDevice == null) HandleDeviceChanged(false);
        }

        void Update()
        {
            if (!visible || departing) return;
            if (cancelAction != null && cancelAction.WasPressedThisFrame())
            {
                if (confirming) HideConfirm();
                else if (hub != null) hub.CloseStation();
                return;
            }
            // 滑鼠點到空白處會取消選取：選回預設的按鈕，鍵盤、手把才導覽得下去
            if (eventSystem != null && eventSystem.currentSelectedGameObject == null) Select(DefaultSelectable());
            if (!confirming) ShowDescription(SelectedBoss());
        }

        void HandleStationOpened(Interactable target)
        {
            if (target != portal) return;
            confirming = departing = false;
            var text = data != null ? data.Get(target.Kind) : null;
            titleText.text = text != null ? text.title : string.Empty;
            portalList.SetActive(true);
            confirmBox.SetActive(false);
            SetVisible(true);
            chosenRow = 0;
            described = null;
            Select(DefaultSelectable());
            ShowDescription(SelectedBoss());
            foreach (int index in pendingUnlocks)
            {
                var lockUi = LockOf(rows[index]);
                if (lockUi != null) lockUi.PlayUnlock(() => FinishUnlock(index));
                else FinishUnlock(index);
            }
            pendingUnlocks.Clear();
        }

        // 解鎖演出播完：這一列可以選了，選到它
        void FinishUnlock(int index)
        {
            if (index >= rows.Count || !visible) return;
            rows[index].interactable = true;
            if (!confirming && !departing)
            {
                chosenRow = index;
                Select(rows[index]);
            }
        }

        static PortalLockUI LockOf(Button row) => row.GetComponentInChildren<PortalLockUI>(true);

        void HandleStationClosed(Interactable target)
        {
            if (target != portal) return;
            confirming = false;
            SetVisible(false);
            pendingUnlocks.Clear();
            // 演出播到一半就關掉：直接當作播完（存檔在打開時已經記過）
            for (int i = 0; i < rows.Count; i++)
            {
                var lockUi = LockOf(rows[i]);
                if (lockUi != null && lockUi.IsPlaying)
                {
                    lockUi.Stop();
                    rows[i].interactable = true;
                }
            }
            if (eventSystem != null) eventSystem.SetSelectedGameObject(null);
        }

        // 清單一列對一個按鈕（Portal.Opened 時送，比 OnStationOpened 早）
        void HandleBossesChanged(IReadOnlyList<Portal.BossEntry> entries)
        {
            while (rows.Count < entries.Count)
            {
                var row = Instantiate(rowTemplate, rowContainer);
                int index = rows.Count;
                row.onClick.AddListener(() => ShowConfirm(index));
                rows.Add(row);
                rowBosses.Add(null);
            }
            for (int i = 0; i < rows.Count; i++)
            {
                bool used = i < entries.Count;
                rows[i].gameObject.SetActive(used);
                rowBosses[i] = used ? entries[i].boss : null;
                if (!used) continue;
                var boss = entries[i].boss;
                rows[i].name = "Row_" + boss.id;
                ApplyRowColors(rows[i], boss, entries[i].locked || entries[i].playUnlock);
                rows[i].interactable = !entries[i].locked && !entries[i].playUnlock;
                var lockUi = LockOf(rows[i]);
                if (lockUi != null) lockUi.SetLocked(entries[i].locked || entries[i].playUnlock);
                if (entries[i].playUnlock) pendingUnlocks.Add(i);
                rows[i].GetComponentInChildren<Text>(true).text = entries[i].defeated && data != null
                    ? string.Format(data.defeatedBossFormat, boss.displayName)
                    : boss.displayName;
            }
        }

        // 自訂底色的列：一般、選到、按下、上鎖都從那個顏色算；沒有自訂就用樣板的顏色
        void ApplyRowColors(Button row, BossDefinition boss, bool locked)
        {
            var colors = templateColors;
            if (boss.portalRowColor.a > 0f && data != null)
            {
                var baseColor = boss.portalRowColor;
                var highlight = Color.Lerp(baseColor, Color.white, data.customRowHighlightBlend);
                highlight.a = baseColor.a;
                colors.normalColor = baseColor;
                colors.highlightedColor = colors.selectedColor = highlight;
                colors.pressedColor = baseColor * 0.8f;
                colors.disabledColor = baseColor * data.lockedRowTint;
            }
            else if (data != null)
            {
                colors.disabledColor = templateColors.normalColor * data.lockedRowTint;
            }
            colors.colorMultiplier = 1f;
            row.colors = colors;
        }

        void HandleDeviceChanged(bool gamepad)
        {
            if (closeHint != null && data != null) closeHint.text = gamepad ? data.closeHintGamepad : data.closeHintKeyboard;
        }

        // 選到的那一列（鍵盤、手把導覽或滑鼠移上去）；沒選到清單裡的列時 null
        BossDefinition SelectedBoss()
        {
            var selected = eventSystem != null ? eventSystem.currentSelectedGameObject : null;
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i].gameObject == selected) return rowBosses[i];
            }
            return null;
        }

        void ShowDescription(BossDefinition boss)
        {
            if (boss == null || boss == described || descriptionText == null) return;
            described = boss;
            descriptionText.text = boss.portalDescription;
        }

        void ShowConfirm(int index)
        {
            if (index >= rowBosses.Count || rowBosses[index] == null || !rows[index].interactable) return;
            chosenRow = index;
            chosen = rowBosses[index];
            confirming = true;
            confirmText.text = string.Format(data.departConfirmFormat, chosen.displayName);
            confirmYesButton.GetComponentInChildren<Text>().text = data.departYes;
            confirmNoButton.GetComponentInChildren<Text>().text = data.departNo;
            portalList.SetActive(false);
            confirmBox.SetActive(true);
            Select(confirmNoButton);
        }

        void HideConfirm()
        {
            confirming = false;
            confirmBox.SetActive(false);
            portalList.SetActive(true);
            Select(DefaultSelectable());
        }

        void Depart()
        {
            if (portal == null || chosen == null) return;
            departing = true;
            group.interactable = false;
            portal.Depart(chosen);
        }

        Selectable DefaultSelectable()
        {
            if (confirming) return confirmNoButton;
            if (chosenRow < rows.Count && rows[chosenRow].gameObject.activeSelf && rows[chosenRow].interactable) return rows[chosenRow];
            foreach (var row in rows)
            {
                if (row.gameObject.activeSelf && row.interactable) return row;
            }
            return null;
        }

        void Select(Selectable selectable)
        {
            if (eventSystem != null && selectable != null) eventSystem.SetSelectedGameObject(selectable.gameObject);
        }

        void SetVisible(bool visible)
        {
            this.visible = visible;
            group.alpha = visible ? 1f : 0f;
            group.interactable = visible;
            group.blocksRaycasts = visible;
        }
    }
}
