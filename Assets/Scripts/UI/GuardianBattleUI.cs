using BossFight.Battle;
using UnityEngine;
using UnityEngine.UI;

namespace BossFight.UI
{
    /// <summary>
    /// 大樹守衛戰的 Boss 面板與提示：只訂閱 GuardianPhaseController。開戰時 Boss 面板從下方滑入（時間、距離、緩動跟 BattleUI 一樣，讀 BattleUIData），
    /// 萬根齊發的事前提示與轉階段訊息顯示在 hint。玩家 HUD 沿用教學的；量條與頭暈倒數是 BossHudUI。
    /// </summary>
    public class GuardianBattleUI : MonoBehaviour
    {
        [SerializeField] BattleUIData data;
        [SerializeField] GuardianPhaseController guardian;
        [SerializeField] CanvasGroup bossGroup;
        [SerializeField] Text hint;

        RectTransform bar;
        Vector2 barHome;
        float slideStart = -1f; // 真實時間；< 0 = 沒有在滑

        void Awake()
        {
            bossGroup.alpha = 0f;
            bossGroup.blocksRaycasts = false;
            hint.text = string.Empty;
            bar = bossGroup.GetComponent<RectTransform>();
            if (bar != null) barHome = bar.anchoredPosition;
        }

        void OnEnable()
        {
            guardian.OnBattleStarted += HandleBattleStarted;
            guardian.OnHintChanged += HandleHint;
        }

        void OnDisable()
        {
            guardian.OnBattleStarted -= HandleBattleStarted;
            guardian.OnHintChanged -= HandleHint;
        }

        void HandleBattleStarted()
        {
            bossGroup.alpha = 1f;
            slideStart = bar != null && data != null ? Time.unscaledTime : -1f;
        }

        void Update()
        {
            if (slideStart < 0f) return;
            float t = Mathf.Clamp01((Time.unscaledTime - slideStart) / data.bossBarSlideTime);
            float ease = 1f - (1f - t) * (1f - t) * (1f - t);
            bar.anchoredPosition = barHome + Vector2.down * (data.bossBarSlideDistance * (1f - ease));
            if (t >= 1f) slideStart = -1f;
        }

        void HandleHint(string value) => hint.text = value;
    }
}
