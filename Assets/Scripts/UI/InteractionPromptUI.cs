using BossFight.Hub;
using UnityEngine;
using UnityEngine.UI;

namespace BossFight.UI
{
    /// <summary>
    /// 休息區畫面下方的互動提示「按 [E] 對話／鍛造／休息／出發」（T20）。只訂閱事件：
    /// InteractionScanner.OnFocusChanged（走進範圍出現、離開消失）、HubManager 的站點開關（介面開著時藏起來）、InputHintDevice（鍵鼠或手把的鍵，改鍵後重讀）。
    /// 鍵名用 BindingText.FirstDisplay 讀 Player/Interact 目前的綁定；文字格式與動詞在 HubUIData。
    /// </summary>
    [DisallowMultipleComponent]
    public class InteractionPromptUI : MonoBehaviour
    {
        [SerializeField] HubUIData data;
        [SerializeField] InteractionScanner scanner;
        [SerializeField] HubManager hub;
        [Tooltip("互動鍵的顯示名稱")]
        [SerializeField] InputNameData inputNames;
        [Tooltip("目前用鍵鼠還是手把；留空時一律鍵鼠")]
        [SerializeField] InputHintDevice hintDevice;
        [SerializeField] Text label;

        Interactable focus;
        bool stationOpen, gamepad;

        void Awake() => Refresh();

        void OnEnable()
        {
            if (scanner != null) scanner.OnFocusChanged += HandleFocusChanged;
            if (hub != null)
            {
                hub.OnStationOpened += HandleStationOpened;
                hub.OnStationClosed += HandleStationClosed;
            }
            if (hintDevice != null) hintDevice.OnDeviceChanged += HandleDeviceChanged;
        }

        void OnDisable()
        {
            if (scanner != null) scanner.OnFocusChanged -= HandleFocusChanged;
            if (hub != null)
            {
                hub.OnStationOpened -= HandleStationOpened;
                hub.OnStationClosed -= HandleStationClosed;
            }
            if (hintDevice != null) hintDevice.OnDeviceChanged -= HandleDeviceChanged;
        }

        void HandleFocusChanged(Interactable target)
        {
            focus = target;
            Refresh();
        }

        void HandleStationOpened(Interactable target)
        {
            stationOpen = true;
            Refresh();
        }

        void HandleStationClosed(Interactable target)
        {
            stationOpen = false;
            Refresh();
        }

        void HandleDeviceChanged(bool value)
        {
            gamepad = value;
            Refresh();
        }

        void Refresh()
        {
            if (label == null) return;
            var text = focus != null && data != null ? data.Get(focus.Kind) : null;
            bool show = text != null && !stationOpen;
            label.enabled = show;
            if (!show) return;
            string key = BindingText.FirstDisplay("Player/Interact", BindingText.Group(gamepad), inputNames);
            label.text = string.Format(data.promptFormat, key, text.verb);
        }
    }
}
