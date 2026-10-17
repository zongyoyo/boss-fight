using System;
using BossFight.Player;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace BossFight.Cutscene
{
    /// <summary>
    /// 玩家走一段路：片段期間從 from 走到 to（世界座標，只看水平），走路的快慢照 progress 曲線。
    /// 每幀算出「現在該在哪」交給 PlayerController.ScriptedMoveTo，由 CharacterController 走過去，所以走路動畫照常；
    /// 離該在的位置超過 snapDistance（第一幀、跳過）時直接瞬移。玩家要先被 SetInputLocked 鎖住才會照著走。
    /// </summary>
    public class ActorWalkClip : PlayableAsset, ITimelineClipAsset
    {
        public ActorWalkBehaviour template = new ActorWalkBehaviour();

        public ClipCaps clipCaps => ClipCaps.Extrapolation;

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner) =>
            ScriptPlayable<ActorWalkBehaviour>.Create(graph, template);
    }

    [Serializable]
    public class ActorWalkBehaviour : PlayableBehaviour
    {
        [Tooltip("起點（世界座標，高度不看）")]
        public Vector3 from;
        [Tooltip("終點（世界座標，高度不看）")]
        public Vector3 to;
        [Tooltip("橫軸是片段進度 0..1，縱軸是走了全程的多少（0..1）")]
        public AnimationCurve progress = new AnimationCurve(new Keyframe(0f, 0f, 1.25f, 1.25f), new Keyframe(0.75f, 0.94f, 0.6f, 0.6f), new Keyframe(1f, 1f, 0f, 0f));
        [Tooltip("離該在的位置超過這個距離 (m) 就直接瞬移")]
        [Min(0.01f)] public float snapDistance = 1f;

        /// <summary>片段本身的長度（秒），由 ActorWalkTrack 填入（Hold 時 playable 的長度包含延伸部分）。</summary>
        [NonSerialized] public double clipDuration;

        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            if (!Application.isPlaying || !(playerData is PlayerController player)) return;

            float t = CurveBehaviour.ClipProgress(playable, clipDuration);
            Vector3 pos = Vector3.LerpUnclamped(from, to, progress.Evaluate(t));
            Vector3 current = player.transform.position;
            pos.y = current.y;

            Vector3 offset = pos - current;
            offset.y = 0f;
            if (offset.magnitude > snapDistance)
            {
                Vector3 dir = to - from;
                dir.y = 0f;
                player.Teleport(pos, dir.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(dir) : player.transform.rotation);
            }
            else
            {
                player.ScriptedMoveTo(pos);
            }
        }
    }
}
