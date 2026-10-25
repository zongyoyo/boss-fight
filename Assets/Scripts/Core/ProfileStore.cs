using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace BossFight.Core
{
    /// <summary>一個角色的穿戴（T22）：slots 依 CharacterDefinition.equipmentSlots 的順序放裝備 id，空字串 = 沒穿。</summary>
    [Serializable]
    public class CharacterEquipmentSave
    {
        [Tooltip("角色 id")]
        public string character = "";
        [Tooltip("每一格的裝備 id（照角色的欄位順序）；空字串 = 沒穿")]
        public List<string> slots = new List<string>();
    }

    /// <summary>存檔的內容（save.json）。只放純資料：id 字串、數字，不放 SO 或元件引用。之後的任務在這裡加欄位（技能點、任務…）；只加欄位時版本不用改（舊檔讀到的是空清單）。</summary>
    [Serializable]
    public class ProfileData
    {
        /// <summary>目前的存檔版本；之後改格式時加一，讀到舊版本再轉換。</summary>
        public const int CurrentVersion = 1;

        [Tooltip("存檔版本")]
        public int version = CurrentVersion;
        [Tooltip("擊敗過的 Boss（id）")]
        public List<string> defeatedBosses = new List<string>();
        [Tooltip("目前的角色（CharacterDefinition 的 id）；空字串 = 名冊的預設角色")]
        public string currentCharacter = "";
        [Tooltip("之後解鎖的角色（id）；一開始就能用的角色（CharacterDefinition.unlockedByDefault）不記在這裡")]
        public List<string> unlockedCharacters = new List<string>();
        [Tooltip("解鎖的裝備（id）")]
        public List<string> unlockedEquipment = new List<string>();
        [Tooltip("解鎖後還沒在鐵匠鋪看過的裝備（id，「新」標記）")]
        public List<string> newEquipment = new List<string>();
        [Tooltip("各角色的穿戴（換角色時各自保留）")]
        public List<CharacterEquipmentSave> characterEquipment = new List<CharacterEquipmentSave>();
    }

    /// <summary>
    /// 存檔：Application.persistentDataPath/save.json（JsonUtility）。有變動就存，離開遊戲（Application.quitting）再存一次。
    /// 讀檔失敗（壞檔、不是 JSON）時把壞檔改名成 save.bak.json（覆蓋舊的備份），開新檔並在 Console 警告。
    /// 第一次讀到時才載入；進 Play 模式時清掉載入狀態，關掉 Domain Reload 時也會重新從檔案讀。
    /// 另外放「這次要在哪個出生點出現」（SetArrival／TakeArrival）：只活在這次執行，不寫進檔案；SceneFlow 換場景前設、新場景的 PlayerSpawner 先看（PeekArrival，決定生成位置）、SceneFlow 再取走（決定要不要淡入）。
    /// 這是 CLAUDE.md「不用 static」規則的第二個例外（第一個是 BattleSession）：存檔要跨場景、跨執行，只放純資料，不放任何元件引用。
    /// 玩家設定（音量、改鍵、難度）照舊由 GameOptions 存 PlayerPrefs，跟這裡分開。
    /// </summary>
    public static class ProfileStore
    {
        const string FileName = "save.json";
        const string BackupName = "save.bak.json";

        static ProfileData data;
        static bool hasArrival;
        static string arrival;

        /// <summary>存檔的完整路徑。</summary>
        public static string SavePath => Path.Combine(Application.persistentDataPath, FileName);
        /// <summary>壞檔備份的完整路徑。</summary>
        public static string BackupPath => Path.Combine(Application.persistentDataPath, BackupName);

        static ProfileData Data
        {
            get
            {
                if (data == null) Load();
                return data;
            }
        }

        // ---------- Boss ----------

        /// <summary>擊敗過這個 Boss 沒有。</summary>
        public static bool HasDefeated(string bossId) => !string.IsNullOrEmpty(bossId) && Data.defeatedBosses.Contains(bossId);

        /// <summary>記錄擊敗 Boss 並存檔；回傳 true = 第一次擊敗（首通）。</summary>
        public static bool RecordBossDefeated(string bossId)
        {
            if (string.IsNullOrEmpty(bossId) || Data.defeatedBosses.Contains(bossId)) return false;
            Data.defeatedBosses.Add(bossId);
            Save();
            return true;
        }

        // ---------- 角色 ----------

        /// <summary>目前的角色 id；空字串 = 用名冊的預設角色（PlayerSpawner 依它生成）。</summary>
        public static string CurrentCharacter => Data.currentCharacter ?? string.Empty;

        /// <summary>這個角色是不是解鎖過（只看存檔；一開始就能用的角色另外看 CharacterDefinition.unlockedByDefault）。</summary>
        public static bool IsCharacterUnlocked(string id) => !string.IsNullOrEmpty(id) && Data.unlockedCharacters.Contains(id);

        /// <summary>解鎖角色並存檔（之後的獎勵用）；回傳 true = 這次才解鎖。</summary>
        public static bool UnlockCharacter(string id)
        {
            if (string.IsNullOrEmpty(id) || Data.unlockedCharacters.Contains(id)) return false;
            Data.unlockedCharacters.Add(id);
            Save();
            return true;
        }

        /// <summary>設定目前的角色並存檔（T21 營火切換角色用）。</summary>
        public static void SetCurrentCharacter(string id)
        {
            id = id ?? string.Empty;
            if (Data.currentCharacter == id) return;
            Data.currentCharacter = id;
            Save();
        }

        // ---------- 裝備 ----------

        /// <summary>這件裝備解鎖了沒有。</summary>
        public static bool IsEquipmentUnlocked(string id) => !string.IsNullOrEmpty(id) && Data.unlockedEquipment.Contains(id);

        /// <summary>解鎖裝備並標成「新」、存檔（獎勵、Debug 的解鎖全部）；回傳 true = 這次才解鎖。</summary>
        public static bool UnlockEquipment(string id)
        {
            if (string.IsNullOrEmpty(id) || Data.unlockedEquipment.Contains(id)) return false;
            Data.unlockedEquipment.Add(id);
            if (!Data.newEquipment.Contains(id)) Data.newEquipment.Add(id);
            Save();
            return true;
        }

        /// <summary>這件裝備有沒有「新」標記（解鎖後還沒在鐵匠鋪看過）。</summary>
        public static bool IsEquipmentNew(string id) => !string.IsNullOrEmpty(id) && Data.newEquipment.Contains(id);

        /// <summary>清掉所有「新」標記並存檔（關掉鐵匠鋪時）；回傳 true = 有清掉東西。</summary>
        public static bool ClearNewEquipment()
        {
            if (Data.newEquipment.Count == 0) return false;
            Data.newEquipment.Clear();
            Save();
            return true;
        }

        /// <summary>這個角色第 slot 格穿的裝備 id；沒穿或沒有記錄時空字串（不檢查解鎖與欄位種類，那是 Progression 的事）。</summary>
        public static string GetEquipped(string character, int slot)
        {
            var record = FindEquipment(character);
            if (record == null || slot < 0 || slot >= record.slots.Count) return string.Empty;
            return record.slots[slot] ?? string.Empty;
        }

        /// <summary>設定這個角色第 slot 格穿的裝備（空字串 = 卸下）並存檔。</summary>
        public static void SetEquipped(string character, int slot, string equipment)
        {
            if (string.IsNullOrEmpty(character) || slot < 0) return;
            equipment = equipment ?? string.Empty;
            var record = FindEquipment(character);
            if (record == null)
            {
                if (equipment.Length == 0) return;
                record = new CharacterEquipmentSave { character = character };
                Data.characterEquipment.Add(record);
            }
            while (record.slots.Count <= slot) record.slots.Add(string.Empty);
            if (record.slots[slot] == equipment) return;
            record.slots[slot] = equipment;
            Save();
        }

        static CharacterEquipmentSave FindEquipment(string character)
        {
            if (string.IsNullOrEmpty(character)) return null;
            foreach (var record in Data.characterEquipment)
            {
                if (record != null && record.character == character) return record;
            }
            return null;
        }

        // ---------- 整份存檔 ----------

        /// <summary>清空存檔（開發者模式的「重置存檔」）並寫檔。</summary>
        public static void ResetProfile()
        {
            data = new ProfileData();
            Save();
            Debug.Log($"[ProfileStore] 存檔已重置：{SavePath}");
        }

        /// <summary>寫進 save.json：先寫暫存檔再取代，寫到一半當掉不會留下半個檔案。</summary>
        public static void Save()
        {
            if (data == null) return; // 還沒讀過就沒有變動
            data.version = ProfileData.CurrentVersion;
            string path = SavePath;
            string temp = path + ".tmp";
            try
            {
                File.WriteAllText(temp, JsonUtility.ToJson(data, true));
                File.Copy(temp, path, true);
                File.Delete(temp);
            }
            catch (Exception e)
            {
                Debug.LogError($"[ProfileStore] 存檔失敗：{path}\n{e.Message}");
            }
        }

        static void Load()
        {
            string path = SavePath;
            if (!File.Exists(path))
            {
                data = new ProfileData();
                return;
            }
            try
            {
                var loaded = JsonUtility.FromJson<ProfileData>(File.ReadAllText(path));
                if (loaded == null) throw new FormatException("檔案是空的");
                if (loaded.defeatedBosses == null) loaded.defeatedBosses = new List<string>();
                if (loaded.currentCharacter == null) loaded.currentCharacter = string.Empty;
                if (loaded.unlockedCharacters == null) loaded.unlockedCharacters = new List<string>();
                if (loaded.unlockedEquipment == null) loaded.unlockedEquipment = new List<string>();
                if (loaded.newEquipment == null) loaded.newEquipment = new List<string>();
                if (loaded.characterEquipment == null) loaded.characterEquipment = new List<CharacterEquipmentSave>();
                loaded.characterEquipment.RemoveAll(r => r == null);
                foreach (var record in loaded.characterEquipment)
                {
                    if (record.slots == null) record.slots = new List<string>();
                }
                data = loaded;
            }
            catch (Exception e)
            {
                string backup = BackupPath;
                try
                {
                    File.Copy(path, backup, true);
                    File.Delete(path);
                }
                catch (Exception moveError)
                {
                    Debug.LogError($"[ProfileStore] 壞檔備份失敗：{moveError.Message}");
                }
                Debug.LogWarning($"[ProfileStore] 存檔讀取失敗（{e.Message}），壞檔已改名成 {backup}，開新的存檔。");
                data = new ProfileData();
                Save();
            }
        }

        // ---------- 這次的出生點（不存檔） ----------

        /// <summary>換場景前設定：新場景要在哪個出生點出現（id；空字串 = 場景預設的位置）。</summary>
        public static void SetArrival(string id)
        {
            hasArrival = true;
            arrival = id ?? string.Empty;
        }

        /// <summary>看這次的出生點但不取走（PlayerSpawner 生成角色用）；回傳 false = 不是換場景進來的。</summary>
        public static bool PeekArrival(out string id)
        {
            id = arrival;
            return hasArrival;
        }

        /// <summary>新場景取走出生點；回傳 false = 不是換場景進來的（直接進 Play、重試）。取走後清掉。</summary>
        public static bool TakeArrival(out string id)
        {
            id = arrival;
            bool had = hasArrival;
            hasArrival = false;
            arrival = null;
            return had;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnLaunch()
        {
            data = null; // 下次讀到時重新從檔案載入
            hasArrival = false;
            arrival = null;
            Application.quitting -= Save;
            Application.quitting += Save;
        }
    }
}
