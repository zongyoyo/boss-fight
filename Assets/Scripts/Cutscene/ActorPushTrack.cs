using BossFight.Player;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace BossFight.Cutscene
{
    /// <summary>把玩家往外推（ActorPushClip：轉階段時退到對視距離、被衝擊波推到外圈）。</summary>
    [TrackColor(0.4f, 0.8f, 0.7f)]
    [TrackClipType(typeof(ActorPushClip))]
    [TrackBindingType(typeof(PlayerController))]
    public class ActorPushTrack : TrackAsset
    {
        protected override Playable CreatePlayable(PlayableGraph graph, GameObject gameObject, TimelineClip clip)
        {
            var playable = base.CreatePlayable(graph, gameObject, clip);
            if (playable.IsValid() && playable.GetPlayableType() == typeof(ActorPushBehaviour))
                ((ScriptPlayable<ActorPushBehaviour>)playable).GetBehaviour().clipDuration = clip.duration;
            return playable;
        }
    }
}
