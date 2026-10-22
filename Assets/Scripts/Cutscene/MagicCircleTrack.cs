using BossFight.Hazards;
using UnityEngine;
using UnityEngine.Timeline;

namespace BossFight.Cutscene
{
    /// <summary>魔法陣軌道驅動的項目。</summary>
    public enum MagicCircleChannel
    {
        /// <summary>出現進度 0..1（先由中心往外描繪，再逐段點亮外圈）；片段之前是 0（看不見）。</summary>
        Reveal,
        /// <summary>隨節拍脈動的強度 0..1。</summary>
        Pulse,
        /// <summary>顏色 0 紫 → 1 紅（轉階段）；片段之前是 0。</summary>
        Color,
        /// <summary>大小 0 原本半徑 → 1 場地邊緣（轉階段）；片段之前是 0。</summary>
        Expand,
    }

    /// <summary>驅動 MagicCircle 的出現、脈動、顏色或大小；同一個魔法陣可以有好幾條軌道各管一項。</summary>
    [TrackColor(0.6f, 0.25f, 0.95f)]
    [TrackClipType(typeof(CurveClip))]
    [TrackBindingType(typeof(MagicCircle))]
    public class MagicCircleTrack : CurveTrackBase
    {
        [Tooltip("這條軌道驅動哪一項")]
        public MagicCircleChannel channel = MagicCircleChannel.Reveal;

        protected override void Apply(object binding, float value, bool active)
        {
            var circle = (MagicCircle)binding;
            switch (channel)
            {
                case MagicCircleChannel.Reveal: circle.SetRevealProgress(value); break;
                case MagicCircleChannel.Pulse: circle.SetPulse(value); break;
                case MagicCircleChannel.Color: circle.SetColorBlend(value); break;
                case MagicCircleChannel.Expand: circle.SetExpand(value); break;
            }
        }
    }
}
