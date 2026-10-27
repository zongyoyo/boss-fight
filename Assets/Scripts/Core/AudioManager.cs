using UnityEngine;

namespace BossFight.Core
{
    /// <summary>
    /// 戰鬥音樂（OIIA 音樂）的播放與變速，以及一次性音效。開場由 Timeline 的 MusicCue 在第 6 鏡呼叫 PlayBattleMusic，
    /// 之後一直播到戰鬥結束；換階段時 BattleManager 用 SetMusicSpeed 改速度（第二階段 1.2、狂暴 1.3）。
    /// 轉階段演出先 StopMusic（唱片刮擦後靜音），再由 Timeline 的 MusicCue 用 RestartBattleMusic 以新的速度從頭播；
    /// BattleManager 在演出開始時用 SetMusicSpeed(speed, false) 先排好速度，重播時才套用，演出前段的音樂不會先變快。
    /// 還沒指定音樂（Assets/Audio/oiia_bgm 還沒放進來）時只在 Console 寫一行一般訊息，不報錯，開場與戰鬥照常進行；
    /// 檔案放進來後在 Inspector 的 battleMusic 指定即可。勝利時 BattleManager 用 FadeOutMusic 淡出（失敗時照播）。開場的腳步、風聲、嗡鳴、雷聲是 Timeline 自己的 Audio Track，不經過這裡。
    /// 音量訂閱 GameOptions（ESC 選單的音量設定）：主音量是 AudioListener.volume；音樂乘在 musicVolume 上；音效套到音效、選單試聽與演出 Timeline 音軌的 AudioSource（乘在各自原本的音量上）。
    /// 暫停（GamePause 的 AudioListener.pause）時音效停住；音樂與選單試聽設成不受暫停影響。
    /// </summary>
    [DisallowMultipleComponent]
    public class AudioManager : MonoBehaviour
    {
        [Tooltip("播音樂的 AudioSource（2D、循環）")]
        [SerializeField] AudioSource musicSource;
        [Tooltip("播一次性音效的 AudioSource（2D）")]
        [SerializeField] AudioSource sfxSource;
        [Tooltip("ESC 選單試聽音效用的 AudioSource（2D、不受暫停影響）；留空時用 sfxSource")]
        [SerializeField] AudioSource uiSource;
        [Tooltip("開場、轉階段演出 Timeline 音軌用的 AudioSource：音量算在音效裡")]
        [SerializeField] AudioSource[] cutsceneSources = new AudioSource[0];
        [Tooltip("OIIA 戰鬥音樂。檔案放進 Assets/Audio/oiia_bgm 後在這裡指定；留空時沒有音樂，其他照常")]
        [SerializeField] AudioClip battleMusic;
        [Tooltip("音樂音量（玩家設定的音樂音量乘在這上面）")]
        [Range(0f, 1f)] [SerializeField] float musicVolume = 0.8f;
        [Tooltip("玩家設定的音量；留空時都當作 1")]
        [SerializeField] GameOptions options;
        [Tooltip("暫停（ESC 選單）時音樂繼續播")]
        [SerializeField] bool musicIgnoresPause = true;

        /// <summary>目前的音樂速度倍率（1 = 原速）。</summary>
        public float MusicSpeed { get; private set; } = 1f;
        public bool IsMusicPlaying => musicSource != null && musicSource.isPlaying;

        bool reportedMissingMusic;
        float fadeTime, fadeElapsed;
        float musicLevel = 1f, sfxLevel = 1f;
        float sfxBaseVolume = 1f, uiBaseVolume = 1f;
        float[] cutsceneBaseVolumes;

        // 淡出進度：1 = 沒有在淡出
        float FadeFactor => fadeTime > 0f ? 1f - Mathf.Clamp01(fadeElapsed / fadeTime) : 1f;

        void Awake()
        {
            if (musicSource != null && musicIgnoresPause) musicSource.ignoreListenerPause = true;
            if (sfxSource != null) sfxBaseVolume = sfxSource.volume;
            if (uiSource != null)
            {
                uiSource.ignoreListenerPause = true;
                uiBaseVolume = uiSource.volume;
            }
            cutsceneBaseVolumes = new float[cutsceneSources.Length];
            for (int i = 0; i < cutsceneSources.Length; i++)
                cutsceneBaseVolumes[i] = cutsceneSources[i] != null ? cutsceneSources[i].volume : 1f;
        }

