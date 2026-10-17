using BossFight.Cutscene;
using UnityEngine;
using UnityEngine.UI;

namespace BossFight.UI
{
    /// <summary>
    /// 開場第 7 鏡的字卡：「BOSS FIGHT」加 Boss 名稱。只訂閱 CutsceneDirector.OnTitleCard：放大淡入、停 duration 秒、淡出。
    /// T9 的 Boss 血條滑入可以訂閱同一個事件。這裡的數字全是視覺參數。
    /// </summary>
    [DisallowMultipleComponent]
    public class TitleCardUI : MonoBehaviour
    {
        [SerializeField] CutsceneDirector source;
        [SerializeField] CanvasGroup group;
        [Tooltip("會縮放的那一塊（標題與名稱的父物件）")]
        [SerializeField] RectTransform panel;
        [SerializeField] Text titleText;
        [SerializeField] Text subtitleText;

        [Header("動畫（秒）")]
        [Min(0.01f)] [SerializeField] float fadeIn = 0.18f;
        [Min(0.01f)] [SerializeField] float fadeOut = 0.4f;
        [Tooltip("出現時從幾倍大縮回 1")]
        [Min(1f)] [SerializeField] float startScale = 1.6f;

        float timer, hold;
        bool showing;

        void Awake() => group.alpha = 0f;

        void OnEnable()
        {
            if (source != null) source.OnTitleCard += Show;
        }

        void OnDisable()
        {
            if (source != null) source.OnTitleCard -= Show;
        }

        void Show(TitleCardInfo info)
        {
            titleText.text = info.title;
            subtitleText.text = info.subtitle;
            hold = Mathf.Max(fadeIn, info.duration);
            timer = 0f;
            showing = true;
        }

        void Update()
        {
            if (!showing) return;
            timer += Time.unscaledDeltaTime;

            float alpha;
            if (timer < fadeIn) alpha = timer / fadeIn;
            else if (timer < hold) alpha = 1f;
            else alpha = 1f - (timer - hold) / fadeOut;
            group.alpha = Mathf.Clamp01(alpha);

            float s = Mathf.Clamp01(timer / fadeIn);
            panel.localScale = Vector3.one * Mathf.Lerp(startScale, 1f, 1f - (1f - s) * (1f - s));

            if (timer >= hold + fadeOut)
            {
                showing = false;
                group.alpha = 0f;
            }
        }
    }
}
