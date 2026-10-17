using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace BossFight.Cutscene
{
    /// <summary>
    /// 曲線片段：在片段期間依曲線輸出一個數值（橫軸 = 片段進度 0..1）。CurveTrack、StormTrack、BossSpinTrack、MagicCircleTrack 共用，
    /// 數值代表什麼由 Track 決定（亮度、暴風進度、轉速、出現進度…）。片段的 Post-Extrapolation 設 Hold 時，結束後維持最後的值。
    /// </summary>
    public class CurveClip : PlayableAsset, ITimelineClipAsset
    {
        public CurveBehaviour template = new CurveBehaviour();

        public ClipCaps clipCaps => ClipCaps.Blending | ClipCaps.Extrapolation;

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner) =>
            ScriptPlayable<CurveBehaviour>.Create(graph, template);
    }

    [Serializable]
    public class CurveBehaviour : PlayableBehaviour
    {
        [Tooltip("橫軸是片段進度 0..1，縱軸是輸出值")]
        public AnimationCurve curve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        [Tooltip("輸出值再乘上這個倍率")]
        public float scale = 1f;

        /// <summary>片段本身的長度（秒），由 Track 在建立 playable 時填入；Hold 時 playable 的長度會包含延伸的部分，不能拿來算進度。</summary>
        [NonSerialized] public double clipDuration;

        public float Evaluate(Playable playable) => curve.Evaluate(ClipProgress(playable, clipDuration)) * scale;

        /// <summary>片段進度 0..1（Hold 延伸時停在 1）。</summary>
        public static float ClipProgress(Playable playable, double clipDuration)
        {
            double duration = clipDuration > 0.0 ? clipDuration : playable.GetDuration();
            return duration > 0.0 ? Mathf.Clamp01((float)(playable.GetTime() / duration)) : 1f;
        }
    }

    /// <summary>
    /// 曲線軌道的混合器：把有權重的片段輸出加權相加，交給 Track 指定的 apply。
    /// 只在 Play 模式套用（Timeline 視窗在編輯模式預覽時不改場景，只看得到鏡頭、Activation 與聲音）。
    /// </summary>
    public class CurveMixerBehaviour : PlayableBehaviour
    {
        /// <summary>參數：綁定的物件、加權後的值、這一幀有沒有任何片段在作用。</summary>
        public Action<object, float, bool> apply;

        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            if (!Application.isPlaying || playerData == null || apply == null) return;

            float value = 0f;
            bool active = false;
            int count = playable.GetInputCount();
            for (int i = 0; i < count; i++)
            {
                float weight = playable.GetInputWeight(i);
                if (weight <= 0f) continue;
                var input = (ScriptPlayable<CurveBehaviour>)playable.GetInput(i);
                value += weight * input.GetBehaviour().Evaluate(input);
                active = true;
            }
            apply(playerData, value, active);
        }

        public static Playable Create(PlayableGraph graph, int inputCount, Action<object, float, bool> apply)
        {
            var mixer = ScriptPlayable<CurveMixerBehaviour>.Create(graph, inputCount);
            mixer.GetBehaviour().apply = apply;
            return mixer;
        }
    }
}