        void OnEnable()
        {
            if (options != null) options.OnVolumeChanged += HandleVolumeChanged;
        }

        void OnDisable()
        {
            if (options != null) options.OnVolumeChanged -= HandleVolumeChanged;
        }

        void HandleVolumeChanged(VolumeChannel channel, float value)
        {
            switch (channel)
            {
                case VolumeChannel.Master:
                    AudioListener.volume = value;
                    break;
                case VolumeChannel.Music:
                    musicLevel = value;
                    ApplyMusicVolume();
                    break;
                case VolumeChannel.Sfx:
                    sfxLevel = value;
                    ApplySfxVolume();
                    break;
            }
        }

        void ApplyMusicVolume()
        {
            if (musicSource != null) musicSource.volume = musicVolume * musicLevel * FadeFactor;
        }

        void ApplySfxVolume()
        {
            if (sfxSource != null) sfxSource.volume = sfxBaseVolume * sfxLevel;
            if (uiSource != null) uiSource.volume = uiBaseVolume * sfxLevel;
            for (int i = 0; i < cutsceneSources.Length; i++)
            {
                if (cutsceneSources[i] != null) cutsceneSources[i].volume = cutsceneBaseVolumes[i] * sfxLevel;
            }
        }

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
            if (musicSource.clip == battleMusic && musicSource.isPlaying && fadeTime <= 0f) return;

            fadeTime = 0f;
            musicSource.clip = battleMusic;
            musicSource.loop = true;
            ApplyMusicVolume();
            musicSource.pitch = MusicSpeed;
            musicSource.Play();
        }

        /// <summary>從頭重播戰鬥音樂（轉階段演出），用目前的 MusicSpeed。沒有指定音樂時什麼都不做。</summary>
        public void RestartBattleMusic()
        {
            StopMusic();
            PlayBattleMusic();
        }

        /// <summary>劇情指定的場景音樂；保留既有戰鬥音樂入口與音量設定。</summary>
        public void PlayMusic(AudioClip clip)
        {
            if (musicSource == null || clip == null) return;
            fadeTime = 0f; musicSource.clip = clip; musicSource.loop = true;
            ApplyMusicVolume(); musicSource.pitch = MusicSpeed; musicSource.Play();
        }

        /// <summary>
        /// 音樂速度倍率（第二階段 1.2、狂暴 1.3）；用 pitch 實作，音高會跟著變。
        /// applyNow = false 時只記下來，下次 PlayBattleMusic／RestartBattleMusic 才套用（正在播的音樂速度不變）。
        /// </summary>
        public void SetMusicSpeed(float speed, bool applyNow = true)
        {
            MusicSpeed = Mathf.Max(0.01f, speed);
            if (applyNow && musicSource != null) musicSource.pitch = MusicSpeed;
        }

        public void StopMusic()
        {
            fadeTime = 0f;
            if (musicSource != null) musicSource.Stop();
        }

        /// <summary>音樂在 duration 秒（真實時間，不受慢動作影響）內淡出後停止（勝利結算）；duration ≤ 0 時直接停。</summary>
        public void FadeOutMusic(float duration)
        {
            if (musicSource == null || !musicSource.isPlaying) return;
            if (duration <= 0f)
            {
                StopMusic();
                return;
            }
            fadeTime = duration;
            fadeElapsed = 0f;
        }

        void Update()
        {
            if (fadeTime <= 0f || musicSource == null) return;
            fadeElapsed += Time.unscaledDeltaTime;
            ApplyMusicVolume();
            if (fadeElapsed >= fadeTime) StopMusic();
        }

        /// <summary>播一次音效（2D）。clip 為 null 時忽略。</summary>
        public void PlaySfx(AudioClip clip, float volume = 1f)
        {
            if (clip != null && sfxSource != null) sfxSource.PlayOneShot(clip, volume);
        }

        /// <summary>播一次選單的音效（2D、暫停中也會響，音量算在音效裡）。clip 為 null 時忽略。</summary>
        public void PlayUiSfx(AudioClip clip, float volume = 1f)
        {
            var source = uiSource != null ? uiSource : sfxSource;
            if (clip != null && source != null) source.PlayOneShot(clip, volume);
        }
    }
}
