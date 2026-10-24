using System;
using BossFight.Core;
using UnityEngine;
using UnityEngine.UI;

namespace BossFight.UI
{
    /// <summary>
    /// 處決標記：目標可以被處決時（Boss 頭暈中，不管玩家離多遠）在它身上（aimPoint，Boss 的 LockOnTarget.AimPoint）顯示紅色圓環加中心點，
    /// 出現時從大縮回、之後一直脈動。只訂閱 ExecutionTarget.OnExecutableChanged；位置每幀用 viewCamera 投到畫面上（目標在鏡頭後方時隱藏）。
    /// 顯示時送 OnShownChanged，LockOnMarkerUI 收到後先把鎖定菱形藏起來，兩個標記不會疊在一起。
    /// </summary>
    [DisallowMultipleComponent]
    public class ExecutionMarkerUI : MonoBehaviour
    {
        [SerializeField] BattleUIData data;
        [Tooltip("可以被處決的目標（Boss）")]
        [SerializeField] ExecutionTarget target;
        [Tooltip("標記的位置（Boss 的 LockOnTarget.AimPoint，胸口）")]
        [SerializeField] Transform aimPoint;
        [Tooltip("投影用的鏡頭（Main Camera）")]
        [SerializeField] Camera viewCamera;
        [Tooltip("標記（父物件是全螢幕的 RectTransform）")]
        [SerializeField] RectTransform marker;
        [Tooltip("換成 BattleUIData.executionMarkerColor 的部分（圓環、中心點；描邊不放）")]
        [SerializeField] Image[] tinted;

        /// <summary>標記出現（true）或消失（false）。</summary>
        public event Action<bool> OnShownChanged;

        RectTransform area;
        bool executable;
        float shownTime;

        void Awake()
        {
            area = marker.parent as RectTransform;
            if (tinted != null)
            {
                foreach (var image in tinted)
                {
                    if (image != null) image.color = data.executionMarkerColor;
                }
            }
            marker.gameObject.SetActive(false);
        }

        void OnEnable()
        {
            if (target != null) target.OnExecutableChanged += HandleExecutableChanged;
        }

        void OnDisable()
        {
            if (target != null) target.OnExecutableChanged -= HandleExecutableChanged;
            HandleExecutableChanged(false);
        }

        void HandleExecutableChanged(bool value)
        {
            if (executable == value) return;
            executable = value;
            shownTime = Time.unscaledTime;
            OnShownChanged?.Invoke(value);
        }

        void LateUpdate()
        {
            bool visible = executable && aimPoint != null && viewCamera != null;
            Vector3 screen = Vector3.zero;
            if (visible)
            {
                screen = viewCamera.WorldToScreenPoint(aimPoint.position);
                visible = screen.z > 0f;
            }
            if (marker.gameObject.activeSelf != visible) marker.gameObject.SetActive(visible);
            if (!visible) return;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(area, screen, null, out var local);
            marker.anchoredPosition = local;

            float t = Time.unscaledTime - shownTime;
            float pop = Mathf.Clamp01(t / data.executionMarkerPopTime);
            float scale = Mathf.Lerp(data.executionMarkerPopScale, 1f, 1f - (1f - pop) * (1f - pop));
            scale *= 1f + Mathf.Sin(t * data.executionMarkerPulseRate * Mathf.PI * 2f) * data.executionMarkerPulse;
            marker.localScale = Vector3.one * scale;
        }
    }
}
