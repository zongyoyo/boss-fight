using BossFight.Cutscene;
using UnityEngine;
using UnityEngine.UI;

namespace BossFight.UI
{
    /// <summary>
    /// 右下角的「長按 Space 跳過」與進度條。只訂閱 CutsceneDirector：可以跳過時淡入，按住時進度條填滿，放開歸零，跳過或開場結束時淡出。
    /// Space 是 Player/SkipCutscene 目前的鍵鼠按鍵，每次出現時重新讀（改鍵後跟著變）。
    /// 第一次挑戰不能跳過，不會出現。這裡的數字全是視覺參數。
    /// </summary>
    [DisallowMultipleComponent]
    public class SkipPromptUI : MonoBehaviour
    {
        [SerializeField] CutsceneDirector source;
        [SerializeField] CanvasGroup group;
        [Tooltip("進度條（Image Type = Filled）")]
        [SerializeField] Image fill;
        [Tooltip("淡入淡出時間（秒）")]
        [Min(0.01f)] [SerializeField] float fadeTime = 0.25f;
        [Tooltip("提示文字；留空時不改字")]
        [SerializeField] Text label;
        [Tooltip("{0} = 跳過鍵")]
        [SerializeField] string labelFormat = "長按 {0} 跳過";
        [Tooltip("跳過鍵的顯示名稱")]
        [SerializeField] InputNameData inputNames;

        bool visible;

        void Awake()
        {
            group.alpha = 0f;
            fill.fillAmount = 0f;
        }

        void OnEnable()
        {
            if (source == null) return;
            source.OnSkipAvailableChanged += HandleAvailable;
            source.OnSkipProgress += HandleProgress;
        }

        void OnDisable()
        {
            if (source == null) return;
            source.OnSkipAvailableChanged -= HandleAvailable;
            source.OnSkipProgress -= HandleProgress;
        }

        void HandleAvailable(bool available)
        {
            visible = available;
            if (!available) return;
            fill.fillAmount = 0f;
            if (label != null) label.text = string.Format(labelFormat, BindingText.FirstDisplay("Player/SkipCutscene", BindingText.KeyboardGroup, inputNames));
        }

        void HandleProgress(float progress) => fill.fillAmount = progress;

        void Update()
        {
            group.alpha = Mathf.MoveTowards(group.alpha, visible ? 1f : 0f, Time.unscaledDeltaTime / fadeTime);
        }
    }
}
