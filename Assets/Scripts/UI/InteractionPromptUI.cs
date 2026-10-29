using BossFight.Hub;
using UnityEngine;
using UnityEngine.UI;

namespace BossFight.UI
{
    /// <summary>
    /// 休息區畫面下方的互動提示「按 [E] 對話／鍛造／休息／出發」（T20）。只訂閱事件：
    /// InteractionScanner.OnFocusChanged（走進範圍出現、離開消失）、HubManager 的站點開關（介面開著時藏起來）、InputHintDevice（鍵鼠或手把的鍵，改鍵後重讀）。
    /// 鍵名用 BindingText.FirstDisplay 讀 Player/Interact 目前的綁定；文字格式與動詞在 HubUIData。
    /// 村長頭上有「!」或「?」時（VillageChief.OnMarkerChanged），對村長的動詞換成「接任務」「回報任務」。
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
        [Tooltip("村長（頭上的標記決定提示的動詞）；留空時一律「對話」")]
        [SerializeField] VillageChief chief;

        Interactable focus;
        VillageChief.QuestMarker chiefMarker;
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
            if (chief != null) chief.OnMarkerChanged += HandleMarkerChanged;
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
            if (chief != null) chief.OnMarkerChanged -= HandleMarkerChanged;
        }

        void HandleMarkerChanged(VillageChief.QuestMarker marker, Transform anchor)
        {
            chiefMarker = marker;
            Refresh();
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
            string verb = text.verb;
            if (focus == chief && chief != null)
            {
                if (chiefMarker == VillageChief.QuestMarker.NewQuest) verb = data.chiefNewQuestVerb;
                else if (chiefMarker == VillageChief.QuestMarker.TurnIn) verb = data.chiefTurnInVerb;
            }
            label.text = string.Format(data.promptFormat, key, verb);
        }
    }
}
