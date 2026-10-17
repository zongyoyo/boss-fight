using UnityEngine;

namespace BossFight.Cutscene
{
    /// <summary>全螢幕白屏（閃電劈下時）：CurveTrack 的值就是不透明度。畫面最上層的全螢幕 Image，用 CanvasGroup 控制。</summary>
    [DisallowMultipleComponent]
    public class ScreenFlash : CurveTarget
    {
        [SerializeField] CanvasGroup group;

        void Awake() => Apply(0f);

        public override void Apply(float value)
        {
            group.alpha = Mathf.Clamp01(value);
        }
    }
}
