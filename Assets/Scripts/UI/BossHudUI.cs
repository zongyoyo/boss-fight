using BossFight.Boss;
using BossFight.Cutscene;
using UnityEngine;
using UnityEngine.UI;

namespace BossFight.UI
{
    /// <summary>
    /// 下方置中的 Boss 名稱、血條（BattleUIData.bossHealthTicks 標出 50% 與 20% 刻度）與失衡條。
    /// 只訂閱 BossHealth.OnBossHealthChanged、BossStagger.OnStaggerChanged／OnStunned／OnStunEnded，名稱來自開場字卡（CutsceneDirector.OnTitleCard）。
    /// 頭暈中失衡條改成倒數：從滿條往中間收縮（ResourceBar 置中模式），收到中間時頭暈剛好結束（遊戲時間，跟 Boss 的頭暈計時一樣會被暫停、畫面停頓停住；
    /// 處決中延長的部分停在 0），顏色固定 stunnedColor。條上顯示「頭暈中！靠近按 F 處決」：F 是 Player/Execute 目前裝置（InputHintDevice）的按鍵，換裝置、改鍵後跟著變。
    /// 出現、滑入由 BattleUI 控制。
    /// </summary>
    [DisallowMultipleComponent]
    public class BossHudUI : MonoBehaviour
    {
        [SerializeField] BattleUIData data;
        [Tooltip("頭暈提示裡處決鍵的顯示名稱")]
        [SerializeField] InputNameData inputNames;
        [Tooltip("目前用鍵鼠還是手把（處決提示顯示哪一種鍵）；留空時一律鍵鼠")]
        [SerializeField] InputHintDevice hintDevice;
        [SerializeField] BossHealth health;
        [SerializeField] BossStagger stagger;
        [Tooltip("開場演出：字卡上的 Boss 名稱用在血條上；留空時用 BattleUIData.fallbackBossName")]
        [SerializeField] CutsceneDirector intro;
        [SerializeField] Text nameText;
        [SerializeField] ResourceBar healthBar;
        [Tooltip("一條刻度線；依 BattleUIData.bossHealthTicks 複製，放在血條上")]
        [SerializeField] RectTransform tickTemplate;
        [SerializeField] ResourceBar staggerBar;
        [Tooltip("頭暈中顯示在失衡條上的文字")]
        [SerializeField] Text stunnedText;
        /// <summary>由事件訂閱者套用階段名稱與血條色，不讀取戰鬥狀態。</summary>
        public void SetPresentation(string title, Color color)
        { nameText.text = title; if (healthBar.FillImage != null) healthBar.FillImage.color = color; }
        public void FlashSelfCost(float before, float after, float max, float duration)
        { if (max > 0f) healthBar.FlashLoss(before / max, after / max, duration); }

        bool stunned, gamepad;
        float stunDuration, stunElapsed;

        void Awake()
        {
            nameText.text = data.fallbackBossName;
            stunnedText.enabled = false;
            BuildTicks();
        }

        void Start()
        {
            if (healthBar.FillImage != null) healthBar.FillImage.color = data.bossHealthColor;
            if (staggerBar.FillImage != null && !stunned) staggerBar.FillImage.color = data.staggerColor;
        }

        void OnEnable()
        {
            if (health != null) health.OnBossHealthChanged += HandleHealthChanged;
            if (stagger != null)
            {
                stagger.OnStaggerChanged += HandleStaggerChanged;
                stagger.OnStunned += HandleStunned;
                stagger.OnStunEnded += HandleStunEnded;
            }
            if (intro != null) intro.OnTitleCard += HandleTitleCard;
            if (hintDevice != null) hintDevice.OnDeviceChanged += HandleDeviceChanged;
        }

        void OnDisable()
        {
            if (health != null) health.OnBossHealthChanged -= HandleHealthChanged;
            if (stagger != null)
            {
                stagger.OnStaggerChanged -= HandleStaggerChanged;
                stagger.OnStunned -= HandleStunned;
                stagger.OnStunEnded -= HandleStunEnded;
            }
            if (intro != null) intro.OnTitleCard -= HandleTitleCard;
            if (hintDevice != null) hintDevice.OnDeviceChanged -= HandleDeviceChanged;
        }

        void BuildTicks()
        {
            if (tickTemplate == null) return;
            tickTemplate.gameObject.SetActive(false);
            foreach (float ratio in data.bossHealthTicks)
            {
                var tick = Instantiate(tickTemplate, tickTemplate.parent);
                tick.name = $"Tick{Mathf.RoundToInt(ratio * 100f)}";
                tick.anchorMin = new Vector2(ratio, tickTemplate.anchorMin.y);
                tick.anchorMax = new Vector2(ratio, tickTemplate.anchorMax.y);
                tick.anchoredPosition = new Vector2(0f, tickTemplate.anchoredPosition.y);
                tick.gameObject.SetActive(true);
            }
        }

        void HandleTitleCard(TitleCardInfo info)
        {
            if (!string.IsNullOrEmpty(info.subtitle)) nameText.text = info.subtitle;
        }

        void HandleHealthChanged(float current, float max) => healthBar.Set(max > 0f ? current / max : 0f);

        // 頭暈中由倒數接管失衡條
        void HandleStaggerChanged(float current, float max)
        {
            if (!stunned) staggerBar.Set(max > 0f ? current / max : 0f);
        }

        void HandleStunned(float duration)
        {
            stunned = true;
            stunDuration = duration;
            stunElapsed = 0f;
            staggerBar.SetCentered(true, 1f);
            if (staggerBar.FillImage != null) staggerBar.FillImage.color = data.stunnedColor;
            RefreshStunnedText();
            stunnedText.enabled = true;
        }

        void HandleStunEnded(StunEndReason reason)
        {
            stunned = false;
            stunnedText.enabled = false;
            staggerBar.SetCentered(false, 0f);
            if (staggerBar.FillImage != null) staggerBar.FillImage.color = data.staggerColor;
        }

        void HandleDeviceChanged(bool usingGamepad)
        {
            gamepad = usingGamepad;
            if (stunned) RefreshStunnedText();
        }

        void RefreshStunnedText() =>
            stunnedText.text = string.Format(data.stunnedLabel, BindingText.FirstDisplay("Player/Execute", BindingText.Group(gamepad), inputNames));

        void Update()
        {
            if (!stunned) return;
            stunElapsed += Time.deltaTime;
            staggerBar.Set(stunDuration > 0f ? 1f - stunElapsed / stunDuration : 0f, true);
        }
    }
}
