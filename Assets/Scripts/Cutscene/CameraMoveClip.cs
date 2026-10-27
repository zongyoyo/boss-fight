using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace BossFight.Cutscene
{
    /// <summary>鏡頭移動的路徑。</summary>
    public enum CameraPath
    {
        /// <summary>從 from 直線移到 to。</summary>
        Line,
        /// <summary>繞 center 轉：半徑 radius、高度 height，俯視角度從 fromAngle 轉到 toAngle（0 = 北，順時針）。</summary>
        Orbit,
    }

    /// <summary>
    /// 移動一台演出鏡頭（CinemachineCamera 的位置；朝向由它的 LookAt 決定）：直線或環繞，快慢照 progress 曲線。
    /// 片段之外不動。Timeline 視窗在編輯模式也能預覽，離開預覽時位置會還原。
    /// </summary>
    public class CameraMoveClip : PlayableAsset, ITimelineClipAsset
    {
        public CameraMoveBehaviour template = new CameraMoveBehaviour();

        public ClipCaps clipCaps => ClipCaps.Extrapolation;

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner) =>
            ScriptPlayable<CameraMoveBehaviour>.Create(graph, template);
    }

    [Serializable]
    public class CameraMoveBehaviour : PlayableBehaviour
    {
        public CameraPath path = CameraPath.Line;
        [Header("直線（世界座標）")]
        public Vector3 from;
        public Vector3 to;
        [Tooltip("相對舞台的局部路徑；預設使用既有世界座標")]
        public bool localSpace;
        [Header("環繞")]
        public Vector3 center;
        [Min(0f)] public float radius = 20f;
        [Tooltip("離 center 的高度 (m)")]
        public float height = 15f;
        [Tooltip("俯視角度（度）：0 = 北，順時針")]
        public float fromAngle;
        public float toAngle = 45f;
        [Header("快慢")]
        [Tooltip("橫軸是片段進度 0..1，縱軸是路徑走了多少（0..1）")]
        public AnimationCurve progress = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        /// <summary>片段本身的長度（秒），由 CameraMoveTrack 填入（Hold 時 playable 的長度包含延伸部分）。</summary>
        [NonSerialized] public double clipDuration;

        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            if (!(playerData is Transform camera)) return;
            Vector3 position = Evaluate(progress.Evaluate(CurveBehaviour.ClipProgress(playable, clipDuration)));
            if (localSpace) camera.localPosition = position;
            else camera.position = position;
        }

        Vector3 Evaluate(float s)
        {
            if (path == CameraPath.Line) return Vector3.LerpUnclamped(from, to, s);
            float a = Mathf.LerpUnclamped(fromAngle, toAngle, s) * Mathf.Deg2Rad;
            return center + new Vector3(Mathf.Sin(a) * radius, height, Mathf.Cos(a) * radius);
        }
    }
}
