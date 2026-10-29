using BossFight.Boss;
using UnityEngine;
using UnityEngine.UI;

namespace BossFight.UI
{
    /// <summary>教學樹精頭頂的血量與軀幹條；數值與顯示條件只收事件。</summary>
    public class TrainingSaplingHudUI : MonoBehaviour
    {
        [SerializeField] TrainingSapling sapling;
        [SerializeField] Camera viewCamera;
        [SerializeField] Transform anchor;
        [SerializeField] RectTransform panel;
        [SerializeField] ResourceBar health;
        [SerializeField] ResourceBar stagger;
        [SerializeField] Text title;
        [Header("頭暈時軀幹條閃光（純表現）")]
        [SerializeField] Color stunFlashColor = Color.white;
        [Tooltip("閃光淡回原色的時間（秒）")]
        [SerializeField] float stunFlashTime = 0.6f;
        [Tooltip("閃光時量條放大的比例")]
        [SerializeField] float stunFlashScale = 0.35f;
        RectTransform area;
        bool shown;
        float flashLeft;
        Color staggerColor;

        void Awake()
        {
            area = panel.parent as RectTransform;
            panel.gameObject.SetActive(false);
        }
        void OnEnable() { sapling.OnViewChanged += HandleView; sapling.OnStunned += HandleStunned; }
        void OnDisable() { sapling.OnViewChanged -= HandleView; sapling.OnStunned -= HandleStunned; }
        // 巴掌讓軀幹條一口氣滿格：閃白、放大一下，讓玩家看到「滿了」
        void HandleStunned()
        {
            if (flashLeft <= 0f && stagger.FillImage != null) staggerColor = stagger.FillImage.color;
            flashLeft = stunFlashTime;
        }
        void Start() => sapling.PublishState();
        void HandleView(SaplingView view)
        {
            shown = view.visible && (view.mode == SaplingMode.Target || view.stunned);
            health.Set(view.maxHealth > 0f ? view.health / view.maxHealth : 0f);
            stagger.Set(view.maxStagger > 0f ? view.stagger / view.maxStagger : 0f);
            title.text = sapling.Data.displayName;
        }
        void LateUpdate()
        {
            bool visible = shown && viewCamera != null && anchor != null;
            Vector3 screen = visible ? viewCamera.WorldToScreenPoint(anchor.position) : Vector3.zero;
            visible &= screen.z > 0f;
            panel.gameObject.SetActive(visible);
            if (!visible) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(area, screen, null, out var point);
            panel.anchoredPosition = point;
            if (flashLeft > 0f && stunFlashTime > 0f)
            {
                flashLeft = Mathf.Max(0f, flashLeft - Time.unscaledDeltaTime);
                float k = flashLeft / stunFlashTime;
                if (stagger.FillImage != null) stagger.FillImage.color = Color.Lerp(staggerColor, stunFlashColor, k);
                stagger.transform.localScale = Vector3.one * (1f + stunFlashScale * k);
            }
        }
    }
}
