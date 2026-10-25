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
        InputAction cancelAction;
        BossDefinition chosen, described;
        bool visible, confirming, departing;
        int chosenRow;

        void Awake()
        {
            var actions = InputSystem.actions;
            if (actions != null) cancelAction = actions.FindAction("UI/Cancel", false);
            rowTemplate.gameObject.SetActive(false);
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
        }

        void HandleStationClosed(Interactable target)
        {
            if (target != portal) return;
            confirming = false;
            SetVisible(false);
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
                rows[i].GetComponentInChildren<Text>(true).text = entries[i].defeated && data != null
                    ? string.Format(data.defeatedBossFormat, boss.displayName)
                    : boss.displayName;
            }
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
            if (index >= rowBosses.Count || rowBosses[index] == null) return;
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
            if (chosenRow < rows.Count && rows[chosenRow].gameObject.activeSelf) return rows[chosenRow];
            foreach (var row in rows)
            {
                if (row.gameObject.activeSelf) return row;
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
