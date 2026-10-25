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
    /// 營火的介面（T21）：HubManager 打開營火（Campfire）時出現在畫面右邊，分「角色」「技能樹」兩頁。
    /// 角色頁照 Campfire.OnCharactersChanged 送來的清單排成一列一個按鈕（rowTemplate 複製，放在有 VerticalLayoutGroup 的 rowContainer 底下），
    /// 目前的角色標「（目前）」並換字色；點了就呼叫 Campfire.SelectCharacter（換人後營火會再送一次清單，選取留在同一列）。
    /// 技能樹頁（T23）照 Campfire.OnSkillTreeChanged：左邊的節點依 SkillNode.gridPosition 排（SkillNodeCellUI 樣板複製），前置用線連
    /// （學過的前置連線亮）；節點學過亮、可學有外框、不能學變暗。右上是剩餘技能點，右邊是選到的節點的說明、狀態（需要先學／點數不足）、
    /// 學了之後的數值差異（Campfire.PreviewNode）與「學習」按鈕（按下就學，Campfire.Learn）。「重置」先跳確認框（Campfire.ResetSkills）。
    /// 開發者模式開著時多一個「給 10 技能點」（Campfire.DebugGiveSkillPoints）。
    /// 返回讀 UI/Cancel（Esc、手把 B）：確認框開著時關確認框，否則 HubManager.CloseStation。面板下方的返回提示依 InputHintDevice 換鍵鼠或手把的字。
    /// 文字與顏色在 HubUIData 的「營火」「技能樹」。由 HubUIBuilder 建立與接線。
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

        [Header("技能樹")]
        [Tooltip("確認框開著時不能操作的部分（節點、說明、按鈕）")]
        [SerializeField] CanvasGroup treeGroup;
        [SerializeField] Text pointsText;
        [Tooltip("目前角色沒有技能樹時的字")]
        [SerializeField] Text noTreeText;
        [Tooltip("一個節點的樣板（停用）")]
        [SerializeField] SkillNodeCellUI nodeTemplate;
        [Tooltip("節點的父物件（左上角 = 格子座標 (0, 0) 的那一格往外留邊）")]
        [SerializeField] RectTransform nodeContainer;
        [Tooltip("一條前置連線的樣板（停用的 Image）")]
        [SerializeField] Image lineTemplate;
        [Tooltip("連線的父物件（跟節點同大小、畫在節點後面）")]
        [SerializeField] RectTransform lineContainer;
        [SerializeField] Text detailName;
        [SerializeField] Text detailStatus;
        [SerializeField] Text detailBody;
        [Tooltip("數值差異（rich text 上色）")]
        [SerializeField] Text detailDiff;
        [SerializeField] Button learnButton;
        [SerializeField] Button resetButton;
        [Tooltip("重置的確認框")]
        [SerializeField] GameObject resetConfirm;
        [SerializeField] Text resetConfirmText;
        [SerializeField] Button resetYesButton;
        [SerializeField] Button resetNoButton;
        [Tooltip("Debug（開發者模式）的「給技能點」")]
        [SerializeField] Button debugPointsButton;

        readonly List<Button> rows = new List<Button>();
        readonly List<CharacterDefinition> rowCharacters = new List<CharacterDefinition>();
        readonly List<SkillNodeCellUI> nodeCells = new List<SkillNodeCellUI>();
        readonly List<Image> lines = new List<Image>();
        readonly List<Campfire.SkillEntry> skillEntries = new List<Campfire.SkillEntry>();
        readonly List<SkillNode> missing = new List<SkillNode>();
        readonly StringBuilder builder = new StringBuilder();
        CharacterDefinition skillCharacter;
        SkillNode focusedNode;
        int points, spent;
        InputAction cancelAction;
        bool visible, onCharacters = true, confirming;
        int currentRow = -1;

        void Awake()
        {
            var actions = InputSystem.actions;
            if (actions != null) cancelAction = actions.FindAction("UI/Cancel", false);
            charactersTab.onClick.AddListener(() => ShowPage(true));
            skillTreeTab.onClick.AddListener(() => ShowPage(false));
            rowTemplate.gameObject.SetActive(false);
            nodeTemplate.gameObject.SetActive(false);
            lineTemplate.gameObject.SetActive(false);
            learnButton.onClick.AddListener(HandleLearnClicked);
            resetButton.onClick.AddListener(OpenResetConfirm);
            resetYesButton.onClick.AddListener(HandleResetConfirmed);
            resetNoButton.onClick.AddListener(CloseResetConfirm);
            if (debugPointsButton != null)
            {
                debugPointsButton.onClick.AddListener(() => { if (campfire != null) campfire.DebugGiveSkillPoints(); });
                debugPointsButton.gameObject.SetActive(false);
            }
            if (data != null)
            {
                SetLabel(learnButton, data.learnButtonLabel);
                SetLabel(resetButton, data.resetSkillsLabel);
                SetLabel(resetYesButton, data.resetSkillsYes);
                SetLabel(resetNoButton, data.resetSkillsNo);
                if (debugPointsButton != null && campfire != null)
                    SetLabel(debugPointsButton, string.Format(data.debugGiveSkillPointsFormat, campfire.DebugSkillPointAmount));
            }
            resetConfirm.SetActive(false);
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
            if (campfire != null)
            {
                campfire.OnCharactersChanged += HandleCharactersChanged;
                campfire.OnSkillTreeChanged += HandleSkillTreeChanged;
            }
            if (hintDevice != null) hintDevice.OnDeviceChanged += HandleDeviceChanged;
        }

        void OnDisable()
        {
            if (hub != null)
            {
                hub.OnStationOpened -= HandleStationOpened;
                hub.OnStationClosed -= HandleStationClosed;
            }
            if (campfire != null)
            {
                campfire.OnCharactersChanged -= HandleCharactersChanged;
                campfire.OnSkillTreeChanged -= HandleSkillTreeChanged;
            }
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
                if (confirming) CloseResetConfirm();
                else if (hub != null) hub.CloseStation();
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
            SetConfirming(false);
            SetVisible(false);
            if (eventSystem != null) eventSystem.SetSelectedGameObject(null);
        }

        void HandleDeviceChanged(bool gamepad)
        {
            if (closeHint != null && data != null) closeHint.text = gamepad ? data.closeHintGamepad : data.closeHintKeyboard;
        }

        // ---------- 角色清單 ----------

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

        // ---------- 技能樹 ----------

        void HandleSkillTreeChanged(Campfire.SkillTreeView view)
        {
            if (skillCharacter != view.character) focusedNode = null; // 換角色：換了一棵樹
            skillCharacter = view.character;
            points = view.points;
            spent = view.spent;
            skillEntries.Clear();
            skillEntries.AddRange(view.nodes);
            if (data == null) return;

            pointsText.text = string.Format(data.skillPointsFormat, points);
            bool hasTree = view.tree != null && skillEntries.Count > 0;
            noTreeText.gameObject.SetActive(!hasTree);
            if (!hasTree) noTreeText.text = string.Format(data.noSkillTreeFormat, view.character != null ? view.character.displayName : string.Empty);
            resetButton.interactable = view.anyLearned;
            if (debugPointsButton != null) debugPointsButton.gameObject.SetActive(view.debugTools);

            LayoutNodes();
            if (focusedNode != null && IndexOf(focusedNode) < 0) focusedNode = null;
            ShowDetail();

            if (visible && !onCharacters && !confirming && eventSystem != null)
            {
                var selected = eventSystem.currentSelectedGameObject;
                if (selected == null || !selected.activeInHierarchy || (selected.TryGetComponent(out Selectable s) && !s.IsInteractable()))
                    Select(DefaultSelectable());
            }
        }

        // 節點依格子座標擺（整組在左邊的區域裡水平置中），前置用線連
        void LayoutNodes()
        {
            while (nodeCells.Count < skillEntries.Count)
            {
                var cell = Instantiate(nodeTemplate, nodeContainer);
                int index = nodeCells.Count;
                cell.Button.onClick.AddListener(() => Focus(index));
                cell.OnFocused += HandleNodeFocused;
                nodeCells.Add(cell);
            }
            int columns = 1;
            foreach (var entry in skillEntries) columns = Mathf.Max(columns, entry.node.gridPosition.x + 1);
            var size = data.skillNodeSize;
            var spacing = data.skillNodeSpacing;
            float left = (nodeContainer.rect.width - (columns - 1) * spacing.x) * 0.5f;
            float top = size.y * 0.5f;

            int lineCount = 0;
            for (int i = 0; i < nodeCells.Count; i++)
            {
                var cell = nodeCells[i];
                bool used = i < skillEntries.Count;
                cell.gameObject.SetActive(used);
                if (!used) continue;
                var entry = skillEntries[i];
                cell.name = "Node_" + entry.node.id;
                cell.Rect.sizeDelta = size;
                cell.Rect.anchoredPosition = NodeCenter(entry.node, left, top);
                FillNode(cell, entry);

                foreach (var id in entry.node.prerequisites)
                {
                    int from = IndexOfId(id);
                    if (from < 0) continue;
                    var line = Line(lineCount++);
                    var a = NodeCenter(skillEntries[from].node, left, top);
                    var b = cell.Rect.anchoredPosition;
                    // 線只畫在兩格的邊之間：不能學的節點是半透明的，線穿過去會看得到
                    var dir = (b - a).normalized;
                    float edge = Mathf.Min(Mathf.Abs(dir.x) > 1e-4f ? size.x * 0.5f / Mathf.Abs(dir.x) : float.MaxValue,
                                           Mathf.Abs(dir.y) > 1e-4f ? size.y * 0.5f / Mathf.Abs(dir.y) : float.MaxValue);
                    if (edge * 2f < (b - a).magnitude)
                    {
                        a += dir * edge;
                        b -= dir * edge;
                    }
                    line.rectTransform.anchoredPosition = (a + b) * 0.5f;
                    line.rectTransform.sizeDelta = new Vector2((b - a).magnitude, data.skillLineWidth);
                    line.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg);
                    line.color = skillEntries[from].state == SkillNodeState.Learned ? data.skillLineLearnedColor : data.skillLineColor;
                }
            }
            for (int i = 0; i < lines.Count; i++) lines[i].gameObject.SetActive(i < lineCount);
        }

        // 容器的錨點在左上：x 往右、y 往下是負的
        Vector2 NodeCenter(SkillNode node, float left, float top) =>
            new Vector2(left + node.gridPosition.x * data.skillNodeSpacing.x, -(top + node.gridPosition.y * data.skillNodeSpacing.y));

        Image Line(int index)
        {
            while (lines.Count <= index) lines.Add(Instantiate(lineTemplate, lineContainer));
            lines[index].gameObject.SetActive(true);
            return lines[index];
        }

        void FillNode(SkillNodeCellUI cell, Campfire.SkillEntry entry)
        {
            var node = entry.node;
            bool learned = entry.state == SkillNodeState.Learned;
            string cost = learned ? data.skillLearnedTag : string.Format(data.skillCostFormat, node.cost);
            string text = $"<b>{node.displayName}</b>\n<size={Mathf.RoundToInt(cell.GetComponentInChildren<Text>(true).fontSize * 0.75f)}>{cost}</size>";
            switch (entry.state)
            {
                case SkillNodeState.Learned:
                    cell.Set(node.icon, text, data.skillLearnedColor, data.skillLearnedTextColor, null, 1f);
                    break;
                case SkillNodeState.Available:
                    cell.Set(node.icon, text, data.buttonColor, data.textColor, data.skillAvailableFrameColor, 1f);
                    break;
                default:
                    cell.Set(node.icon, text, data.buttonColor, data.textColor, null, data.skillLockedAlpha);
                    break;
            }
        }

        void HandleNodeFocused(SkillNodeCellUI cell)
        {
            int index = nodeCells.IndexOf(cell);
            if (index >= 0 && index < skillEntries.Count) Focus(index);
        }

        void Focus(int index)
        {
            if (index < 0 || index >= skillEntries.Count) return;
            focusedNode = skillEntries[index].node;
            ShowDetail();
        }

        void ShowDetail()
        {
            detailName.text = detailStatus.text = detailBody.text = detailDiff.text = string.Empty;
            int index = focusedNode != null ? IndexOf(focusedNode) : -1;
            learnButton.gameObject.SetActive(index >= 0);
            if (index < 0)
            {
                if (skillEntries.Count > 0) detailBody.text = data.skillSelectHint;
                return;
            }
            var entry = skillEntries[index];
            var node = entry.node;
            detailName.text = node.displayName;
            detailBody.text = node.description;
            learnButton.interactable = entry.state == SkillNodeState.Available;
            switch (entry.state)
            {
                case SkillNodeState.Learned:
                    detailStatus.text = Colored(data.skillLearnedText, data.skillLearnedColor);
                    return; // 學過了：沒有差異可看
                case SkillNodeState.Available:
                    detailStatus.text = string.Format(data.skillAvailableFormat, node.cost);
                    break;
                case SkillNodeState.MissingPrerequisite:
                    missing.Clear();
                    SkillTreeLoadout.MissingPrerequisites(skillCharacter, node, missing);
                    builder.Clear();
                    foreach (var m in missing)
                    {
                        if (builder.Length > 0) builder.Append(data.skillNameSeparator);
                        builder.Append(m.displayName);
                    }
                    detailStatus.text = Colored(string.Format(data.skillMissingPrerequisiteFormat, builder.ToString()), data.worseColor);
                    break;
                case SkillNodeState.NotEnoughPoints:
                    detailStatus.text = Colored(string.Format(data.skillNotEnoughPointsFormat, node.cost, points), data.worseColor);
                    break;
            }
            detailDiff.text = DiffText(campfire != null ? campfire.PreviewNode(node) : null);
        }

        string DiffText(Campfire.SkillPreview preview)
        {
            if (preview == null) return string.Empty;
            builder.Clear();
            builder.Append(data.learnPreviewHeader);
            if (preview.stats.Count + preview.addedEffects.Count == 0)
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
            return builder.ToString();
        }

        static string Colored(string text, Color color) => $"<color=#{ColorUtility.ToHtmlStringRGBA(color)}>{text}</color>";

        // 學完「學習」按鈕會停用：選回剛學的節點，鍵盤、手把才導覽得下去
        void HandleLearnClicked()
        {
            if (campfire == null || focusedNode == null) return;
            var node = focusedNode;
            if (campfire.Learn(node)) Select(CellOf(node));
        }

        void OpenResetConfirm()
        {
            if (data == null || skillCharacter == null) return;
            resetConfirmText.text = string.Format(data.resetSkillsConfirmFormat, skillCharacter.displayName, spent);
            SetConfirming(true);
            Select(resetNoButton);
        }

        void HandleResetConfirmed()
        {
            if (campfire != null) campfire.ResetSkills();
            SetConfirming(false);
            Select(DefaultSelectable());
        }

        void CloseResetConfirm()
        {
            SetConfirming(false);
            Select(resetButton.IsInteractable() ? resetButton : DefaultSelectable());
        }

        void SetConfirming(bool on)
        {
            confirming = on;
            resetConfirm.SetActive(on);
            treeGroup.interactable = !on; // 頁籤照常能點：換頁時關掉確認框
        }

        int IndexOf(SkillNode node)
        {
            for (int i = 0; i < skillEntries.Count; i++)
            {
                if (skillEntries[i].node == node) return i;
            }
            return -1;
        }

        int IndexOfId(string id)
        {
            if (string.IsNullOrEmpty(id)) return -1;
            for (int i = 0; i < skillEntries.Count; i++)
            {
                if (skillEntries[i].node.id == id) return i;
            }
            return -1;
        }

        Selectable CellOf(SkillNode node)
        {
            int index = IndexOf(node);
            return index >= 0 && index < nodeCells.Count ? nodeCells[index].Button : null;
        }

        // ---------- 頁面 ----------

        void ShowPage(bool characters)
        {
            if (confirming) SetConfirming(false);
            onCharacters = characters;
            charactersPage.SetActive(characters);
            skillTreePage.SetActive(!characters);
            SetTabColor(charactersTab, characters);
            SetTabColor(skillTreeTab, !characters);
            if (!characters) LayoutNodes(); // 頁面第一次打開前 rect 還沒算好：打開時再排一次
            Select(DefaultSelectable());
        }

        void SetTabColor(Button tab, bool active)
        {
            if (data == null) return;
            var colors = tab.colors;
            colors.normalColor = active ? data.tabActiveColor : data.buttonColor;
            tab.colors = colors;
        }

        // 角色頁選目前角色那一列；技能樹頁選剛才的節點、沒有時第一個節點，再沒有時頁籤
        Selectable DefaultSelectable()
        {
            if (onCharacters)
            {
                if (currentRow >= 0 && currentRow < rows.Count && rows[currentRow].gameObject.activeSelf) return rows[currentRow];
                return charactersTab;
            }
            if (confirming) return resetNoButton;
            var focused = focusedNode != null ? CellOf(focusedNode) : null;
            if (focused != null) return focused;
            return skillEntries.Count > 0 && nodeCells.Count > 0 ? nodeCells[0].Button : skillTreeTab;
        }

        void Select(Selectable selectable)
        {
            if (eventSystem != null && selectable != null) eventSystem.SetSelectedGameObject(selectable.gameObject);
        }

        static void SetLabel(Button button, string text)
        {
            var label = button.GetComponentInChildren<Text>(true);
            if (label != null) label.text = text;
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
