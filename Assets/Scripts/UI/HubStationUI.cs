using BossFight.Hub;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace BossFight.UI
{
    /// <summary>
    /// 休息區的站點面板（T20）：HubManager.OnStationOpened 時出現、寫上站點名稱（HubUIData）。村長、鐵匠鋪先是空面板（內容在 T22–T24），
    /// 營火（T21）有自己的 CampfireUI，這裡不顯示；
    /// 傳送門是挑戰清單（目前只有 OIIA 貓一列）→ 確認 → Portal.Depart 換場景（黑幕淡出期間面板維持開著、不再收輸入）。
    /// 返回讀 UI/Cancel（Esc、手把 B）：確認框開著時回到清單，否則呼叫 HubManager.CloseStation。面板下方的返回提示依 InputHintDevice 換鍵鼠或手把的字。
    /// 由 HubUIBuilder 建立與接線。
    /// </summary>
    [DisallowMultipleComponent]
    public class HubStationUI : MonoBehaviour
    {
        [SerializeField] HubUIData data;
        [SerializeField] HubManager hub;
        [SerializeField] EventSystem eventSystem;
        [Tooltip("目前用鍵鼠還是手把（返回提示）；留空時一律鍵鼠")]
        [SerializeField] InputHintDevice hintDevice;
        [Tooltip("整個面板")]
        [SerializeField] CanvasGroup group;
        [SerializeField] Text titleText;
        [Tooltip("還沒有內容的站點中央的字")]
        [SerializeField] Text emptyText;
        [SerializeField] Text closeHint;

        [Header("傳送門")]
        [SerializeField] GameObject portalList;
        [SerializeField] Button bossButton;
        [SerializeField] GameObject confirmBox;
        [SerializeField] Text confirmText;
        [SerializeField] Button confirmYesButton;
        [SerializeField] Button confirmNoButton;

        InputAction cancelAction;
        Interactable current;
        bool confirming, departing;

        void Awake()
        {
            var actions = InputSystem.actions;
            if (actions != null) cancelAction = actions.FindAction("UI/Cancel", false);
            bossButton.onClick.AddListener(ShowConfirm);
            confirmYesButton.onClick.AddListener(Depart);
            confirmNoButton.onClick.AddListener(HideConfirm);
            SetVisible(false);
        }

        void OnEnable()
        {
            cancelAction?.Enable();
            if (hub != null)
            {
                hub.OnStationOpened += HandleStationOpened;
                hub.OnStationClosed += HandleStationClosed;
            }
            if (hintDevice != null) hintDevice.OnDeviceChanged += HandleDeviceChanged;
        }

        void OnDisable()
        {
            if (hub != null)
            {
                hub.OnStationOpened -= HandleStationOpened;
                hub.OnStationClosed -= HandleStationClosed;
            }
            if (hintDevice != null) hintDevice.OnDeviceChanged -= HandleDeviceChanged;
        }

        void Start()
        {
            if (hintDevice == null) HandleDeviceChanged(false);
        }

        void Update()
        {
            if (current == null || departing) return;
            if (cancelAction != null && cancelAction.WasPressedThisFrame())
            {
                if (confirming) HideConfirm();
                else if (hub != null) hub.CloseStation();
                return;
            }
            // 滑鼠點到空白處會取消選取：選回預設的按鈕，鍵盤、手把才導覽得下去
            if (eventSystem != null && eventSystem.currentSelectedGameObject == null) Select(DefaultSelectable());
        }

        void HandleStationOpened(Interactable target)
        {
            if (target is Campfire) return; // CampfireUI 負責
            current = target;
            confirming = departing = false;
            var text = data != null ? data.Get(target.Kind) : null;
            titleText.text = text != null ? text.title : string.Empty;
            bool portal = target is Portal;
            emptyText.text = data != null ? data.emptyStationText : string.Empty;
            emptyText.gameObject.SetActive(!portal);
            portalList.SetActive(portal);
            if (portal) bossButton.GetComponentInChildren<Text>().text = data.bossLabel;
            confirmBox.SetActive(false);
            SetVisible(true);
            Select(DefaultSelectable());
        }

        void HandleStationClosed(Interactable target)
        {
            current = null;
            confirming = false;
            SetVisible(false);
            if (eventSystem != null) eventSystem.SetSelectedGameObject(null);
        }

        void HandleDeviceChanged(bool gamepad)
        {
            if (closeHint != null && data != null) closeHint.text = gamepad ? data.closeHintGamepad : data.closeHintKeyboard;
        }

        void ShowConfirm()
        {
            confirming = true;
            confirmText.text = string.Format(data.departConfirmFormat, data.bossLabel);
            confirmYesButton.GetComponentInChildren<Text>().text = data.departYes;
            confirmNoButton.GetComponentInChildren<Text>().text = data.departNo;
            portalList.SetActive(false);
            confirmBox.SetActive(true);
            Select(confirmNoButton);
        }

        void HideConfirm()
        {
            confirming = false;
            confirmBox.SetActive(false);
            portalList.SetActive(true);
            Select(bossButton);
        }

        void Depart()
        {
            if (!(current is Portal portal)) return;
            departing = true;
            group.interactable = false;
            portal.Depart();
        }

        Selectable DefaultSelectable()
        {
            if (!(current is Portal)) return null;
            return confirming ? confirmNoButton : bossButton;
        }

        void Select(Selectable selectable)
        {
            if (eventSystem != null && selectable != null) eventSystem.SetSelectedGameObject(selectable.gameObject);
        }

        void SetVisible(bool visible)
        {
            group.alpha = visible ? 1f : 0f;
            group.interactable = visible;
            group.blocksRaycasts = visible;
        }
    }
}
