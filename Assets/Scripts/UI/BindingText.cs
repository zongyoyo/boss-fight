using System;
using UnityEngine.InputSystem;

namespace BossFight.UI
{
    /// <summary>
    /// 找 Input Action 的綁定與顯示名稱（改鍵頁、按鍵提示共用）。控制組名稱照 InputSystem_Actions 的 Control Scheme。
    /// 只讀 InputSystem.actions，不存任何狀態。
    /// </summary>
    public static class BindingText
    {
        public const string KeyboardGroup = "Keyboard&Mouse";
        public const string GamepadGroup = "Gamepad";

        /// <summary>
        /// 動作在 group 裡第 slot 個綁定（0 = 主要、1 = 次要）的索引；compositePart 不是空的時找組合鍵裡同名的部分（Move 的 up／down／left／right）。
        /// 找不到時回傳 -1。
        /// </summary>
        public static int FindBinding(InputAction action, string group, string compositePart, int slot)
        {
            bool part = !string.IsNullOrEmpty(compositePart);
            var bindings = action.bindings;
            int count = 0;
            for (int i = 0; i < bindings.Count; i++)
            {
                var b = bindings[i];
                if (b.isComposite || b.isPartOfComposite != part || !InGroup(b, group)) continue;
                if (part && !string.Equals(b.name, compositePart, StringComparison.OrdinalIgnoreCase)) continue;
                if (count++ == slot) return i;
            }
            return -1;
        }

        public static bool InGroup(InputBinding binding, string group)
        {
            if (string.IsNullOrEmpty(binding.groups)) return false;
            foreach (var g in binding.groups.Split(';'))
            {
                if (g == group) return true;
            }
            return false;
        }

        /// <summary>某個綁定的顯示名稱：先查 names 的對照表，沒有就用 Input System 的名稱；沒有綁定時是 names.emptyLabel。</summary>
        public static string Display(InputAction action, int bindingIndex, InputNameData names)
        {
            string empty = names != null ? names.emptyLabel : string.Empty;
            if (action == null || bindingIndex < 0) return empty;
            string path = action.bindings[bindingIndex].effectivePath;
            if (string.IsNullOrEmpty(path)) return empty;
            if (names != null && names.TryGet(path, out var label)) return label;
            return action.GetBindingDisplayString(bindingIndex, InputBinding.DisplayStringOptions.DontIncludeInteractions);
        }

        /// <summary>動作在 group 裡第一個有綁定的按鍵名稱（按鍵提示用）；actionPath 找不到或都沒有綁定時是 names.emptyLabel。</summary>
        public static string FirstDisplay(string actionPath, string group, InputNameData names)
        {
            var action = InputSystem.actions != null ? InputSystem.actions.FindAction(actionPath, false) : null;
            if (action != null)
            {
                var bindings = action.bindings;
                for (int i = 0; i < bindings.Count; i++)
                {
                    var b = bindings[i];
                    if (!b.isComposite && InGroup(b, group) && !string.IsNullOrEmpty(b.effectivePath)) return Display(action, i, names);
                }
            }
            return names != null ? names.emptyLabel : string.Empty;
        }
    }
}
