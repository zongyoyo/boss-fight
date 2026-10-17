using UnityEngine.Timeline;

namespace BossFight.Cutscene
{
    /// <summary>驅動一個 CurveTarget（白屏、閃電、煙霧、鏡頭震動）；沒有片段時送 0。</summary>
    [TrackColor(0.95f, 0.85f, 0.35f)]
    [TrackClipType(typeof(CurveClip))]
    [TrackBindingType(typeof(CurveTarget))]
    public class CurveTrack : CurveTrackBase
    {
        protected override void Apply(object binding, float value, bool active) => ((CurveTarget)binding).Apply(value);
    }
}
