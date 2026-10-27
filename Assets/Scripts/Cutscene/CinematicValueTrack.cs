using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
namespace BossFight.Cutscene
{
    [TrackColor(0.55f, 0.3f, 0.15f)]
    [TrackClipType(typeof(CinematicValueClip))]
    [TrackBindingType(typeof(CinematicStage))]
    public class CinematicValueTrack : TrackAsset
    {
        protected override Playable CreatePlayable(PlayableGraph graph, GameObject go, TimelineClip clip)
        {
            var p = base.CreatePlayable(graph, go, clip);
            ((ScriptPlayable<CinematicValueBehaviour>)p).GetBehaviour().clipDuration = clip.duration;
            return p;
        }
    }
}
