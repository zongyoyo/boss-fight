using System;
using System.Collections.Generic;
using BossFight.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UI;

namespace BossFight.UI
{
    /// <summary>
    /// ESC 選單的改鍵頁（鍵鼠一頁、控制器一頁）。Awake 時依 PauseMenuData.bindings 複製列範本：鍵鼠頁有主要、次要兩欄（Keyboard&amp;Mouse 控制組裡的第 1、2 個綁定），
    /// 控制器頁一欄（Gamepad 控制組）。改的是 InputSystem.actions 的 binding override，改完由 GameOptions.SaveBindings 存檔，立刻生效。
    /// <para>
    /// 改鍵流程：點一格 → 等所有按鍵放開（點擊用的滑鼠左鍵、手把 A 不會被當成新鍵）→ PerformInteractiveRebinding 等下一個按鍵。
    /// 鍵鼠頁只收鍵盤按鍵與滑鼠按鈕，控制器頁只收手把按鈕（搖桿方向不收）；Esc、Start 保留給選單，按了是取消；鍵鼠頁的次要欄按 Delete 清空（主要欄不能空著）。
    /// 新鍵和這一頁其他格同鍵時互換；次要欄原本空著時，對方是次要鍵就移過來、是主要鍵就不換（主要欄不能空著）。checkConflict 關掉的列（長按跳過開場）不檢查。
    /// 等待按鍵時整頁不能互動、EventSystem 不送導覽事件（按 Enter、A 不會又點到格子）；結束後等按鍵放開才恢復，IsBusy 期間選單不把 Esc、Start、B 當成返回。
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public class KeyBindingPageUI : MonoBehaviour
    {
        // 鍵鼠頁不收的控制：Esc（選單、取消）、Delete（清空次要欄）、滑鼠移動與滾輪
        static readonly string[] KeyboardExcluded =
        {
            "<Keyboard>/escape", "<Keyboard>/delete", "<Keyboard>/anyKey",
            "<Mouse>/position", "<Mouse>/delta", "<Mouse>/delta/*", "<Mouse>/scroll", "<Mouse>/scroll/*", "<Mouse>/press", "<Pointer>/press",
        };
        // 控制器頁不收的控制：Start（選單、取消）、搖桿方向（移動與視角固定在搖桿上）
        static readonly string[] GamepadExcluded =
        {
            "<Gamepad>/start", "<Gamepad>/leftStick", "<Gamepad>/leftStick/*", "<Gamepad>/rightStick", "<Gamepad>/rightStick/*",
        };

        [SerializeField] PauseMenuData data;
        [SerializeField] InputNameData inputNames;
        [Tooltip("改完存檔")]
        [SerializeField] GameOptions options;
        [Tooltip("勾選 = 控制器頁（Gamepad 控制組、一欄）；不勾 = 鍵鼠頁（Keyboard&Mouse 控制組、主要／次要兩欄）")]
        [SerializeField] bool gamepad;
        [Tooltip("一列的範本（不啟用）；Awake 時依 PauseMenuData.bindings 複製到同一個父物件底下")]
        [SerializeField] KeyBindingRowUI rowTemplate;
        [Tooltip("操作提示與改鍵結果")]
        [SerializeField] Text statusText;
        [SerializeField] Button resetButton;
        [SerializeField] Button backButton;
        [Tooltip("等待按鍵時關掉整頁的互動")]
        [SerializeField] CanvasGroup group;
        [SerializeField] EventSystem eventSystem;

        /// <summary>按下這一頁的「返回」。</summary>
        public event Action OnBack;

        /// <summary>等待按鍵中，或剛結束、按鍵還沒放開：選單不要把 Esc、Start、B 當成返回。</summary>
        public bool IsBusy => pending != null || operation != null || waitRelease;
        /// <summary>進這一頁時選取的第一格。</summary>
        public Selectable FirstSelectable => cells.Count > 0 ? cells[0].button : backButton;

        sealed class Cell
        {
            public PauseMenuData.BindingEntry entry;
            public InputAction action;
            public int index;
            public int slot;
            public Button button;
            public Text text;
            public string Path => action.bindings[index].effectivePath;
        }

        readonly List<Cell> cells = new List<Cell>();
        InputActionRebindingExtensions.RebindingOperation operation;
        Cell pending, lastCell;
        bool waitRelease, clearRequested, actionWasEnabled;
        string oldPath, notice;

        string Group => gamepad ? BindingText.GamepadGroup : BindingText.KeyboardGroup;
        int Slots => gamepad ? 1 : 2;

        void Awake()
        {
            rowTemplate.gameObject.SetActive(false);
            resetButton.onClick.AddListener(ResetToDefaults);
            backButton.onClick.AddListener(Back);
            var actions = InputSystem.actions;
            if (actions == null) return;

            foreach (var entry in data.bindings)
            {
                if (gamepad ? !entry.gamepad : !entry.keyboard) continue;
                var action = actions.FindAction(entry.action, false);
                if (action == null)
                {
                    Debug.LogWarning($"[PauseMenu] 改鍵清單的動作 {entry.action} 不在 Input Actions 裡，略過。", this);
                    continue;
                }
                var row = Instantiate(rowTemplate, rowTemplate.transform.parent);
                row.name = $"Row_{entry.action.Replace('/', '_')}{(string.IsNullOrEmpty(entry.compositePart) ? "" : "_" + entry.compositePart)}";
                row.gameObject.SetActive(true);
                row.Label.text = entry.label;
                for (int slot = 0; slot < row.SlotCount; slot++)
                {
                    var button = row.Slot(slot);
                    var text = row.SlotText(slot);
                    if (slot >= Slots)
                    {
                        button.gameObject.SetActive(false);
                        continue;
                    }
                    int index = BindingText.FindBinding(action, Group, entry.compositePart, slot);
                    if (index < 0)
                    {
                        // Input Actions 裡沒有這一格的綁定（要能改的話先在 InputSystem_Actions 補一個空綁定）
                        button.interactable = false;
                        text.text = inputNames.emptyLabel;
                        continue;
                    }
                    var cell = new Cell { entry = entry, action = action, index = index, slot = slot, button = button, text = text };
                    button.onClick.AddListener(() => Begin(cell));
                    cells.Add(cell);
                }
            }
        }

        void OnEnable()
        {
            notice = null;
            Refresh();
        }

        void OnDisable()
        {
            pending = null;
            if (operation != null) operation.Cancel(); // → End
            waitRelease = false;
            SetInteractive(true);
        }

        void Back()
        {
            if (!IsBusy) OnBack?.Invoke();
        }

        void Update()
        {
            if (pending != null)
            {
                if (CancelPressed()) EndPending();
                else if (!AnyButtonHeld()) StartOperation();
                return;
            }
            if (operation != null)
            {
                // 另一種裝置的取消鍵、次要欄的 Delete（WithCancelingThrough 只能指定一個）
                var kb = Keyboard.current;
                if (!gamepad && lastCell != null && lastCell.slot > 0 && kb != null && kb.deleteKey.wasPressedThisFrame) clearRequested = true;
                if (clearRequested || CancelPressed()) operation.Cancel();
                return;
            }
            if (waitRelease && !AnyButtonHeld())
            {
                waitRelease = false;
                SetInteractive(true);
                if (eventSystem != null && lastCell != null) eventSystem.SetSelectedGameObject(lastCell.button.gameObject);
            }
        }

        // ---------- 改鍵 ----------

        void Begin(Cell cell)
        {
            if (IsBusy) return;
            pending = lastCell = cell;
            notice = null;
            SetInteractive(false);
            cell.text.text = data.listeningText;
            cell.text.color = data.listeningColor;
            statusText.text = gamepad ? data.gamepadListeningHint : cell.slot > 0 ? data.keyboardListeningHintSecondary : data.keyboardListeningHint;
            statusText.color = data.hintColor;
        }

        // 點擊的按鍵放開了才開始等新鍵
        void StartOperation()
        {
            var cell = pending;
            pending = null;
            clearRequested = false;
            oldPath = cell.Path;
            actionWasEnabled = cell.action.enabled;
            cell.action.Disable(); // 改鍵時動作要停用

            var op = cell.action.PerformInteractiveRebinding(cell.index)
                .WithCancelingThrough(gamepad ? "<Gamepad>/start" : "<Keyboard>/escape")
                .OnComplete(_ => End(cell, true))
                .OnCancel(_ => End(cell, false));
            if (gamepad)
            {
                op.WithControlsHavingToMatchPath("<Gamepad>");
            }
            else
            {
                op.WithControlsHavingToMatchPath("<Keyboard>");
                op.WithControlsHavingToMatchPath("<Mouse>");
            }
            foreach (var path in gamepad ? GamepadExcluded : KeyboardExcluded) op.WithControlsExcluding(path);
            operation = op.Start();
        }

        void EndPending()
        {
            pending = null;
            waitRelease = true;
            Refresh();
        }

        void End(Cell cell, bool completed)
        {
            operation?.Dispose();
            operation = null;

            if (clearRequested)
            {
                cell.action.ApplyBindingOverride(cell.index, string.Empty);
                completed = true;
            }
            clearRequested = false;
            if (completed)
            {
                ResolveConflict(cell);
                if (options != null) options.SaveBindings();
            }
            if (actionWasEnabled) cell.action.Enable();
            waitRelease = true;
            Refresh();
        }

        void ResolveConflict(Cell cell)
        {
            string path = cell.Path;
            if (string.IsNullOrEmpty(path) || !cell.entry.checkConflict) return;
            Cell other = null;
            foreach (var c in cells)
            {
                if (c != cell && c.entry.checkConflict && string.Equals(c.Path, path, StringComparison.OrdinalIgnoreCase))
                {
                    other = c;
                    break;
                }
            }
            if (other == null) return;

            string previous = oldPath ?? string.Empty;
            if (previous.Length == 0 && other.slot == 0)
            {
                cell.action.ApplyBindingOverride(cell.index, previous); // 對方的主要欄不能空著：不換
                notice = string.Format(data.inUseFormat, other.entry.label);
                return;
            }
            other.action.ApplyBindingOverride(other.index, previous);
            notice = string.Format(previous.Length == 0 ? data.movedFormat : data.swappedFormat, other.entry.label);
        }

        void ResetToDefaults()
        {
            if (IsBusy) return;
            foreach (var cell in cells) cell.action.RemoveBindingOverride(cell.index);
            if (options != null) options.SaveBindings();
            notice = data.resetDoneText;
            Refresh();
        }

        // ---------- 畫面 ----------

        void Refresh()
        {
            foreach (var cell in cells)
            {
                cell.text.text = BindingText.Display(cell.action, cell.index, inputNames);
                cell.text.color = data.textColor;
            }
            statusText.text = notice ?? (gamepad ? data.gamepadHint : data.keyboardHint);
            statusText.color = notice != null ? data.noticeColor : data.hintColor;
        }

        void SetInteractive(bool on)
        {
            if (group != null) group.interactable = on;
            if (eventSystem != null) eventSystem.sendNavigationEvents = on;
        }

        static bool CancelPressed()
        {
            var kb = Keyboard.current;
            var pad = Gamepad.current;
            return (kb != null && kb.escapeKey.wasPressedThisFrame) || (pad != null && pad.startButton.wasPressedThisFrame);
        }

        static bool AnyButtonHeld()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.anyKey.isPressed) return true;
            var mouse = Mouse.current;
            if (mouse != null && (mouse.leftButton.isPressed || mouse.rightButton.isPressed || mouse.middleButton.isPressed ||
                                  mouse.forwardButton.isPressed || mouse.backButton.isPressed)) return true;
            foreach (var pad in Gamepad.all)
            {
                foreach (var control in pad.allControls)
                {
                    if (control is ButtonControl button && button.isPressed) return true;
                }
            }
            return false;
        }
    }
}
