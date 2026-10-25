using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BossFight.UI
{
    /// <summary>
    /// 鐵匠鋪（T22）的一格：左邊的裝備欄或右邊的一件裝備。HubUIBuilder 建樣板時接好子物件，BlacksmithUI 複製後填內容。
    /// 被選到（鍵盤、手把導覽）或滑鼠移上去時送 OnFocused，BlacksmithUI 用來更新下方的說明與差異預覽。
    /// </summary>
    [DisallowMultipleComponent]
    public class EquipmentCellUI : MonoBehaviour, ISelectHandler, IPointerEnterHandler
    {
        [SerializeField] Button button;
        [SerializeField] Image icon;
        [SerializeField] Text label;
        [Tooltip("「新」標記（欄位沒有）")]
        [SerializeField] GameObject newBadge;
        [Tooltip("「裝備中」標籤（欄位沒有）")]
        [SerializeField] GameObject equippedTag;
        [Tooltip("裝備中的邊框")]
        [SerializeField] Outline frame;

        /// <summary>被選到或滑鼠移上去。</summary>
        public event Action<EquipmentCellUI> OnFocused;

        public Button Button => button;
        public Image Icon => icon;
        public Text Label => label;

        public void OnSelect(BaseEventData eventData) => OnFocused?.Invoke(this);

        public void OnPointerEnter(PointerEventData eventData) => OnFocused?.Invoke(this);

        /// <summary>「新」標記、「裝備中」標籤與邊框（highlight：邊框顏色）。</summary>
        public void SetMarks(bool isNew, bool equipped, Color highlight)
        {
            if (newBadge != null) newBadge.SetActive(isNew);
            if (equippedTag != null) equippedTag.SetActive(equipped);
            if (frame != null)
            {
                frame.enabled = equipped;
                frame.effectColor = highlight;
            }
        }
    }
}
