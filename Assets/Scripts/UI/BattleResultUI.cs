using BossFight.Battle;
using BossFight.Cutscene;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BossFight.UI
{
    /// <summary>
    /// 勝利與失敗畫面。只訂閱 BattleManager.OnBattleResult：暗幕淡入，顯示標題、副標、戰鬥資訊與按鈕。
    /// 勝利：通關時間、第幾次挑戰，按鈕「再挑戰」；失敗：Boss 剩餘血量、戰鬥時間、第幾次挑戰，按鈕「重試」。
    /// 按鈕呼叫 BattleManager.Retry（重新載入場景，之後開場可以長按跳過）；出現時把按鈕設為選取，Enter／手把 A 也能按。
    /// Boss 名稱來自開場字卡（CutsceneDirector.OnTitleCard），沒有時用 BattleUIData.fallbackBossName。按鈕下面的提示跟著目前裝置（InputHintDevice）換鍵鼠或手把的字。
    /// </summary>
    [DisallowMultipleComponent]
    public class BattleResultUI : MonoBehaviour
    {
        [SerializeField] BattleUIData data;
        [SerializeField] BattleManager battle;
        [Tooltip("開場演出：字卡上的 Boss 名稱；可留空")]
        [SerializeField] CutsceneDirector intro;
        [SerializeField] CanvasGroup group;
        [SerializeField] Text titleText;
        [SerializeField] Text subtitleText;
        [SerializeField] Text statsText;
        [SerializeField] Button retryButton;
        [SerializeField] Text retryLabel;
        [SerializeField] Text hintText;
        [Tooltip("出現時把按鈕設為選取（Enter／手把 A 可以按）")]
        [SerializeField] EventSystem eventSystem;
        [Tooltip("目前用鍵鼠還是手把（按鈕下面的提示）；留空時一律鍵鼠")]
        [SerializeField] InputHintDevice hintDevice;

        string bossName;
        float shownAt = -1f;

        void Awake()
        {
            bossName = data.fallbackBossName;
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
            hintText.text = data.buttonHintKeyboard;
        }

        void OnEnable()
        {
            if (battle != null) battle.OnBattleResult += Show;
            if (intro != null) intro.OnTitleCard += HandleTitleCard;
            retryButton.onClick.AddListener(Retry);
            if (hintDevice != null) hintDevice.OnDeviceChanged += HandleDeviceChanged;
        }

        void OnDisable()
        {
            if (battle != null) battle.OnBattleResult -= Show;
            if (intro != null) intro.OnTitleCard -= HandleTitleCard;
            retryButton.onClick.RemoveListener(Retry);
            if (hintDevice != null) hintDevice.OnDeviceChanged -= HandleDeviceChanged;
        }

        void HandleDeviceChanged(bool gamepad) => hintText.text = gamepad ? data.buttonHintGamepad : data.buttonHintKeyboard;

        void HandleTitleCard(TitleCardInfo info)
        {
            if (!string.IsNullOrEmpty(info.subtitle)) bossName = info.subtitle;
        }

        void Show(BattleResult result)
        {
            if (result.victory)
            {
                titleText.text = data.victoryTitle;
                titleText.color = data.victoryColor;
                subtitleText.text = string.Format(data.victorySubtitle, bossName);
                statsText.text = $"通關時間　{FormatTime(result.battleTime)}\n第 {result.attempt} 次挑戰";
                retryLabel.text = data.victoryButton;
            }
            else
            {
                titleText.text = data.defeatTitle;
                titleText.color = data.defeatColor;
                subtitleText.text = data.defeatSubtitle;
                statsText.text = $"{bossName} 剩餘血量　{Mathf.CeilToInt(result.bossHealthRatio * 100f)}%\n" +
                                 $"戰鬥時間　{FormatTime(result.battleTime)}\n第 {result.attempt} 次挑戰";
                retryLabel.text = data.defeatButton;
            }
            shownAt = Time.unscaledTime;
            group.interactable = true;
            group.blocksRaycasts = true;
            if (eventSystem != null) eventSystem.SetSelectedGameObject(retryButton.gameObject);
        }

        static string FormatTime(float seconds)
        {
            int total = Mathf.FloorToInt(seconds);
            return $"{total / 60}:{total % 60:00}";
        }

        void Retry()
        {
            if (battle != null) battle.Retry();
        }

        void Update()
        {
            if (shownAt < 0f) return;
            group.alpha = Mathf.Clamp01((Time.unscaledTime - shownAt) / data.resultFadeTime);
        }
    }
}
