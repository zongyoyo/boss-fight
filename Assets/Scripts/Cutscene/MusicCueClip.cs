using System;
using BossFight.Core;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace BossFight.Cutscene
{
    /// <summary>音樂提示的動作。</summary>
    public enum MusicCueAction
    {
        /// <summary>開始播戰鬥音樂；已經在播就不重來（開場第 6 鏡）。</summary>
        Play,
        /// <summary>停止音樂（轉階段的唱片刮擦後靜音）。</summary>
        Stop,
        /// <summary>從頭重播，用 AudioManager 目前的速度（轉階段第 4 步以第二階段的速度重新播放）。</summary>
        Restart,
    }

    /// <summary>
    /// 片段開始時叫 AudioManager 對戰鬥音樂（OIIA 音樂）做一個動作；音樂由 AudioManager 播，演出結束後繼續。
    /// 跳過開場直接跳進片段中間也會執行；每個片段只執行一次。片段設成 Hold 時延伸到之後也算（轉階段的「Restart」：跳過演出時在結尾補上重播）。
    /// </summary>
    public class MusicCueClip : PlayableAsset, ITimelineClipAsset
    {
        public MusicCueBehaviour template = new MusicCueBehaviour();

        public ClipCaps clipCaps => ClipCaps.Extrapolation;

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner) =>
            ScriptPlayable<MusicCueBehaviour>.Create(graph, template);
    }

    [Serializable]
    public class MusicCueBehaviour : PlayableBehaviour
    {
        [Tooltip("片段開始時做什麼")]
        public MusicCueAction action = MusicCueAction.Play;

        bool started;

        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            if (started || !Application.isPlaying || !(playerData is AudioManager audio)) return;
            started = true;
            switch (action)
            {
                case MusicCueAction.Play: audio.PlayBattleMusic(); break;
                case MusicCueAction.Stop: audio.StopMusic(); break;
                case MusicCueAction.Restart: audio.RestartBattleMusic(); break;
            }
        }
    }
}
