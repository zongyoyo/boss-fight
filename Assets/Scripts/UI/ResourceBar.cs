using UnityEngine;
using UnityEngine.UI;

namespace BossFight.UI
{
    /// <summary>
    /// 一條橫向的量條（血量、耐力、失衡值）：主條追到目前的比例；扣的時候後面留一段淺色的「延遲扣血」，停 trailDelay 秒再縮過去，看得出這一下扣了多少。
    /// 長度用 RectTransform 的 anchorMax.x 控制（不需要 Filled 的 sprite）。不訂閱事件，由擁有它的 UI 元件呼叫 Set。時間用真實時間（慢動作、畫面停頓時照跑）。
    /// </summary>
    [DisallowMultipleComponent]
    public class ResourceBar : MonoBehaviour
    {
        [SerializeField] BattleUIData data;
        [Tooltip("主條（父物件內從左邊長出來）")]
        [SerializeField] RectTransform fill;
        [Tooltip("延遲扣血；留空時沒有")]
        [SerializeField] RectTransform trail;
        [Tooltip("扣的時候留延遲扣血；耐力這種一直變動的條關掉")]
        [SerializeField] bool useTrail = true;

        float target = 1f, shown = 1f, trailValue = 1f, trailHoldUntil;
        bool initialized;

        /// <summary>主條的 Image（擁有者用來換顏色、閃爍）；沒有時為 null。</summary>
        public Image FillImage { get; private set; }

        void Awake()
        {
            FillImage = fill != null ? fill.GetComponent<Image>() : null;
            if (trail == null) return;
            trail.gameObject.SetActive(useTrail);
            var trailImage = trail.GetComponent<Image>();
            if (trailImage != null) trailImage.color = data.trailColor;
        }

        /// <summary>設定比例 0..1。第一次（或 instant）直接跳過去。</summary>
        public void Set(float ratio, bool instant = false)
        {
            ratio = Mathf.Clamp01(ratio);
            if (!initialized || instant)
            {
                initialized = true;
                target = shown = trailValue = ratio;
                Apply();
                return;
            }
            if (ratio < target && useTrail)
            {
                trailValue = Mathf.Max(trailValue, shown);
                trailHoldUntil = Time.unscaledTime + data.trailDelay;
            }
            target = ratio;
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            shown = Mathf.MoveTowards(shown, target, data.fillSpeed * dt);
            if (trailValue < shown || !useTrail) trailValue = shown; // 回血：延遲條直接跟上
            else if (Time.unscaledTime >= trailHoldUntil) trailValue = Mathf.MoveTowards(trailValue, shown, data.trailSpeed * dt);
            Apply();
        }

        void Apply()
        {
            if (fill != null) fill.anchorMax = new Vector2(shown, fill.anchorMax.y);
            if (trail != null) trail.anchorMax = new Vector2(trailValue, trail.anchorMax.y);
        }
    }
}
