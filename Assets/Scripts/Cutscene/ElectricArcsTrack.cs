using BossFight.Hazards;
using UnityEngine.Timeline;

namespace BossFight.Cutscene
{
    /// <summary>
    /// 電光（Hazards/ElectricArcs）的強度：片段的曲線值就是 SetIntensity（0 隱藏 → 1 全亮）；沒有片段時送 0。
    /// 轉階段第 3 步場地邊緣冒出的電光、第 4 步貓身上纏繞的紅色閃電；Boss 身上的片段 Hold 到演出結束，之後由 BossPhaseVisual 維持。
    /// </summary>
    [TrackColor(1f, 0.3f, 0.25f)]
    [TrackClipType(typeof(CurveClip))]
    [TrackBindingType(typeof(ElectricArcs))]
    public class ElectricArcsTrack : CurveTrackBase
    {
        protected override void Apply(object binding, float value, bool active) => ((ElectricArcs)binding).SetIntensity(value);
    }
}
