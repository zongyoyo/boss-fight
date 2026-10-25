using System.Collections.Generic;
using System.Text;
using BossFight.Hub;
using BossFight.Progression;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace BossFight.UI
{
    /// <summary>
    /// 鐵匠鋪的介面（T22）：HubManager 打開鐵匠鋪（Blacksmith）時出現在畫面右邊。
    /// 左邊是目前角色的裝備欄（點了卸下），右邊是所有裝備（點了穿上、穿著的再點卸下；未解鎖的是剪影加「？？？」；
    /// 角色穿不了的變淡；新解鎖的標「新」、穿著的標「裝備中」），內容照 Blacksmith.OnViewChanged 送來的清單（樣板複製、重複使用）。
    /// 下方是選到（鍵盤、手把導覽或滑鼠移上去）的那一格的說明與換裝前後的數值差異（Blacksmith.PreviewItem／PreviewUnequip；綠變好、紅變差），
    /// 未解鎖的顯示來源提示。開發者模式開著時右上角有「解鎖全部裝備」（Blacksmith.DebugUnlockAll）。
    /// 返回讀 UI/Cancel（Esc、手把 B）→ HubManager.CloseStation。文字與顏色在 HubUIData 的「鐵匠鋪」。由 HubUIBuilder 建立與接線。
    /// </summary>
    [DisallowMultipleComponent]
    public class BlacksmithUI : MonoBehaviour
    {
        [SerializeField] HubUIData data;
        [SerializeField] HubManager hub;
        [SerializeField] Blacksmith blacksmith;
        [SerializeField] EventSystem eventSystem;
        [Tooltip("目前用鍵鼠還是手把（返回提示）；留空時一律鍵鼠")]
        [SerializeField] InputHintDevice hintDevice;
        [Tooltip("整個面板")]
        [SerializeField] CanvasGroup group;
        [SerializeField] Text closeHint;

        [Header("裝備欄（左）")]
        [SerializeField] Text characterLabel;
        [Tooltip("一格欄位的樣板（停用）")]
        [SerializeField] EquipmentCellUI slotTemplate;
        [SerializeField] RectTransform slotContainer;

        [Header("裝備清單（右）")]
        [Tooltip("一件裝備的樣板（停用）")]
        [SerializeField] EquipmentCellUI itemTemplate;
        [SerializeField] RectTransform itemContainer;

        [Header("說明與差異（下）")]
        [SerializeField] Text detailName;
        [SerializeField] Text detailBody;
        [Tooltip("數值差異（rich text 上色）")]
        [SerializeField] Text detailDiff;

        [Header("Debug")]
        [SerializeField] Button debugUnlockButton;

        readonly List<EquipmentCellUI> slotCells = new List<EquipmentCellUI>();
        readonly List<EquipmentCellUI> itemCells = new List<EquipmentCellUI>();
        readonly List<Blacksmith.SlotEntry> slots = new List<Blacksmith.SlotEntry>();
        readonly List<Blacksmith.ItemEntry> items = new List<Blacksmith.ItemEntry>();
        readonly StringBuilder builder = new StringBuilder();
        CharacterDefinition character;
        InputAction cancelAction;
        EquipmentCellUI focused;
        bool visible;

        void Awake()
        {
            var actions = InputSystem.actions;
            if (actions != null) cancelAction = actions.FindAction("UI/Cancel", false);
            slotTemplate.gameObject.SetActive(false);
            itemTemplate.gameObject.SetActive(false);
            if (debugUnlockButton != null)
            {
                debugUnlockButton.onClick.AddListener(() => { if (blacksmith != null) blacksmith.DebugUnlockAll(); });
                debugUnlockButton.GetComponentInChildren<Text>(true).text = data != null ? data.debugUnlockAllLabel : string.Empty;
            }
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
            if (blacksmith != null) blacksmith.OnViewChanged += HandleViewChanged;
            if (hintDevice != null) hintDevice.OnDeviceChanged += HandleDeviceChanged;
        }

        void OnDisable()
        {
            if (hub != null)
            {
                hub.OnStationOpened -= HandleStationOpened;
                hub.OnStationClosed -= HandleStationClosed;
            }
            if (blacksmith != null) blacksmith.OnViewChanged -= HandleViewChanged;
            if (hintDevice != null) hintDevice.OnDeviceChanged -= HandleDeviceChanged;
        }

        void Start()
        {
            if (hintDevice == null) HandleDeviceChanged(false);
        }

        void Update()
        {
            if (!visible) return;
            if (cancelAction != null && cancelAction.WasPressedThisFrame())
            {
                if (hub != null) hub.CloseStation();
                return;
            }
            // 滑鼠點到空白處會取消選取：選回剛才那一格，鍵盤、手把才導覽得下去
            if (eventSystem != null && eventSystem.currentSelectedGameObject == null) Select(DefaultCell());
        }

        void HandleStationOpened(Interactable target)
        {
            if (target != blacksmith) return;
            SetVisible(true);
            focused = null;
            Select(DefaultCell());
        }

        void HandleStationClosed(Interactable target)
        {
            if (target != blacksmith) return;
            SetVisible(false);
            focused = null;
            if (eventSystem != null) eventSystem.SetSelectedGameObject(null);
        }

        void HandleDeviceChanged(bool gamepad)
        {
            if (closeHint != null && data != null) closeHint.text = gamepad ? data.closeHintGamepad : data.closeHintKeyboard;
        }

        // ---------- 內容 ----------

        void HandleViewChanged(Blacksmith.View view)
        {
            character = view.character;
            slots.Clear();
            slots.AddRange(view.slots);
            items.Clear();
            items.AddRange(view.items);
            if (characterLabel != null && data != null)
                characterLabel.text = character != null ? string.Format(data.blacksmithCharacterFormat, character.displayName) : string.Empty;

            Ensure(slotCells, slotTemplate, slotContainer, slots.Count, HandleSlotClicked);
            for (int i = 0; i < slotCells.Count; i++)
            {
                bool used = i < slots.Count;
                slotCells[i].gameObject.SetActive(used);
                if (used) FillSlot(slotCells[i], slots[i]);
            }
            Ensure(itemCells, itemTemplate, itemContainer, items.Count, HandleItemClicked);
            for (int i = 0; i < itemCells.Count; i++)
            {
                bool used = i < items.Count;
                itemCells[i].gameObject.SetActive(used);
                if (used) FillItem(itemCells[i], items[i]);
            }
            if (debugUnlockButton != null) debugUnlockButton.gameObject.SetActive(view.debugTools);

            if (visible && eventSystem != null)
            {
                var selected = eventSystem.currentSelectedGameObject;
                if (selected == null || !selected.activeInHierarchy) Select(DefaultCell());
            }
            ShowDetail(); // 換裝後差異跟著變（穿上的那件變成「卸下後」）
        }

        void Ensure(List<EquipmentCellUI> cells, EquipmentCellUI template, RectTransform container, int count, System.Action<int> clicked)
        {
            while (cells.Count < count)
            {
                var cell = Instantiate(template, container);
                int index = cells.Count;
                cell.Button.onClick.AddListener(() => clicked(index));
                cell.OnFocused += HandleFocused;
                cells.Add(cell);
            }
        }

        void FillSlot(EquipmentCellUI cell, Blacksmith.SlotEntry slot)
        {
            string type = slot.type != null ? slot.type.displayName : string.Empty;
            string item = slot.item != null ? slot.item.displayName : data.emptySlotText;
            cell.Label.text = type + "\n" + item;
            bool hasIcon = slot.item != null && slot.item.icon != null;
            cell.Icon.enabled = hasIcon;
            cell.Icon.sprite = hasIcon ? slot.item.icon : null;
            cell.Icon.color = Color.white;
            cell.SetMarks(false, slot.item != null, data.equippedColor);
            cell.name = "Slot_" + type;
        }

        void FillItem(EquipmentCellUI cell, Blacksmith.ItemEntry entry)
        {
            var item = entry.item;
            float alpha = entry.unlocked && !entry.hasSlot ? data.unavailableAlpha : 1f;
            cell.Icon.enabled = item.icon != null;
            cell.Icon.sprite = item.icon;
            cell.Icon.color = entry.unlocked ? new Color(1f, 1f, 1f, alpha) : data.lockedIconColor;
            cell.Label.text = entry.unlocked ? item.displayName : data.lockedEquipmentName;
            var color = data.textColor;
            color.a *= alpha;
            cell.Label.color = color;
            cell.SetMarks(entry.unlocked && entry.isNew, entry.equippedSlot >= 0, data.equippedColor);
            cell.name = "Item_" + item.id;
        }

        void HandleSlotClicked(int index)
        {
            if (blacksmith != null && index < slots.Count) blacksmith.Unequip(index);
        }

        void HandleItemClicked(int index)
        {
            if (blacksmith != null && index < items.Count) blacksmith.Toggle(items[index].item);
        }

        // 滑鼠移上去也選起來，鍵盤、手把接著從這一格導覽
        void HandleFocused(EquipmentCellUI cell)
        {
            if (!visible) return;
            focused = cell;
            if (eventSystem != null && eventSystem.currentSelectedGameObject != cell.gameObject) eventSystem.SetSelectedGameObject(cell.gameObject);
            ShowDetail();
        }

        // ---------- 說明與差異 ----------

        void ShowDetail()
        {
            detailName.text = detailBody.text = detailDiff.text = string.Empty;
            if (focused == null || !focused.gameObject.activeSelf || data == null || blacksmith == null) return;
            int slot = slotCells.IndexOf(focused);
            if (slot >= 0 && slot < slots.Count)
            {
                var entry = slots[slot];
                if (entry.item == null)
                {
                    detailName.text = entry.type != null ? entry.type.displayName : string.Empty;
                    detailBody.text = data.emptySlotHint;
                    return;
                }
                detailName.text = entry.item.displayName;
                detailBody.text = entry.item.description;
                detailDiff.text = DiffText(blacksmith.PreviewUnequip(slot));
                return;
            }
            int index = itemCells.IndexOf(focused);
            if (index < 0 || index >= items.Count) return;
            var itemEntry = items[index];
            var item = itemEntry.item;
            if (!itemEntry.unlocked)
            {
                detailName.text = data.lockedEquipmentName;
                detailBody.text = string.Format(data.equipmentSourceFormat, item.sourceHint);
                return;
            }
            detailName.text = itemEntry.equippedSlot >= 0 ? $"{item.displayName}（{data.equippedTagText}）" : item.displayName;
            detailBody.text = item.description;
            if (!itemEntry.hasSlot)
            {
                string type = item.slotType != null ? item.slotType.displayName : string.Empty;
                detailDiff.text = Colored(string.Format(data.noSlotFormat, character != null ? character.displayName : string.Empty, type), data.hintColor);
                return;
            }
            detailDiff.text = DiffText(blacksmith.PreviewItem(item));
        }

        string DiffText(Blacksmith.Preview preview)
        {
            if (preview == null) return string.Empty;
            builder.Clear();
            builder.Append(preview.unequip ? data.unequipPreviewHeader : data.equipPreviewHeader);
            if (preview.stats.Count + preview.addedEffects.Count + preview.removedEffects.Count == 0)
            {
                builder.Append(data.noStatChangeText);
                return builder.ToString();
            }
            foreach (var change in preview.stats)
            {
                var label = data.GetStat(change.stat);
                string name = label != null ? label.label : change.stat.ToString();
                string format = label != null ? label.format : "0.##";
                bool better = label != null && label.lowerIsBetter ? change.after < change.before : change.after > change.before;
                string line = string.Format(data.statChangeFormat, name, change.before.ToString(format), change.after.ToString(format));
                builder.Append('\n').Append(Colored(line, better ? data.betterColor : data.worseColor));
            }
            foreach (var effect in preview.addedEffects)
                builder.Append('\n').Append(Colored(string.Format(data.effectAddedFormat, effect.displayName), data.betterColor));
            foreach (var effect in preview.removedEffects)
                builder.Append('\n').Append(Colored(string.Format(data.effectRemovedFormat, effect.displayName), data.worseColor));
            return builder.ToString();
        }

        static string Colored(string text, Color color) => $"<color=#{ColorUtility.ToHtmlStringRGBA(color)}>{text}</color>";

        // ---------- 選取 ----------

        // 剛才選到的那一格；沒有時第一件裝備，再沒有時第一格欄位
        Selectable DefaultCell()
        {
            if (focused != null && focused.gameObject.activeInHierarchy) return focused.Button;
            foreach (var cell in itemCells)
            {
                if (cell.gameObject.activeSelf) return cell.Button;
            }
            foreach (var cell in slotCells)
            {
                if (cell.gameObject.activeSelf) return cell.Button;
            }
            return null;
        }

        void Select(Selectable selectable)
        {
            if (eventSystem != null && selectable != null) eventSystem.SetSelectedGameObject(selectable.gameObject);
        }

        void SetVisible(bool on)
        {
            visible = on;
            group.alpha = on ? 1f : 0f;
            group.interactable = on;
            group.blocksRaycasts = on;
        }
    }
}
