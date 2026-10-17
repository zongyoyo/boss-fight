using BossFight.Player;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace BossFight.Cutscene
{
    /// <summary>讓玩家照 ActorWalkClip 走位（開場第 1 鏡走進競技場）。</summary>
    [TrackColor(0.4f, 0.8f, 0.45f)]
    [TrackClipType(typeof(ActorWalkClip))]
    [TrackBindingType(typeof(PlayerController))]
    public class ActorWalkTrack : TrackAsset
    {
        protected override Playable CreatePlayable(PlayableGraph graph, GameObject gameObject, TimelineClip clip)
        {
            var playable = base.CreatePlayable(graph, gameObject, clip);
            if (playable.IsValid() && playable.GetPlayableType() == typeof(ActorWalkBehaviour))
                ((ScriptPlayable<ActorWalkBehaviour>)playable).GetBehaviour().clipDuration = clip.duration;
            return playable;
        }
    }
}
