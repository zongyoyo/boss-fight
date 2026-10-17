using BossFight.Core;
using UnityEngine.Timeline;

namespace BossFight.Cutscene
{
    /// <summary>天色變暗、雷雲聚集：片段的曲線值就是 ArenaAtmosphere 的暴風進度（0 平靜 → 1 暴風雨）；片段之前是 0，Hold 之後維持。</summary>
    [TrackColor(0.35f, 0.45f, 0.75f)]
    [TrackClipType(typeof(CurveClip))]
    [TrackBindingType(typeof(ArenaAtmosphere))]
    public class StormTrack : CurveTrackBase
    {
        protected override void Apply(object binding, float value, bool active) => ((ArenaAtmosphere)binding).SetStorm(value);
    }
}
