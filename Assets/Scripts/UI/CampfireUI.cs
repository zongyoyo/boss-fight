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
    /// 營火的介面（T21）：HubManager 打開營火（Campfire）時出現在畫面右邊，分「角色」「技能樹」兩頁（技能樹 T23 再做，先是「（準備中）」）。
    /// 角色頁照 Campfire.OnCharactersChanged 送來的清單排成一列一個按鈕（rowTemplate 複製，放在有 VerticalLayoutGroup 的 rowContainer 底下），
    /// 目前的角色標「（目前）」並換字色；點了就呼叫 Campfire.SelectCharacter（換人後營火會再送一次清單，選取留在同一列）。
    /// 返回讀 UI/Cancel（Esc、手把 B）→ HubManager.CloseStation。面板下方的返回提示依 InputHintDevice 換鍵鼠或手把的字。由 HubUIBuilder 建立與接線。
    /// </summary>
    [DisallowMultipleComponent]
    public class CampfireUI : MonoBehaviour
    {
        [SerializeField] HubUIData data;
        [SerializeField] HubManager hub;
        [SerializeField] Campfire campfire;
        [SerializeField] EventSystem eventSystem;
        [Tooltip("目前用鍵鼠還是手把（返回提示）；留空時一律鍵鼠")]
        [SerializeField] InputHintDevice hintDevice;
        [Tooltip("整個面板")]
        [SerializeField] CanvasGroup group;
        [SerializeField] Text closeHint;

        [Header("頁籤")]
        [SerializeField] Button charactersTab;
        [SerializeField] Button skillTreeTab;
        [SerializeField] GameObject charactersPage;
        [SerializeField] GameObject skillTreePage;

        [Header("角色清單")]
        [Tooltip("一列的樣板（停用的按鈕，底下有 Text）")]
        [SerializeField] Button rowTemplate;
        [SerializeField] RectTransform rowContainer;

        readonly List<Button> rows = new List<Button>();
        readonly List<CharacterDefinition> rowCharacters = new List<CharacterDefinition>();
        InputAction cancelAction;
        bool visible, onCharacters = true;
        int currentRow = -1;

        void Awake()
        {
            var actions = InputSystem.actions;
            if (actions != null) cancelAction = actions.FindAction("UI/Cancel", false);
            charactersTab.onClick.AddListener(() => ShowPage(true));
            skillTreeTab.onClick.AddListener(() => ShowPage(false));
            rowTemplate.gameObject.SetActive(false);
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
            if (campfire != null) campfire.OnCharactersChanged += HandleCharactersChanged;
            if (hintDevice != null) hintDevice.OnDeviceChanged += HandleDeviceChanged;
        }

        void OnDisable()
        {
            if (hub != null)
            {
                hub.OnStationOpened -= HandleStationOpened;
                hub.OnStationClosed -= HandleStationClosed;
            }
            if (campfire != null) campfire.OnCharactersChanged -= HandleCharactersChanged;
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
            // 滑鼠點到空白處會取消選取：選回預設的按鈕，鍵盤、手把才導覽得下去
            if (eventSystem != null && eventSystem.currentSelectedGameObject == null) Select(DefaultSelectable());
        }

        void HandleStationOpened(Interactable target)
        {
            if (target != campfire) return;
            SetVisible(true);
            ShowPage(true);
        }

        void HandleStationClosed(Interactable target)
        {
            if (target != campfire) return;
            SetVisible(false);
            if (eventSystem != null) eventSystem.SetSelectedGameObject(null);
        }

        void HandleDeviceChanged(bool gamepad)
        {
            if (closeHint != null && data != null) closeHint.text = gamepad ? data.closeHintGamepad : data.closeHintKeyboard;
        }

        // 清單一列對一個按鈕；按鈕重複使用，換人後選取還留在原本那一列
        void HandleCharactersChanged(IReadOnlyList<Campfire.CharacterEntry> entries)
        {
            while (rows.Count < entries.Count)
            {
                var row = Instantiate(rowTemplate, rowContainer);
                int index = rows.Count;
                row.onClick.AddListener(() => SelectRow(index));
                rows.Add(row);
                rowCharacters.Add(null);
            }
            currentRow = -1;
            for (int i = 0; i < rows.Count; i++)
            {
                bool used = i < entries.Count;
                rows[i].gameObject.SetActive(used);
                rowCharacters[i] = used ? entries[i].character : null;
                if (!used) continue;
                var entry = entries[i];
                var label = rows[i].GetComponentInChildren<Text>(true);
                string name = entry.character.displayName;
                label.text = entry.current && data != null ? string.Format(data.currentCharacterFormat, name) : name;
                label.color = entry.current && data != null ? data.currentCharacterColor : rowTemplate.GetComponentInChildren<Text>(true).color;
                rows[i].name = "Row_" + entry.character.id;
                if (entry.current) currentRow = i;
            }
            // 選到的那一列被藏起來時（開發者模式關掉）改選目前角色
            if (visible && onCharacters && eventSystem != null)
            {
                var selected = eventSystem.currentSelectedGameObject;
                if (selected == null || !selected.activeInHierarchy) Select(DefaultSelectable());
            }
        }

        void SelectRow(int index)
        {
            if (campfire != null && index < rowCharacters.Count && rowCharacters[index] != null) campfire.SelectCharacter(rowCharacters[index]);
        }

        void ShowPage(bool characters)
        {
            onCharacters = characters;
            charactersPage.SetActive(characters);
            skillTreePage.SetActive(!characters);
            SetTabColor(charactersTab, characters);
            SetTabColor(skillTreeTab, !characters);
            Select(DefaultSelectable());
        }

        void SetTabColor(Button tab, bool active)
        {
            if (data == null) return;
            var colors = tab.colors;
            colors.normalColor = active ? data.tabActiveColor : data.buttonColor;
            tab.colors = colors;
        }

        // 角色頁選目前角色那一列，技能樹頁選頁籤
        Selectable DefaultSelectable()
        {
            if (!onCharacters) return skillTreeTab;
            if (currentRow >= 0 && currentRow < rows.Count && rows[currentRow].gameObject.activeSelf) return rows[currentRow];
            return charactersTab;
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
