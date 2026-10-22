using BossFight.Boss;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace BossFight.Cutscene
{
    /// <summary>演出中移動 Boss（BossActorClip：擊退、瞬移回中央）。</summary>
    [TrackColor(0.95f, 0.4f, 0.55f)]
    [TrackClipType(typeof(BossActorClip))]
    [TrackBindingType(typeof(BossController))]
    public class BossActorTrack : TrackAsset
    {
        protected override Playable CreatePlayable(PlayableGraph graph, GameObject gameObject, TimelineClip clip)
        {
            var playable = base.CreatePlayable(graph, gameObject, clip);
            if (playable.IsValid() && playable.GetPlayableType() == typeof(BossActorBehaviour))
                ((ScriptPlayable<BossActorBehaviour>)playable).GetBehaviour().clipDuration = clip.duration;
            return playable;
        }
    }
}
