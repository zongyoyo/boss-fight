using BossFight.Player;
using UnityEngine;
using UnityEngine.UI;

namespace BossFight.UI
{
    /// <summary>
    /// 左下角的玩家 HUD：血條、耐力條、骨頭剩餘次數與累積進度。只訂閱 PlayerHealth.OnHealthChanged／OnBonesChanged／OnBoneProgressChanged／OnGuardBreak
    /// 與 PlayerStamina.OnStaminaChanged／OnRegenBoostChanged。
    /// 耐力歸零（翻滾、重擊把耐力用光，或舉盾被打到破防）時耐力條閃一下；舉盾加速回復時換成 staminaBoostColor。
    /// 骨頭依上限複製 boneTemplate，沒有的變暗；下方的細條是下一根骨頭的累積進度。
    /// 玩家由 PlayerSpawner 生成後交給 BindPlayer（T18）。
    /// 骨頭左邊的按鍵提示是 Player/Heal 目前裝置（InputHintDevice）的按鍵，換裝置、改鍵後跟著變。
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerHudUI : MonoBehaviour, IPlayerBinding
    {
        [SerializeField] BattleUIData data;
        [SerializeField] ResourceBar healthBar;
        [SerializeField] ResourceBar staminaBar;
        [Tooltip("一根骨頭的圖示（底下的 Image 都會跟著變色）；依每場次數複製，排在同一個父物件裡（父物件只放骨頭，其他子物件會被清掉）")]
        [SerializeField] RectTransform boneTemplate;
        [Tooltip("骨頭與骨頭的間距（Canvas 單位）")]
        [SerializeField] float boneSpacing = 56f;
        [Tooltip("血量數字，例如 72 / 100；留空時不顯示")]
        [SerializeField] Text healthText;
        [Tooltip("下一根骨頭的累積進度；留空時不顯示")]
        [SerializeField] ResourceBar boneProgressBar;
        [Tooltip("骨頭左邊的啃骨頭鍵（Player/Heal）；留空時不改字")]
        [SerializeField] Text healKeyText;
        [Tooltip("啃骨頭鍵的顯示名稱")]
        [SerializeField] InputNameData inputNames;
        [Tooltip("目前用鍵鼠還是手把；留空時一律鍵鼠")]
        [SerializeField] InputHintDevice hintDevice;

        [Header("骨頭閃爍（純表現）")]
        [Tooltip("FlashBones 閃多久（秒）")]
        [SerializeField] float boneFlashTime = 1.5f;
        [Tooltip("放大的比例（0.4 = 最大 1.4 倍）")]
        [SerializeField] float boneFlashScale = 0.4f;
        [Tooltip("閃幾下")]
        [SerializeField] int boneFlashPulses = 3;

        Image[][] bones = new Image[0][];
        RectTransform[] boneRects = new RectTransform[0];
        float staminaFlashUntil, boneFlashUntil;
        bool staminaWasEmpty, staminaFull, regenBoosted;
        PlayerHealth health;   // PlayerSpawner 交給 BindPlayer
        PlayerStamina stamina;
        bool subscribed;

        /// <summary>PlayerSpawner 生成（或換）角色時呼叫；已經訂閱就先退訂舊角色（新角色的初始值在它的 Start 送出）。</summary>
        public void BindPlayer(PlayerRig rig)
        {
            if (subscribed) UnsubscribePlayer();
            health = rig != null ? rig.Health : null;
            stamina = rig != null ? rig.Stamina : null;
            if (subscribed) SubscribePlayer();
        }

        void OnEnable()
        {
            SubscribePlayer();
            if (hintDevice != null) hintDevice.OnDeviceChanged += HandleDeviceChanged;
            subscribed = true;
        }

        void OnDisable()
        {
            UnsubscribePlayer();
            if (hintDevice != null) hintDevice.OnDeviceChanged -= HandleDeviceChanged;
            subscribed = false;
        }

        void SubscribePlayer()
        {
            if (health != null)
            {
                health.OnHealthChanged += HandleHealthChanged;
                health.OnBonesChanged += HandleBonesChanged;
                health.OnBoneProgressChanged += HandleBoneProgressChanged;
                health.OnGuardBreak += FlashStamina;
            }
            if (stamina != null)
            {
                stamina.OnStaminaChanged += HandleStaminaChanged;
                stamina.OnRegenBoostChanged += HandleRegenBoostChanged;
            }
        }

        void UnsubscribePlayer()
        {
            if (health != null)
            {
                health.OnHealthChanged -= HandleHealthChanged;
                health.OnBonesChanged -= HandleBonesChanged;
                health.OnBoneProgressChanged -= HandleBoneProgressChanged;
                health.OnGuardBreak -= FlashStamina;
            }
            if (stamina != null)
            {
                stamina.OnStaminaChanged -= HandleStaminaChanged;
                stamina.OnRegenBoostChanged -= HandleRegenBoostChanged;
            }
        }

        void Start()
        {
            if (healthBar.FillImage != null) healthBar.FillImage.color = data.playerHealthColor;
            if (boneProgressBar != null && boneProgressBar.FillImage != null) boneProgressBar.FillImage.color = data.boneProgressColor;
            if (hintDevice == null) HandleDeviceChanged(false);
        }

        void HandleDeviceChanged(bool gamepad)
        {
            if (healKeyText != null) healKeyText.text = BindingText.FirstDisplay("Player/Heal", BindingText.Group(gamepad), inputNames);
        }

        void HandleHealthChanged(float current, float max)
        {
            healthBar.Set(max > 0f ? current / max : 0f);
            if (healthText != null) healthText.text = $"{Mathf.CeilToInt(current)} / {Mathf.RoundToInt(max)}";
        }

        void HandleStaminaChanged(float current, float max)
        {
            staminaBar.Set(max > 0f ? current / max : 0f);
            bool empty = current <= 0f;
            if (empty && !staminaWasEmpty) FlashStamina();
            staminaWasEmpty = empty;
            staminaFull = current >= max;
        }

        void HandleRegenBoostChanged(bool boosted) => regenBoosted = boosted;

        void HandleBoneProgressChanged(float progress)
        {
            if (boneProgressBar != null) boneProgressBar.Set(progress);
        }

        void FlashStamina() => staminaFlashUntil = Time.unscaledTime + data.emptyFlashTime;

        void HandleBonesChanged(int left, int max)
        {
            if (bones.Length != max) BuildBones(max);
            for (int i = 0; i < bones.Length; i++)
            {
                var color = i < left ? data.boneColor : data.boneUsedColor;
                foreach (var image in bones[i]) image.color = color;
            }
        }

        void BuildBones(int count)
        {
            if (boneTemplate == null) return;
            var parent = boneTemplate.parent;
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                var child = parent.GetChild(i);
                if (child != boneTemplate) Destroy(child.gameObject);
            }
            boneTemplate.gameObject.SetActive(false);
            bones = new Image[count][];
            boneRects = new RectTransform[count];
            for (int i = 0; i < count; i++)
            {
                var bone = Instantiate(boneTemplate, parent);
                bone.name = $"Bone{i}";
                bone.gameObject.SetActive(true);
                bone.anchoredPosition = boneTemplate.anchoredPosition + Vector2.right * (boneSpacing * i);
                bones[i] = bone.GetComponentsInChildren<Image>(true);
                boneRects[i] = bone;
            }
        }

        /// <summary>骨頭圖示放大閃幾下（序章教學拿到第一根骨頭時，TutorialHudUI 呼叫）。</summary>
        public void FlashBones() => boneFlashUntil = Time.unscaledTime + boneFlashTime;

        void Update()
        {
            if (boneFlashUntil > 0f)
            {
                float left = boneFlashUntil - Time.unscaledTime;
                float scale = left > 0f ? 1f + boneFlashScale * Mathf.Abs(Mathf.Sin(left * Mathf.PI * boneFlashPulses / boneFlashTime)) : 1f;
                foreach (var rect in boneRects)
                    if (rect != null) rect.localScale = Vector3.one * scale;
                if (left <= 0f) boneFlashUntil = 0f;
            }
            var image = staminaBar.FillImage;
            if (image == null) return;
            bool flashing = Time.unscaledTime < staminaFlashUntil &&
                            Mathf.Repeat(Time.unscaledTime * data.flashRate, 1f) < 0.5f;
            // 加速回復只在還沒滿的時候顯示，滿了舉著盾也是一般顏色
            image.color = flashing ? data.staminaEmptyColor : regenBoosted && !staminaFull ? data.staminaBoostColor : data.staminaColor;
        }
    }
}
