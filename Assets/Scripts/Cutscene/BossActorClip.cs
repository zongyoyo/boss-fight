using System;
using BossFight.Boss;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace BossFight.Cutscene
{
    /// <summary>演出中 Boss 的動作。</summary>
    public enum BossActorAction
    {
        /// <summary>被擊退：往遠離玩家的方向滑 distance 公尺，快慢照 progress 曲線（不會滑出場地）。</summary>
        Knockback,
        /// <summary>瞬移回場地中心（法陣中央），片段期間一直面向玩家。</summary>
        ReturnToCenter,
    }

    /// <summary>
    /// 演出中直接移動 Boss（轉階段第 1 步：被擊退、瞬移回法陣中央）。Boss 在轉階段（Transition）中不會自己移動，所以直接改它的 transform。
    /// 片段第一幀記下起點與方向，之後依片段進度算位置；只在 Play 模式套用。
    /// </summary>
    public class BossActorClip : PlayableAsset, ITimelineClipAsset
    {
        public BossActorBehaviour template = new BossActorBehaviour();

        public ClipCaps clipCaps => ClipCaps.Extrapolation;

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner) =>
            ScriptPlayable<BossActorBehaviour>.Create(graph, template);
    }

    [Serializable]
    public class BossActorBehaviour : PlayableBehaviour
    {
        public BossActorAction action = BossActorAction.Knockback;
        [Tooltip("擊退距離 (m)")]
        [Min(0f)] public float distance = 2.5f;
        [Tooltip("橫軸是片段進度 0..1，縱軸是滑了全程的多少（0..1）")]
        public AnimationCurve progress = new AnimationCurve(new Keyframe(0f, 0f, 3f, 3f), new Keyframe(1f, 1f, 0f, 0f));

        /// <summary>片段本身的長度（秒），由 BossActorTrack 填入（Hold 時 playable 的長度包含延伸部分）。</summary>
        [NonSerialized] public double clipDuration;

        bool started;
        Vector3 start, direction;

        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            if (!Application.isPlaying || !(playerData is BossController boss)) return;
            Transform t = boss.transform;

            if (!started)
            {
                started = true;
                start = t.position;
                direction = boss.Target != null ? Flat(t.position - boss.Target.position) : Vector3.zero;
                if (direction.sqrMagnitude < 1e-4f) direction = -Flat(t.forward);
                direction.Normalize();
                if (action == BossActorAction.ReturnToCenter)
                {
                    Vector3 c = boss.ArenaCenter;
                    t.position = new Vector3(c.x, t.position.y, c.z);
                }
            }

            if (action == BossActorAction.Knockback)
            {
                float s = progress.Evaluate(CurveBehaviour.ClipProgress(playable, clipDuration));
                t.position = boss.ClampToArena(start + direction * (distance * s));
                return;
            }

            if (boss.Target != null)
            {
                Vector3 to = Flat(boss.Target.position - t.position);
                if (to.sqrMagnitude > 1e-4f) t.rotation = Quaternion.LookRotation(to);
            }
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
