using BossFight.Core;
using BossFight.Player;
using UnityEngine;

namespace BossFight.UI
{
    /// <summary>
    /// 鎖定標記：鎖定時在目標的 LockOnTarget.AimPoint 上顯示一個小菱形，出現時從大縮回、之後輕輕呼吸。
    /// 鎖定哪個目標只訂閱 PlayerController.OnLockTargetChanged；位置每幀用 viewCamera 把 AimPoint 投到畫面上（目標在鏡頭後方時隱藏）。
    /// 處決標記（ExecutionMarkerUI.OnShownChanged）出現時先藏起來，兩個標記不疊在一起。
    /// </summary>
    [DisallowMultipleComponent]
    public class LockOnMarkerUI : MonoBehaviour
    {
        [SerializeField] BattleUIData data;
        [SerializeField] PlayerController player;
        [Tooltip("投影用的鏡頭（Main Camera）")]
        [SerializeField] Camera viewCamera;
        [Tooltip("標記（父物件是全螢幕的 RectTransform）")]
        [SerializeField] RectTransform marker;
        [Tooltip("處決標記；出現時藏起鎖定菱形（可留空）")]
        [SerializeField] ExecutionMarkerUI executionMarker;

        LockOnTarget target;
        RectTransform area;
        float shownTime;
        bool hiddenByExecution;

        void Awake()
        {
            area = marker.parent as RectTransform;
            marker.gameObject.SetActive(false);
        }

        void OnEnable()
        {
            if (player != null) player.OnLockTargetChanged += HandleLockTargetChanged;
            if (executionMarker != null) executionMarker.OnShownChanged += HandleExecutionMarker;
        }

        void OnDisable()
        {
            if (player != null) player.OnLockTargetChanged -= HandleLockTargetChanged;
            if (executionMarker != null) executionMarker.OnShownChanged -= HandleExecutionMarker;
        }

        void HandleExecutionMarker(bool shown)
        {
            hiddenByExecution = shown;
            if (!shown) shownTime = Time.unscaledTime; // 處決標記消失後重新彈出
        }

        void HandleLockTargetChanged(LockOnTarget next)
        {
            target = next;
            shownTime = Time.unscaledTime;
        }

        void LateUpdate()
        {
            bool visible = target != null && viewCamera != null && !hiddenByExecution;
            Vector3 screen = Vector3.zero;
            if (visible)
            {
                screen = viewCamera.WorldToScreenPoint(target.AimPoint.position);
                visible = screen.z > 0f;
            }
            if (marker.gameObject.activeSelf != visible) marker.gameObject.SetActive(visible);
            if (!visible) return;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(area, screen, null, out var local);
            marker.anchoredPosition = local;

            float t = Time.unscaledTime - shownTime;
            float pop = Mathf.Clamp01(t / data.lockMarkerPopTime);
            float scale = Mathf.Lerp(data.lockMarkerPopScale, 1f, 1f - (1f - pop) * (1f - pop));
            scale *= 1f + Mathf.Sin(t * data.lockMarkerPulseRate * Mathf.PI * 2f) * data.lockMarkerPulse;
            marker.localScale = Vector3.one * scale;
        }
    }
}
