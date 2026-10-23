using System;
using UnityEngine;

namespace BossFight.UI
{
    /// <summary>
    /// 按鍵的顯示名稱：改鍵頁、頭暈提示（按 F 處決）、跳過提示（長按 Space 跳過）共用。
    /// 綁定路徑（例如 &lt;Mouse&gt;/leftButton、&lt;Gamepad&gt;/buttonSouth）在對照表裡就用表上的名稱，沒列到的用 Input System 的名稱。
    /// 手把用 Xbox 的按鍵名稱。
    /// </summary>
    [CreateAssetMenu(menuName = "BossFight/Input Name Data", fileName = "InputNameData")]
    public class InputNameData : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            [Tooltip("綁定路徑，大小寫不拘，例如 <Mouse>/leftButton")]
            public string path;
            public string label;

            public Entry(string path, string label)
            {
                this.path = path;
                this.label = label;
            }
        }

        [Tooltip("綁定路徑 → 顯示名稱")]
        public Entry[] names =
        {
            new Entry("<Mouse>/leftButton", "滑鼠左鍵"),
            new Entry("<Mouse>/rightButton", "滑鼠右鍵"),
            new Entry("<Mouse>/middleButton", "滑鼠中鍵"),
            new Entry("<Mouse>/forwardButton", "滑鼠側鍵（前）"),
            new Entry("<Mouse>/backButton", "滑鼠側鍵（後）"),
            new Entry("<Keyboard>/upArrow", "↑"),
            new Entry("<Keyboard>/downArrow", "↓"),
            new Entry("<Keyboard>/leftArrow", "←"),
            new Entry("<Keyboard>/rightArrow", "→"),
            new Entry("<Gamepad>/buttonSouth", "A"),
            new Entry("<Gamepad>/buttonEast", "B"),
            new Entry("<Gamepad>/buttonWest", "X"),
            new Entry("<Gamepad>/buttonNorth", "Y"),
            new Entry("<Gamepad>/leftShoulder", "LB"),
            new Entry("<Gamepad>/rightShoulder", "RB"),
            new Entry("<Gamepad>/leftTrigger", "LT"),
            new Entry("<Gamepad>/rightTrigger", "RT"),
            new Entry("<Gamepad>/leftStickPress", "L3（按下左搖桿）"),
            new Entry("<Gamepad>/rightStickPress", "R3（按下右搖桿）"),
            new Entry("<Gamepad>/dpad/up", "十字鍵 ↑"),
            new Entry("<Gamepad>/dpad/down", "十字鍵 ↓"),
            new Entry("<Gamepad>/dpad/left", "十字鍵 ←"),
            new Entry("<Gamepad>/dpad/right", "十字鍵 →"),
            new Entry("<Gamepad>/select", "View"),
        };
        [Tooltip("沒有綁定時顯示")]
        public string emptyLabel = "—";

        public bool TryGet(string path, out string label)
        {
            foreach (var entry in names)
            {
                if (string.Equals(entry.path, path, StringComparison.OrdinalIgnoreCase))
                {
                    label = entry.label;
                    return true;
                }
            }
            label = null;
            return false;
        }
    }
}
