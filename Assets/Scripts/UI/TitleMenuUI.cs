using BossFight.Title;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BossFight.UI
{
    /// <summary>
    /// 標題畫面的介面（T32）：左上遊戲名、左下主選單（開始遊戲、設定、退出遊戲）。
    /// 只呼叫 TitleManager 的方法、訂閱它的事件；T33 起「開始遊戲」打開存檔欄位、「設定」打開 ESC 選單的設定頁。
    /// 滑鼠點到空白處取消選取時，選回目前這一頁的預設按鈕，鍵盤與手把才導覽得下去。
    /// </summary>
    [DisallowMultipleComponent]
    public class TitleMenuUI : MonoBehaviour
    {
        [SerializeField] TitleUIData data;
        [SerializeField] TitleManager title;
        [SerializeField] EventSystem eventSystem;

        [Header("遊戲名")]
        [SerializeField] Text titleText;

        [Header("主選單")]
        [SerializeField] GameObject mainPage;
        [SerializeField] Button startButton;
        [SerializeField] Button settingsButton;
        [SerializeField] Button quitButton;

        void Awake()
        {
            if (data != null)
            {
                if (titleText != null) titleText.text = data.gameTitle;
                SetLabel(startButton, data.startLabel);
                SetLabel(settingsButton, data.settingsLabel);
                SetLabel(quitButton, data.quitLabel);
            }
            quitButton.onClick.AddListener(() => title.Quit());
        }

        void OnEnable()
        {
            if (title != null) title.OnLeaving += HandleLeaving;
        }

        void OnDisable()
        {
            if (title != null) title.OnLeaving -= HandleLeaving;
        }

        void Start()
        {
            mainPage.SetActive(true);
            Select(startButton);
        }

        void Update()
        {
            if (title != null && title.IsLeaving) return;
            if (eventSystem != null && eventSystem.currentSelectedGameObject == null) Select(startButton);
        }

        void HandleLeaving()
        {
            // 換場景中不再接受點擊
            foreach (var button in mainPage.GetComponentsInChildren<Button>(true)) button.interactable = false;
        }

        void Select(Selectable target)
        {
            if (eventSystem != null && target != null) eventSystem.SetSelectedGameObject(target.gameObject);
        }

        static void SetLabel(Button button, string label)
        {
            if (button == null) return;
            var text = button.GetComponentInChildren<Text>(true);
            if (text != null) text.text = label;
        }
    }
}
