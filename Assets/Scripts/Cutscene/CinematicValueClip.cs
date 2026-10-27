using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace BossFight.Cutscene
{
    public enum CinematicValue { Darkness, Rise, Wither, Glow, RedFlash, WhiteFlash, Black, Blur, Vignette, LowPass, Lying, Shake, Debris, Birds, Wave, Push, Title, Slow, GroundCrack }

    /// <summary>一段可調曲線驅動一個演出通道；長度由軌道填入，不用 Hold 延伸的 duration。</summary>
    [Serializable]
    public class CinematicValueBehaviour : PlayableBehaviour
    {
        [Tooltip("套用的演出通道") ] public CinematicValue channel;
        [Tooltip("片段進度 0..1 對應的輸出值") ] public AnimationCurve curve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        [NonSerialized] public double clipDuration;
        public override void ProcessFrame(Playable playable, FrameData info, object binding)
        {
            if (!Application.isPlaying || info.effectiveWeight <= 0f || !(binding is CinematicStage stage)) return;
            stage.Apply(channel, curve.Evaluate(CurveBehaviour.ClipProgress(playable, clipDuration)));
        }
    }
    public class CinematicValueClip : PlayableAsset, ITimelineClipAsset
    {
        [Tooltip("片段的通道與曲線") ] public CinematicValueBehaviour template = new CinematicValueBehaviour();
        public ClipCaps clipCaps => ClipCaps.Extrapolation;
        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner) => ScriptPlayable<CinematicValueBehaviour>.Create(graph, template);
    }
}
