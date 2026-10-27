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
        RectTransform area;
        bool shown;

        void Awake()
        {
            area = panel.parent as RectTransform;
            panel.gameObject.SetActive(false);
        }
        void OnEnable() => sapling.OnViewChanged += HandleView;
        void OnDisable() => sapling.OnViewChanged -= HandleView;
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
        }
    }
}
