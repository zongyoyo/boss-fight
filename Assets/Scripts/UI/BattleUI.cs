using BossFight.Battle;
using BossFight.Cutscene;
using UnityEngine;

namespace BossFight.UI
{
    /// <summary>
    /// 戰鬥 UI 的總控：決定 HUD（玩家血條、耐力、骨頭、鎖定標記）與 Boss 血條什麼時候出現。數值由各自的元件更新
    /// （PlayerHudUI、BossHudUI、LockOnMarkerUI），勝敗畫面是 BattleResultUI，開場字卡是 TitleCardUI（IntroUI）。
    /// 只訂閱 BattleManager.OnBattleStateChanged／OnBattleResult 與開場的 CutsceneDirector.OnTitleCard：
    /// 開場中隱藏；字卡出現時（開場第 7 鏡）Boss 血條從下方滑入、HUD 淡入（沒有字卡就在第一階段開打時）；
    /// 轉階段演出期間淡出，第二階段開始再出現；勝敗畫面出現時淡出。時間讀 BattleUIData，用真實時間。
    /// </summary>
    [DisallowMultipleComponent]
    public class BattleUI : MonoBehaviour
    {
        [SerializeField] BattleUIData data;
        [SerializeField] BattleManager battle;
        [Tooltip("開場演出：字卡出現時 HUD 才出現；留空時第一階段開打就出現")]
        [SerializeField] CutsceneDirector intro;
        [Tooltip("玩家 HUD 與鎖定標記")]
        [SerializeField] CanvasGroup hudGroup;
        [Tooltip("Boss 名稱、血條、失衡條")]
        [SerializeField] CanvasGroup bossGroup;
        [Tooltip("會滑入的 Boss 面板（bossGroup 本身或它底下）")]
        [SerializeField] RectTransform bossPanel;

        bool visible;
        float slideStart;
        Vector2 bossRest;

        void Awake()
        {
            bossRest = bossPanel.anchoredPosition;
            hudGroup.alpha = bossGroup.alpha = 0f;
            hudGroup.blocksRaycasts = bossGroup.blocksRaycasts = false;
        }

        void OnEnable()
        {
            if (battle != null)
            {
                battle.OnBattleStateChanged += HandleStateChanged;
                battle.OnBattleResult += HandleResult;
            }
            if (intro != null) intro.OnTitleCard += HandleTitleCard;
        }

        void OnDisable()
        {
            if (battle != null)
            {
                battle.OnBattleStateChanged -= HandleStateChanged;
                battle.OnBattleResult -= HandleResult;
            }
            if (intro != null) intro.OnTitleCard -= HandleTitleCard;
        }

        void HandleTitleCard(TitleCardInfo info) => SetVisible(true);

        void HandleStateChanged(BattleState state)
        {
            switch (state)
            {
                case BattleState.None:
                case BattleState.Intro:
                case BattleState.Transition:
                    SetVisible(false);
                    break;
                case BattleState.Phase1:
                case BattleState.Phase2:
                case BattleState.Ultimate:
                case BattleState.Berserk:
                    SetVisible(true);
                    break;
                // Victory／Defeat：留著看 Boss 血條歸零（或自己的血條歸零），勝敗畫面出現時才收
            }
        }

        void HandleResult(BattleResult result) => SetVisible(false);

        void SetVisible(bool on)
        {
            if (on == visible) return;
            visible = on;
            if (on) slideStart = Time.unscaledTime;
        }

        void Update()
        {
            float step = Time.unscaledDeltaTime / data.hudFadeTime;
            float target = visible ? 1f : 0f;
            hudGroup.alpha = Mathf.MoveTowards(hudGroup.alpha, target, step);
            bossGroup.alpha = Mathf.MoveTowards(bossGroup.alpha, target, step);

            float t = visible ? Mathf.Clamp01((Time.unscaledTime - slideStart) / data.bossBarSlideTime) : 1f;
            float ease = 1f - (1f - t) * (1f - t) * (1f - t);
            bossPanel.anchoredPosition = bossRest + Vector2.down * (data.bossBarSlideDistance * (1f - ease));
        }
    }
}
