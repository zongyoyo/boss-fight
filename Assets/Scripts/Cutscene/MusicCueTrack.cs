using BossFight.Core;
using UnityEngine.Timeline;

namespace BossFight.Cutscene
{
    /// <summary>在指定時間開始播戰鬥音樂（開場第 6 鏡）。</summary>
    [TrackColor(0.95f, 0.5f, 0.3f)]
    [TrackClipType(typeof(MusicCueClip))]
    [TrackBindingType(typeof(AudioManager))]
    public class MusicCueTrack : TrackAsset
    {
    }
}
