using BossFight.Boss;
using BossFight.Cutscene;
using UnityEngine;
using UnityEngine.UI;

namespace BossFight.UI
{
    /// <summary>
    /// 下方置中的 Boss 名稱、血條（BattleUIData.bossHealthTicks 標出 50% 與 20% 刻度）與失衡條。
    /// 只訂閱 BossHealth.OnBossHealthChanged、BossStagger.OnStaggerChanged／OnStunned／OnStunEnded，名稱來自開場字卡（CutsceneDirector.OnTitleCard）。
    /// 頭暈中失衡條閃爍，條上顯示「頭暈中！靠近按 F 處決」（F 是 Player/Execute 目前的鍵鼠按鍵，改鍵後跟著變）。出現、滑入由 BattleUI 控制。
    /// </summary>
    [DisallowMultipleComponent]
    public class BossHudUI : MonoBehaviour
    {
        [SerializeField] BattleUIData data;
        [Tooltip("頭暈提示裡處決鍵的顯示名稱")]
        [SerializeField] InputNameData inputNames;
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

        bool stunned;

        void Awake()
        {
            nameText.text = data.fallbackBossName;
            stunnedText.enabled = false;
            BuildTicks();
        }

        void Start()
        {
            if (healthBar.FillImage != null) healthBar.FillImage.color = data.bossHealthColor;
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

        void HandleStaggerChanged(float current, float max) => staggerBar.Set(max > 0f ? current / max : 0f);

        void HandleStunned(float duration)
        {
            stunned = true;
            stunnedText.text = string.Format(data.stunnedLabel, BindingText.FirstDisplay("Player/Execute", BindingText.KeyboardGroup, inputNames));
            stunnedText.enabled = true;
        }

        void HandleStunEnded(StunEndReason reason)
        {
            stunned = false;
            stunnedText.enabled = false;
        }

        void Update()
        {
            var image = staggerBar.FillImage;
            if (image == null) return;
            bool flash = stunned && Mathf.Repeat(Time.unscaledTime * data.flashRate, 1f) < 0.5f;
            image.color = flash ? data.stunnedColor : data.staggerColor;
        }
    }
}
