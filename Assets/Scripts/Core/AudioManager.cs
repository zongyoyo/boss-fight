using UnityEngine;

namespace BossFight.Core
{
    /// <summary>
    /// 戰鬥音樂（OIIA 音樂）的播放與變速，以及一次性音效。開場由 Timeline 的 MusicCue 在第 6 鏡呼叫 PlayBattleMusic，
    /// 之後一直播到戰鬥結束；第二階段、狂暴用 SetMusicSpeed 改速度（T7、T8）。
    /// 還沒指定音樂（Assets/Audio/oiia_bgm 還沒放進來）時只在 Console 寫一行一般訊息，不報錯，開場與戰鬥照常進行；
    /// 檔案放進來後在 Inspector 的 battleMusic 指定即可。開場的腳步、風聲、嗡鳴、雷聲是 Timeline 自己的 Audio Track，不經過這裡。
    /// </summary>
    [DisallowMultipleComponent]
    public class AudioManager : MonoBehaviour
    {
        [Tooltip("播音樂的 AudioSource（2D、循環）")]
        [SerializeField] AudioSource musicSource;
        [Tooltip("播一次性音效的 AudioSource（2D）")]
        [SerializeField] AudioSource sfxSource;
        [Tooltip("OIIA 戰鬥音樂。檔案放進 Assets/Audio/oiia_bgm 後在這裡指定；留空時沒有音樂，其他照常")]
        [SerializeField] AudioClip battleMusic;
        [Tooltip("音樂音量")]
        [Range(0f, 1f)] [SerializeField] float musicVolume = 0.8f;

        /// <summary>目前的音樂速度倍率（1 = 原速）。</summary>
        public float MusicSpeed { get; private set; } = 1f;
        public bool IsMusicPlaying => musicSource != null && musicSource.isPlaying;

        bool reportedMissingMusic;

        /// <summary>從頭開始播戰鬥音樂（循環）；已經在播就不重來。沒有指定音樂時什麼都不做。</summary>
        public void PlayBattleMusic()
        {
            if (musicSource == null) return;
            if (battleMusic == null)
            {
                if (!reportedMissingMusic)
                    Debug.Log("[Audio] 還沒指定戰鬥音樂（AudioManager.battleMusic），這次沒有音樂。放進 Assets/Audio/oiia_bgm 後在 Inspector 指定即可。", this);
                reportedMissingMusic = true;
                return;
            }
            if (musicSource.clip == battleMusic && musicSource.isPlaying) return;

            musicSource.clip = battleMusic;
            musicSource.loop = true;
            musicSource.volume = musicVolume;
            musicSource.pitch = MusicSpeed;
            musicSource.Play();
        }

        /// <summary>音樂速度倍率（第二階段 1.2、狂暴 1.3）；用 pitch 實作，音高會跟著變。</summary>
        public void SetMusicSpeed(float speed)
        {
            MusicSpeed = Mathf.Max(0.01f, speed);
            if (musicSource != null) musicSource.pitch = MusicSpeed;
        }

        public void StopMusic()
        {
            if (musicSource != null) musicSource.Stop();
        }

        /// <summary>播一次音效（2D）。clip 為 null 時忽略。</summary>
        public void PlaySfx(AudioClip clip, float volume = 1f)
        {
            if (clip != null && sfxSource != null) sfxSource.PlayOneShot(clip, volume);
        }
    }
}
