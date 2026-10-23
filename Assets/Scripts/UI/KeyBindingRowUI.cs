using UnityEngine;
using UnityEngine.UI;

namespace BossFight.UI
{
    /// <summary>改鍵頁的一列：動作名稱與一或兩格按鍵（主要、次要）。只放引用，邏輯在 KeyBindingPageUI。</summary>
    [DisallowMultipleComponent]
    public class KeyBindingRowUI : MonoBehaviour
    {
        [SerializeField] Text label;
        [Tooltip("按鍵格（主要、次要）")]
        [SerializeField] Button[] slots = new Button[0];
        [Tooltip("按鍵格上的文字，順序和 slots 一樣")]
        [SerializeField] Text[] slotTexts = new Text[0];

        public Text Label => label;
        public int SlotCount => Mathf.Min(slots.Length, slotTexts.Length);
        public Button Slot(int index) => slots[index];
        public Text SlotText(int index) => slotTexts[index];
    }
}
