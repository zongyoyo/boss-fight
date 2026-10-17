using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace BossFight.Cutscene
{
    /// <summary>
    /// 用 CurveClip 的軌道共用的部分：混合器把片段的曲線值加權相加後交給 Apply；建立片段時把片段長度填進 CurveBehaviour。
    /// </summary>
    public abstract class CurveTrackBase : TrackAsset
    {
        /// <summary>套用到綁定的物件；active = 這一幀有沒有任何片段在作用（沒有時 value 是 0）。</summary>
        protected abstract void Apply(object binding, float value, bool active);

        public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount) =>
            CurveMixerBehaviour.Create(graph, inputCount, Apply);

        protected override Playable CreatePlayable(PlayableGraph graph, GameObject gameObject, TimelineClip clip)
        {
            var playable = base.CreatePlayable(graph, gameObject, clip);
            if (playable.IsValid() && playable.GetPlayableType() == typeof(CurveBehaviour))
                ((ScriptPlayable<CurveBehaviour>)playable).GetBehaviour().clipDuration = clip.duration;
            return playable;
        }
    }
}
