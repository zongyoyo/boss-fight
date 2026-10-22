using BossFight.Core;
using UnityEngine.Timeline;

namespace BossFight.Cutscene
{
    /// <summary>在指定時間播、停或重播戰鬥音樂（開場第 6 鏡、轉階段第 2 步與第 4 步）。</summary>
    [TrackColor(0.95f, 0.5f, 0.3f)]
    [TrackClipType(typeof(MusicCueClip))]
    [TrackBindingType(typeof(AudioManager))]
    public class MusicCueTrack : TrackAsset
    {
    }
}
