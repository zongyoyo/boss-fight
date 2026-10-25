using BossFight.Hub;
using UnityEngine;
using UnityEngine.UI;

namespace BossFight.UI
{
    /// <summary>
    /// 村長頭上的「!」（有可接的任務）與「?」（有可回報的任務）（T24）。只訂閱 VillageChief.OnMarkerChanged（標記與位置）與 HubManager 的站點開關（介面開著時藏起來）。
    /// 每幀用 viewCamera 把標記的位置投到畫面上、上下浮動（村長在鏡頭後方時隱藏）。文字、顏色、大小在 HubUIData 的「村長頭上的標記」。由 HubUIBuilder 建立與接線。
    /// </summary>
    [DisallowMultipleComponent]
    public class QuestMarkerUI : MonoBehaviour
    {
        [SerializeField] HubUIData data;
        [SerializeField] VillageChief chief;
        [SerializeField] HubManager hub;
        [Tooltip("投影用的鏡頭（Main Camera）")]
        [SerializeField] Camera viewCamera;
        [Tooltip("標記放在這個範圍裡（全螢幕）")]
        [SerializeField] RectTransform area;
        [SerializeField] Text label;

        VillageChief.QuestMarker marker;
        Transform anchor;
        bool stationOpen;

        void Awake()
        {
            if (data != null) label.fontSize = data.markerFontSize;
            label.enabled = false;
        }

        void OnEnable()
        {
            if (chief != null) chief.OnMarkerChanged += HandleMarkerChanged;
            if (hub != null)
            {
                hub.OnStationOpened += HandleStationOpened;
                hub.OnStationClosed += HandleStationClosed;
            }
        }

        void OnDisable()
        {
            if (chief != null) chief.OnMarkerChanged -= HandleMarkerChanged;
            if (hub != null)
            {
                hub.OnStationOpened -= HandleStationOpened;
                hub.OnStationClosed -= HandleStationClosed;
            }
        }

        void HandleMarkerChanged(VillageChief.QuestMarker value, Transform position)
        {
            marker = value;
            anchor = position;
            if (data == null) return;
            label.text = marker == VillageChief.QuestMarker.TurnIn ? data.turnInMarker : data.newQuestMarker;
            label.color = marker == VillageChief.QuestMarker.TurnIn ? data.turnInMarkerColor : data.newQuestMarkerColor;
        }

        void HandleStationOpened(Interactable target) => stationOpen = true;

        void HandleStationClosed(Interactable target) => stationOpen = false;

        void LateUpdate()
        {
            bool show = marker != VillageChief.QuestMarker.None && anchor != null && viewCamera != null && !stationOpen;
            Vector3 screen = Vector3.zero;
            if (show)
            {
                screen = viewCamera.WorldToScreenPoint(anchor.position);
                show = screen.z > 0f;
            }
            label.enabled = show;
            if (!show) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(area, screen, null, out var local);
            float bob = data != null ? Mathf.Sin(Time.time * data.markerBobRate * Mathf.PI * 2f) * data.markerBob : 0f;
            label.rectTransform.anchoredPosition = local + new Vector2(0f, bob);
        }
    }
}
