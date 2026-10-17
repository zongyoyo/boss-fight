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
    }

    /// <summary>驅動 MagicCircle 的出現或脈動；同一個魔法陣可以有兩條軌道各管一項。</summary>
    [TrackColor(0.6f, 0.25f, 0.95f)]
    [TrackClipType(typeof(CurveClip))]
    [TrackBindingType(typeof(MagicCircle))]
    public class MagicCircleTrack : CurveTrackBase
    {
        [Tooltip("這條軌道驅動出現進度還是脈動強度")]
        public MagicCircleChannel channel = MagicCircleChannel.Reveal;

        protected override void Apply(object binding, float value, bool active)
        {
            var circle = (MagicCircle)binding;
            if (channel == MagicCircleChannel.Reveal) circle.SetRevealProgress(value);
            else circle.SetPulse(value);
        }
    }
}
