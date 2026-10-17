using System;
using BossFight.Core;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace BossFight.Cutscene
{
    /// <summary>
    /// 片段開始時叫 AudioManager 從頭播戰鬥音樂（OIIA 音樂）；音樂由 AudioManager 播，開場結束後繼續。
    /// 跳過開場直接跳進片段中間也會開始播；已經在播就不重來。
    /// </summary>
    public class MusicCueClip : PlayableAsset, ITimelineClipAsset
    {
        public MusicCueBehaviour template = new MusicCueBehaviour();

        public ClipCaps clipCaps => ClipCaps.None;

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner) =>
            ScriptPlayable<MusicCueBehaviour>.Create(graph, template);
    }

    [Serializable]
    public class MusicCueBehaviour : PlayableBehaviour
    {
        bool started;

        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            if (started || !Application.isPlaying || !(playerData is AudioManager audio)) return;
            started = true;
            audio.PlayBattleMusic();
        }
    }
}
