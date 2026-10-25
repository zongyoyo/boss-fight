using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BossFight.UI
{
    /// <summary>
    /// 營火技能樹頁（T23）的一個節點：HubUIBuilder 建樣板時接好子物件，CampfireUI 複製後依狀態填內容（學過亮、可學有外框、不能學變暗）。
    /// 被選到（點擊、鍵盤或手把導覽）時送 OnFocused，CampfireUI 用來更新右邊的說明與「學習」按鈕。
    /// 不像鐵匠鋪那樣滑鼠移上去就選：滑鼠從節點移到「學習」按鈕的路上會經過別的節點。
    /// </summary>
    [DisallowMultipleComponent]
    public class SkillNodeCellUI : MonoBehaviour, ISelectHandler
    {
        [SerializeField] Button button;
        [SerializeField] Image icon;
        [SerializeField] Text label;
        [Tooltip("可以學的外框")]
        [SerializeField] Outline frame;
        [Tooltip("不能學時整格變暗")]
        [SerializeField] CanvasGroup group;

        /// <summary>被選到。</summary>
        public event Action<SkillNodeCellUI> OnFocused;

        public Button Button => button;
        public RectTransform Rect => (RectTransform)transform;

        public void OnSelect(BaseEventData eventData) => OnFocused?.Invoke(this);

        /// <summary>填內容：text 是名稱與花費（rich text），background／textColor 是底色與字色，frameColor 為 null 時不畫外框，alpha 是整格的不透明度。</summary>
        public void Set(Sprite sprite, string text, Color background, Color textColor, Color? frameColor, float alpha)
        {
            bool hasIcon = sprite != null;
            icon.enabled = hasIcon;
            icon.sprite = sprite;
            label.text = text;
            label.color = textColor;
            label.rectTransform.offsetMin = new Vector2(hasIcon ? icon.rectTransform.rect.width + 16f : 6f, label.rectTransform.offsetMin.y); // 有圖示時字往右讓
            var colors = button.colors;
            colors.normalColor = background;
            button.colors = colors;
            frame.enabled = frameColor.HasValue;
            if (frameColor.HasValue) frame.effectColor = frameColor.Value;
            group.alpha = alpha;
        }
    }
}
