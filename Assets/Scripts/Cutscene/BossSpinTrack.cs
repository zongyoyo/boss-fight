using BossFight.Boss;
using UnityEngine.Timeline;

namespace BossFight.Cutscene
{
    /// <summary>
    /// 貓原地旋轉：片段的曲線值（× scale）就是 OiiaCatPoser 的轉速（度/秒）。沒有片段時不動 OiiaCatPoser；
    /// 片段 Hold 到開場結束，轉速留在 OiiaCatPoser 上，迴旋衝撞的預兆從這個轉速接著加速。
    /// </summary>
    [TrackColor(0.85f, 0.45f, 0.95f)]
    [TrackClipType(typeof(CurveClip))]
    [TrackBindingType(typeof(OiiaCatPoser))]
    public class BossSpinTrack : CurveTrackBase
    {
        protected override void Apply(object binding, float value, bool active)
        {
            if (active) ((OiiaCatPoser)binding).SetSpinImmediate(value);
        }
    }
}
