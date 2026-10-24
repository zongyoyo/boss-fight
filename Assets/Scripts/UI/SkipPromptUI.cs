using BossFight.Cutscene;
using UnityEngine;
using UnityEngine.UI;

namespace BossFight.UI
{
    /// <summary>
    /// 右下角的「長按 Space 跳過」與進度條。只訂閱 CutsceneDirector（開場、轉階段，可以接好幾段）：可以跳過時淡入，按住時進度條填滿，放開歸零，跳過或演出結束時淡出。
    /// Space 是 Player/SkipCutscene 目前裝置（InputHintDevice）的按鍵，出現時、換裝置、改鍵後重新讀。
    /// 第一次看到的演出不能跳過，不會出現。這裡的數字全是視覺參數。
    /// </summary>
    [DisallowMultipleComponent]
    public class SkipPromptUI : MonoBehaviour
    {
        [Tooltip("可以跳過的演出（開場、轉階段）")]
        [SerializeField] CutsceneDirector[] sources;
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
        [Tooltip("目前用鍵鼠還是手把；留空時一律鍵鼠")]
        [SerializeField] InputHintDevice hintDevice;

        bool visible, gamepad;

        void Awake()
        {
            group.alpha = 0f;
            fill.fillAmount = 0f;
        }

        void OnEnable()
        {
            if (sources != null)
            {
                foreach (var source in sources)
                {
                    if (source == null) continue;
                    source.OnSkipAvailableChanged += HandleAvailable;
                    source.OnSkipProgress += HandleProgress;
                }
            }
            if (hintDevice != null) hintDevice.OnDeviceChanged += HandleDeviceChanged;
        }

        void OnDisable()
        {
            if (sources != null)
            {
                foreach (var source in sources)
                {
                    if (source == null) continue;
                    source.OnSkipAvailableChanged -= HandleAvailable;
                    source.OnSkipProgress -= HandleProgress;
                }
            }
            if (hintDevice != null) hintDevice.OnDeviceChanged -= HandleDeviceChanged;
        }

        void HandleAvailable(bool available)
        {
            visible = available;
            if (!available) return;
            fill.fillAmount = 0f;
            RefreshLabel();
        }

        void HandleDeviceChanged(bool usingGamepad)
        {
            gamepad = usingGamepad;
            RefreshLabel();
        }

        void RefreshLabel()
        {
            if (label != null) label.text = string.Format(labelFormat, BindingText.FirstDisplay("Player/SkipCutscene", BindingText.Group(gamepad), inputNames));
        }

        void HandleProgress(float progress) => fill.fillAmount = progress;

        void Update()
        {
            group.alpha = Mathf.MoveTowards(group.alpha, visible ? 1f : 0f, Time.unscaledDeltaTime / fadeTime);
        }
    }
}
