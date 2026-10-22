using System;
using BossFight.Core;
using BossFight.Player;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace BossFight.Cutscene
{
    /// <summary>
    /// 把玩家從 center 往外推到離中心 radius 公尺（只往外推，已經在更外面就不動），面向 center 往後滑。
    /// 轉階段第 1 步：狗離貓太近時先退到對視的距離（自訂半徑）；第 5 步：無傷害衝擊波把狗推到外圈（ArenaData.transitionPushRadius）。
    /// 片段第一幀記下起點，之後依 progress 曲線交給 PlayerController.ScriptedMoveTo；玩家要先被 SetInputLocked 鎖住才會照著走。
    /// </summary>
    public class ActorPushClip : PlayableAsset, ITimelineClipAsset
    {
        public ActorPushBehaviour template = new ActorPushBehaviour();

        public ClipCaps clipCaps => ClipCaps.Extrapolation;

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner) =>
            ScriptPlayable<ActorPushBehaviour>.Create(graph, template);
    }

    [Serializable]
    public class ActorPushBehaviour : PlayableBehaviour
    {
        [Tooltip("往外推的中心（世界座標，場地中心）")]
        public Vector3 center;
        [Tooltip("推到的半徑讀 ArenaData.transitionPushRadius；留空時用下面的 radius")]
        public ArenaData arena;
        [Tooltip("arena 留空時推到的半徑 (m)")]
        [Min(0f)] public float radius = 6f;
        [Tooltip("橫軸是片段進度 0..1，縱軸是推了全程的多少（0..1）")]
        public AnimationCurve progress = new AnimationCurve(new Keyframe(0f, 0f, 3f, 3f), new Keyframe(1f, 1f, 0f, 0f));

        /// <summary>片段本身的長度（秒），由 ActorPushTrack 填入（Hold 時 playable 的長度包含延伸部分）。</summary>
        [NonSerialized] public double clipDuration;

        bool started, moving;
        Vector3 from, to;

        /// <summary>推到的半徑 (m)。</summary>
        public float TargetRadius => arena != null ? arena.transitionPushRadius : radius;

        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            if (!Application.isPlaying || !(playerData is PlayerController player)) return;

            if (!started)
            {
                started = true;
                from = player.transform.position;
                Vector3 offset = Flat(from - center);
                float r = TargetRadius;
                moving = offset.magnitude < r;
                if (moving)
                {
                    Vector3 dir = offset.sqrMagnitude > 1e-4f ? offset.normalized : -Flat(player.transform.forward).normalized;
                    to = center + dir * r;
                    to.y = from.y;
                }
            }
            if (!moving) return;

            float s = progress.Evaluate(CurveBehaviour.ClipProgress(playable, clipDuration));
            Vector3 pos = Vector3.LerpUnclamped(from, to, s);
            pos.y = player.transform.position.y;
            player.ScriptedMoveTo(pos, center - pos);
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
