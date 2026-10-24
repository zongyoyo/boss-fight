using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BossFight.Core
{
    /// <summary>音量的種類。</summary>
    public enum VolumeChannel
    {
        /// <summary>主音量：AudioListener.volume，所有聲音。</summary>
        Master,
        /// <summary>戰鬥音樂。</summary>
        Music,
        /// <summary>戰鬥音效、演出的 Timeline 音軌、選單試聽。</summary>
        Sfx,
    }

    /// <summary>
    /// 玩家設定（ESC 選單的「設定」與「難度」）：音量（主音量、音樂、音效，0..1）、開發者模式、改鍵、難度。存在 PlayerPrefs，重試（重新載入場景）與下次啟動都會套用；
    /// 沒有存檔時用 GameOptionsData 的預設值。改鍵存的是 Project-wide Input Actions（InputSystem.actions）的 binding override JSON，Awake 時套回去。
    /// 這裡只放設定值與存檔，怎麼套用由訂閱方決定：音量是 AudioManager，開發者模式是 Debug 的 DeveloperMode，
    /// 難度是 BattleManager（Awake 時讀 Difficulty，交給玩家與 Boss；戰鬥中改了要重新挑戰才生效）。
    /// Start 時把目前的值各送一次 OnVolumeChanged／OnDeveloperModeChanged／OnDifficultyChanged，訂閱方在 OnEnable 訂閱就會拿到初始值，不用另外讀欄位。
    /// DefaultExecutionOrder(-100)：Awake 比 BattleManager 早，讀得到存檔的難度。
    /// </summary>
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    public class GameOptions : MonoBehaviour
    {
        const string KeyPrefix = "BossFight.Options.";
        const string BindingsKey = KeyPrefix + "Bindings";
        const string DeveloperModeKey = KeyPrefix + "DeveloperMode";
        const string DifficultyKey = KeyPrefix + "Difficulty";

        [Tooltip("沒有存檔時的預設值")]
        [SerializeField] GameOptionsData data;

        /// <summary>某一種音量改變（Start 時每種也各送一次初始值）。</summary>
        public event Action<VolumeChannel, float> OnVolumeChanged;
        /// <summary>開發者模式開關改變（Start 時也送一次初始值）。</summary>
        public event Action<bool> OnDeveloperModeChanged;
        /// <summary>難度改變（Start 時也送一次初始值）。</summary>
        public event Action<Difficulty> OnDifficultyChanged;

        public bool DeveloperMode { get; private set; }
        /// <summary>存檔的難度；這一場套用的是 BattleManager Awake 時讀到的值。</summary>
        public Difficulty Difficulty { get; private set; }

        static readonly VolumeChannel[] Channels = (VolumeChannel[])Enum.GetValues(typeof(VolumeChannel));
        readonly float[] volumes = new float[Channels.Length];

        public float GetVolume(VolumeChannel channel) => volumes[(int)channel];

        void Awake()
        {
            foreach (var channel in Channels)
                volumes[(int)channel] = Mathf.Clamp01(PlayerPrefs.GetFloat(VolumeKey(channel), DefaultVolume(channel)));
            DeveloperMode = PlayerPrefs.GetInt(DeveloperModeKey, data != null && data.defaultDeveloperMode ? 1 : 0) != 0;
            int difficulty = PlayerPrefs.GetInt(DifficultyKey, (int)(data != null ? data.defaultDifficulty : Difficulty.Hard));
            Difficulty = Enum.IsDefined(typeof(Difficulty), difficulty) ? (Difficulty)difficulty : Difficulty.Hard;
            LoadBindings();
        }

        void Start()
        {
            foreach (var channel in Channels) OnVolumeChanged?.Invoke(channel, volumes[(int)channel]);
            OnDeveloperModeChanged?.Invoke(DeveloperMode);
            OnDifficultyChanged?.Invoke(Difficulty);
        }

        void OnDisable() => PlayerPrefs.Save();

        public void SetVolume(VolumeChannel channel, float value)
        {
            value = Mathf.Clamp01(value);
            if (Mathf.Approximately(volumes[(int)channel], value)) return;
            volumes[(int)channel] = value;
            PlayerPrefs.SetFloat(VolumeKey(channel), value);
            OnVolumeChanged?.Invoke(channel, value);
        }

        public void SetDeveloperMode(bool on)
        {
            if (DeveloperMode == on) return;
            DeveloperMode = on;
            PlayerPrefs.SetInt(DeveloperModeKey, on ? 1 : 0);
            OnDeveloperModeChanged?.Invoke(on);
        }

        /// <summary>改難度並立刻存檔；這一場不會變，要重新挑戰（重新載入場景）才套用。</summary>
        public void SetDifficulty(Difficulty difficulty)
        {
            if (Difficulty == difficulty) return;
            Difficulty = difficulty;
            PlayerPrefs.SetInt(DifficultyKey, (int)difficulty);
            PlayerPrefs.Save();
            OnDifficultyChanged?.Invoke(difficulty);
        }

        /// <summary>把 InputSystem.actions 目前的 binding override 存起來（改鍵、恢復預設之後呼叫）。</summary>
        public void SaveBindings()
        {
            var actions = InputSystem.actions;
            if (actions == null) return;
            PlayerPrefs.SetString(BindingsKey, actions.SaveBindingOverridesAsJson());
            PlayerPrefs.Save();
        }

        // 沒有存檔時也清掉 override：Editor 裡 Project-wide Actions 是同一份資產，上一次 Play 的改鍵可能還留在記憶體
        static void LoadBindings()
        {
            var actions = InputSystem.actions;
            if (actions == null) return;
            string json = PlayerPrefs.GetString(BindingsKey, string.Empty);
            if (string.IsNullOrEmpty(json)) actions.RemoveAllBindingOverrides();
            else actions.LoadBindingOverridesFromJson(json);
        }

        float DefaultVolume(VolumeChannel channel)
        {
            if (data == null) return 1f;
            switch (channel)
            {
                case VolumeChannel.Music: return data.defaultMusicVolume;
                case VolumeChannel.Sfx: return data.defaultSfxVolume;
                default: return data.defaultMasterVolume;
            }
        }

        static string VolumeKey(VolumeChannel channel) => KeyPrefix + "Volume." + channel;
    }
}
