using BossFight.Battle;
using BossFight.Core;
using BossFight.Cutscene;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BossFight.UI
{
    /// <summary>
    /// 勝利與失敗畫面。只訂閱 BattleManager.OnBattleResult：暗幕淡入，顯示勝利或戰敗那一組文字（各自一個父物件，只開一組）與共用的按鈕。
    /// 勝利：標題、副標、通關時間、第幾次挑戰，按鈕「再挑戰」。
    /// 戰敗：中央大字「菜」（defeatColorDelay 秒後花 defeatColorTime 秒從白色轉紅）、中字「就多練」、按鈕「重試」，
    /// 按鈕下面是一行小字的 Boss 剩餘血量／戰鬥時間／第幾次挑戰，再下面是灰色小字「選單裡有簡單模式」（簡單模式戰敗時不顯示，看 BattleResult.difficulty）。
    /// 戰敗的文字、顏色、字級、高度都讀 BattleUIData（Awake 時套用，改資產不用重建）。
    /// 按鈕呼叫 BattleManager.Retry（重新載入場景，之後開場可以長按跳過）；出現時把按鈕設為選取，Enter／手把 A 也能按。
    /// 右邊的「返回休息區」呼叫 BattleManager.ReturnToHub（不先確認，跟重試一樣直接走；勝敗共用）。
    /// Boss 名稱來自開場字卡（CutsceneDirector.OnTitleCard），沒有時用 BattleUIData.fallbackBossName。按鈕下面不放按鍵提示（勝敗都一樣）。
    /// </summary>
    [DisallowMultipleComponent]
    public class BattleResultUI : MonoBehaviour
    {
        [SerializeField] BattleUIData data;
        [SerializeField] BattleManager battle;
        [Tooltip("開場演出：字卡上的 Boss 名稱；可留空")]
        [SerializeField] CutsceneDirector intro;
        [SerializeField] CanvasGroup group;
        [Header("勝利")]
        [Tooltip("勝利那一組文字的父物件")]
        [SerializeField] GameObject victoryGroup;
        [SerializeField] Text titleText;
        [SerializeField] Text subtitleText;
        [SerializeField] Text statsText;
        [Header("戰敗")]
        [Tooltip("戰敗那一組文字的父物件")]
        [SerializeField] GameObject defeatGroup;
        [Tooltip("中央的大字")]
        [SerializeField] Text defeatTitleText;
        [Tooltip("大字下面的中字")]
        [SerializeField] Text defeatSubtitleText;
        [Tooltip("戰鬥資訊下面的灰色小字（簡單模式提示）")]
        [SerializeField] Text defeatHintText;
        [Tooltip("按鈕下面一行的 Boss 剩餘血量、戰鬥時間、挑戰次數")]
        [SerializeField] Text defeatStatsText;
        [Header("按鈕")]
        [SerializeField] Button retryButton;
        [SerializeField] Text retryLabel;
        [Tooltip("重試按鈕右邊的「返回休息區」")]
        [SerializeField] Button hubButton;
        [SerializeField] Text hubLabel;
        [Tooltip("出現時把按鈕設為選取（Enter／手把 A 可以按）")]
        [SerializeField] EventSystem eventSystem;

        string bossName;
        float shownAt = -1f;
        bool defeated;

        void Awake()
        {
            bossName = data.fallbackBossName;
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
            if (victoryGroup != null) victoryGroup.SetActive(false);
            if (defeatGroup != null) defeatGroup.SetActive(false);
            SetupText(defeatTitleText, data.defeatTitle, data.defeatTitleSize, data.defeatTitleStartColor, data.defeatTitleY);
            SetupText(defeatSubtitleText, data.defeatSubtitle, data.defeatSubtitleSize, data.defeatTextColor, data.defeatSubtitleY);
            SetupText(defeatHintText, data.defeatHint, data.defeatHintSize, data.defeatHintColor, data.defeatHintY);
            SetupText(defeatStatsText, string.Empty, data.defeatStatsSize, data.defeatTextColor, data.defeatStatsY);
        }

        static void SetupText(Text text, string value, int size, Color color, float y)
        {
            if (text == null) return;
            text.text = value;
            text.fontSize = size;
            text.color = color;
            var rt = text.rectTransform;
            rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, y);
        }

        void OnEnable()
        {
            if (battle != null) battle.OnBattleResult += Show;
            if (intro != null) intro.OnTitleCard += HandleTitleCard;
            retryButton.onClick.AddListener(Retry);
            if (hubButton != null) hubButton.onClick.AddListener(ReturnToHub);
        }

        void OnDisable()
        {
            if (battle != null) battle.OnBattleResult -= Show;
            if (intro != null) intro.OnTitleCard -= HandleTitleCard;
            retryButton.onClick.RemoveListener(Retry);
            if (hubButton != null) hubButton.onClick.RemoveListener(ReturnToHub);
        }

        void HandleTitleCard(TitleCardInfo info)
        {
            if (!string.IsNullOrEmpty(info.subtitle)) bossName = info.subtitle;
        }

        void Show(BattleResult result)
        {
            defeated = !result.victory;
            if (victoryGroup != null) victoryGroup.SetActive(!defeated);
            if (defeatGroup != null) defeatGroup.SetActive(defeated);
            if (!defeated)
            {
                titleText.text = data.victoryTitle;
                titleText.color = data.victoryColor;
                subtitleText.text = string.Format(data.victorySubtitle, bossName);
                statsText.text = $"通關時間　{FormatTime(result.battleTime)}\n第 {result.attempt} 次挑戰";
                retryLabel.text = data.victoryButton;
            }
            else
            {
                defeatTitleText.color = data.defeatTitleStartColor;
                // 已經是簡單模式就不再推薦簡單模式
                defeatHintText.gameObject.SetActive(result.difficulty != Difficulty.Easy);
                string gap = data.defeatStatsSeparator;
                defeatStatsText.text = $"{bossName} 剩餘血量 {Mathf.CeilToInt(result.bossHealthRatio * 100f)}%{gap}" +
                                       $"戰鬥時間 {FormatTime(result.battleTime)}{gap}第 {result.attempt} 次挑戰";
                retryLabel.text = data.defeatButton;
            }
            if (hubLabel != null) hubLabel.text = data.returnToHubButton;
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

        void ReturnToHub()
        {
            if (battle != null) battle.ReturnToHub();
        }

        void Update()
        {
            if (shownAt < 0f) return;
            float elapsed = Time.unscaledTime - shownAt;
            group.alpha = Mathf.Clamp01(elapsed / data.resultFadeTime);
            if (!defeated) return;
            // 剛出現是白色，停一下再慢慢轉紅
            float t = Mathf.SmoothStep(0f, 1f, (elapsed - data.defeatColorDelay) / data.defeatColorTime);
            defeatTitleText.color = Color.Lerp(data.defeatTitleStartColor, data.defeatColor, t);
        }
    }
}
