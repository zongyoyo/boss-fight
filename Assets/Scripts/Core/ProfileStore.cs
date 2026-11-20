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

    /// <summary>一個角色學過的技能（T23）：節點 id 與學這些節點實際付的技能點（重置時照這個退回）。</summary>
    [Serializable]
    public class CharacterSkillsSave
    {
        [Tooltip("角色 id")]
        public string character = "";
        [Tooltip("學過的節點 id（SkillTreeData 裡的節點）")]
        public List<string> nodes = new List<string>();
        [Tooltip("學這些節點總共付的技能點（重置時全部退回池子）")]
        public int spent;
    }

    /// <summary>存檔的內容（save.json）。只放純資料：id 字串、數字，不放 SO 或元件引用。只加欄位時版本不用改（舊檔讀到的是空清單）。</summary>
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
        [Tooltip("技能點池（全帳號共用，T23）")]
        public int skillPoints;
        [Tooltip("各角色學過的技能（分開記）")]
        public List<CharacterSkillsSave> characterSkills = new List<CharacterSkillsSave>();
        [Tooltip("看過休息區的開場劇情沒有（T24：村長第一次走過來講話）")]
        public bool storyIntroSeen;
        [Tooltip("序章結局演出已完成；中途離開及開發者略過不會設定")]
        public bool prologueDone;
        [Tooltip("演出正常結束時一起保存的序章結果")] public PrologueResult prologueResult;
        [Tooltip("接過、還沒回報的任務（QuestData 的 id）")]
        public List<string> acceptedQuests = new List<string>();
        [Tooltip("回報完的任務（id）")]
        public List<string> completedQuests = new List<string>();
        [Tooltip("傳送門清單播過解鎖演出（鎖鏈碎裂）的 Boss（id，T34）")]
        public List<string> portalUnlocksSeen = new List<string>();
        [Tooltip("最後一次存檔的本機時間（ISO 8601，標題畫面的存檔欄位顯示，T31）")]
        public string lastSaved = "";
    }

    /// <summary>一個存檔欄位的摘要（T31，標題畫面顯示用）：不切換欄位、不改檔案，直接讀那一格的檔案。</summary>
    public class ProfileSlotInfo
    {
        /// <summary>第幾格（0 起算）。</summary>
        public int slot;
        /// <summary>這一格有存檔檔案。</summary>
        public bool exists;
        /// <summary>檔案在，但讀不出來（壞檔；真的載入時才會備份並開新檔）。</summary>
        public bool corrupt;
        /// <summary>讀到的內容；沒有檔案或壞檔時 null。</summary>
        public ProfileData data;
        /// <summary>最後存檔時間；沒有記錄（T31 以前的存檔）時 null。</summary>
        public DateTime? lastSaved;

        /// <summary>空欄位：沒有檔案（壞檔不算空，載入會開新檔）。</summary>
        public bool IsEmpty => !exists;
    }

    /// <summary>
    /// 存檔（T31 起四個欄位）：Application.persistentDataPath/save_slot1..4.json（JsonUtility）。有變動就存（目前欄位），離開遊戲（Application.quitting）再存一次。
    /// 編輯器另用 save_editor_slot1..4.json：跟同一台電腦上的發布版分開，編輯器測試不會出現在打包版的欄位裡。
    /// 舊的單一存檔：編輯器第一次用到欄位時有 save_editor.json，沒有任何編輯器欄位就搬成欄位 1，已經有欄位就改名成 save_editor.legacy.json 留著；
    /// 打包版不遷移舊的 save.json（使用者決定：打包版四格一開始都是空的），舊檔留在原地不動。
    /// 目前欄位（ActiveSlot）由標題畫面 SelectSlot／StartNewProfile 決定，記在 PlayerPrefs（BossFight.Profile.LastSlot）；
    /// 編輯器直接在休息區、Boss 戰、序章按 Play 時用上次的欄位（沒有記錄 = 欄位 1）。
    /// 讀檔失敗（壞檔、不是 JSON）時把壞檔改名成那一格的 .bak.json（覆蓋舊的備份），開新檔並在 Console 警告。
    /// 第一次讀到時才載入；進 Play 模式時清掉載入狀態，關掉 Domain Reload 時也會重新從檔案讀。
    /// 另外放「這次要在哪個出生點出現」（SetArrival／TakeArrival）：只活在這次執行，不寫進檔案；SceneFlow 換場景前設、新場景的 PlayerSpawner 先看（PeekArrival，決定生成位置）、SceneFlow 再取走（決定要不要淡入）。
    /// 這是 CLAUDE.md「不用 static」規則的第二個例外（第一個是 BattleSession）：存檔要跨場景、跨執行，只放純資料，不放任何元件引用。
    /// 玩家設定（音量、改鍵、難度）照舊由 GameOptions 存 PlayerPrefs，跟這裡分開。
    /// </summary>
    public static class ProfileStore
    {
        /// <summary>存檔欄位的數量（標題畫面列出這麼多格）。</summary>
        public const int SlotCount = 4;
        const string LastSlotKey = "BossFight.Profile.LastSlot";
#if UNITY_EDITOR
        const string SlotPrefix = "save_editor_slot";
        const string LegacyFileName = "save_editor.json"; // T31 以前編輯器的單一存檔，搬成欄位 1
        const string LegacyKeptName = "save_editor.legacy.json"; // 已經有欄位時，舊檔改名留著
#else
        const string SlotPrefix = "save_slot";
#endif

        static ProfileData data;
        static int activeSlot = -1; // -1 = 還沒決定，第一次用到時讀 PlayerPrefs
        static bool migrated;
        static bool hasArrival;
        static string arrival;

        /// <summary>目前的存檔欄位（0 起算）。</summary>
        public static int ActiveSlot
        {
            get
            {
                if (activeSlot < 0) activeSlot = Mathf.Clamp(PlayerPrefs.GetInt(LastSlotKey, 0), 0, SlotCount - 1);
                return activeSlot;
            }
        }

        /// <summary>序章是否完成；舊存檔沒有這個欄位時為 false。</summary>
        public static bool PrologueDone => Data.prologueDone;
        public static PrologueResult PrologueResult => Data.prologueResult;

        /// <summary>純資料快照計算；不改來源、不寫檔，Editor 可用獨立資料驗證去重。</summary>
        public static bool PreparePrologueCompletion(ProfileData source, PrologueResult result, string bossId,
            int skillPoints, string[] equipmentIds, string[] characterIds, out ProfileData next, out bool firstClear)
        {
            next = null; firstClear = false;
            if (source == null || source.prologueDone || result == Core.PrologueResult.None ||
                (result == Core.PrologueResult.Victory && string.IsNullOrEmpty(bossId))) return false;
            next = JsonUtility.FromJson<ProfileData>(JsonUtility.ToJson(source));
            next.prologueDone = true; next.prologueResult = result;
            firstClear = result == Core.PrologueResult.Victory && !next.defeatedBosses.Contains(bossId);
            if (!firstClear) return true;
            next.defeatedBosses.Add(bossId);
            next.skillPoints = checked(next.skillPoints + Math.Max(0, skillPoints));
            if (equipmentIds != null) foreach (string id in equipmentIds)
                if (!string.IsNullOrEmpty(id) && !next.unlockedEquipment.Contains(id))
                { next.unlockedEquipment.Add(id); if (!next.newEquipment.Contains(id)) next.newEquipment.Add(id); }
            if (characterIds != null) foreach (string id in characterIds)
                if (!string.IsNullOrEmpty(id) && !next.unlockedCharacters.Contains(id)) next.unlockedCharacters.Add(id);
            return true;
        }

        /// <summary>唯一的序章完成提交：一次替換檔案成功後才採用快照；退出時 Save 不會寫入待領獎勵。</summary>
        public static bool TryCompletePrologue(PrologueResult result, string bossId, int skillPoints,
            string[] equipmentIds, string[] characterIds, out bool firstClear)
        {
            firstClear = false;
            try
            {
                if (!PreparePrologueCompletion(Data, result, bossId, skillPoints, equipmentIds, characterIds, out var next, out bool first)) return false;
                next.lastSaved = DateTime.Now.ToString("o");
                string path = SavePath, temp = path + ".prologue.tmp";
                File.WriteAllText(temp, JsonUtility.ToJson(next, true));
                if (File.Exists(path)) File.Replace(temp, path, null);
                else File.Move(temp, path);
                data = next; firstClear = first; return true;
            }
            catch (Exception e) { Debug.LogError("[ProfileStore] 序章完成保存失敗，未採用結算快照：" + e.Message); return false; }
        }

        /// <summary>目前欄位的存檔完整路徑。</summary>
        public static string SavePath => SlotPath(ActiveSlot);
        /// <summary>目前欄位的壞檔備份完整路徑。</summary>
        public static string BackupPath => SlotBackupPath(ActiveSlot);

        /// <summary>某一格的存檔完整路徑。</summary>
        public static string SlotPath(int slot) => Path.Combine(Application.persistentDataPath, $"{SlotPrefix}{slot + 1}.json");
        /// <summary>某一格的壞檔備份完整路徑。</summary>
        public static string SlotBackupPath(int slot) => Path.Combine(Application.persistentDataPath, $"{SlotPrefix}{slot + 1}.bak.json");

        // ---------- 存檔欄位（T31） ----------

        /// <summary>讀某一格的摘要（不切換欄位、不改檔案）。slot 超出範圍時回傳空欄位。</summary>
        public static ProfileSlotInfo PeekSlot(int slot)
        {
            MigrateLegacy();
            var info = new ProfileSlotInfo { slot = slot };
            if (slot < 0 || slot >= SlotCount) return info;
            // 目前欄位已經載入時以記憶體裡的為準（還沒寫進檔案的變動也算）
            if (slot == ActiveSlot && data != null && File.Exists(SlotPath(slot)))
            {
                info.exists = true;
                info.data = data;
            }
            else
            {
                string path = SlotPath(slot);
                if (!File.Exists(path)) return info;
                info.exists = true;
                try
                {
                    info.data = JsonUtility.FromJson<ProfileData>(File.ReadAllText(path));
                    if (info.data == null) info.corrupt = true;
                    else Normalize(info.data);
                }
                catch (Exception)
                {
                    info.corrupt = true;
                    info.data = null;
                }
            }
            if (info.data != null && DateTime.TryParse(info.data.lastSaved, null, System.Globalization.DateTimeStyles.RoundtripKind, out var time))
                info.lastSaved = time;
            return info;
        }

        /// <summary>切換到某一格（載入存檔用；之後第一次讀到時才從檔案載入），記成上次的欄位。</summary>
        public static void SelectSlot(int slot)
        {
            if (slot < 0 || slot >= SlotCount) return;
            MigrateLegacy();
            if (data != null && slot != ActiveSlot) Save(); // 先把目前這一格寫回去
            activeSlot = slot;
            data = null;
            PlayerPrefs.SetInt(LastSlotKey, slot);
            PlayerPrefs.Save();
        }

        /// <summary>在某一格開新存檔（標題畫面的「新遊戲」）：切過去、清空並寫檔。</summary>
        public static void StartNewProfile(int slot)
        {
            if (slot < 0 || slot >= SlotCount) return;
            SelectSlot(slot);
            data = new ProfileData();
            Save();
            Debug.Log($"[ProfileStore] 欄位 {slot + 1} 開新存檔：{SavePath}");
        }

        /// <summary>刪除某一格的存檔（含暫存檔，壞檔備份留著）；刪的是目前欄位時清掉已載入的內容。回傳 true = 真的刪了檔案。</summary>
        public static bool DeleteSlot(int slot)
        {
            if (slot < 0 || slot >= SlotCount) return false;
            MigrateLegacy();
            string path = SlotPath(slot);
            bool existed = File.Exists(path);
            try
            {
                if (existed) File.Delete(path);
                if (File.Exists(path + ".tmp")) File.Delete(path + ".tmp");
            }
            catch (Exception e)
            {
                Debug.LogError($"[ProfileStore] 刪除欄位 {slot + 1} 失敗：{e.Message}");
                return false;
            }
            if (slot == ActiveSlot) data = null;
            if (existed) Debug.Log($"[ProfileStore] 欄位 {slot + 1} 已刪除：{path}");
            return existed;
        }

        // 編輯器：有 T31 以前的 save_editor.json 時，還沒有任何編輯器欄位就搬成欄位 1；已經有欄位就改名成 save_editor.legacy.json 留著
        // （都搬走，之後刪光欄位也不會再把舊檔搬回來）。打包版不遷移。
        static void MigrateLegacy()
        {
            if (migrated) return;
            migrated = true;
#if UNITY_EDITOR
            string legacy = Path.Combine(Application.persistentDataPath, LegacyFileName);
            if (!File.Exists(legacy)) return;
            bool anySlot = false;
            for (int i = 0; i < SlotCount; i++) anySlot |= File.Exists(SlotPath(i));
            try
            {
                if (anySlot)
                {
                    string kept = Path.Combine(Application.persistentDataPath, LegacyKeptName);
                    if (File.Exists(kept)) File.Delete(kept);
                    File.Move(legacy, kept);
                    Debug.Log($"[ProfileStore] 已經有存檔欄位，舊的編輯器存檔改名留著、不再讀：{kept}");
                }
                else
                {
                    File.Move(legacy, SlotPath(0));
                    Debug.Log($"[ProfileStore] 舊的編輯器存檔已搬成欄位 1：{SlotPath(0)}");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ProfileStore] 搬移舊的編輯器存檔失敗：{e.Message}");
            }
#endif
        }

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

        // ---------- 技能 ----------

        /// <summary>技能點池剩下的點數（全帳號共用）。</summary>
        public static int SkillPoints => Data.skillPoints;

        /// <summary>技能點加進池子並存檔（獎勵、Debug 的「給 10 技能點」）；amount ≤ 0 時不做事。</summary>
        public static void AddSkillPoints(int amount)
        {
            if (amount <= 0) return;
            Data.skillPoints += amount;
            Save();
        }

        /// <summary>這個角色學過這個節點沒有（不檢查節點還在不在樹裡，那是 Progression 的事）。</summary>
        public static bool IsSkillLearned(string character, string node)
        {
            var record = FindSkills(character);
            return record != null && !string.IsNullOrEmpty(node) && record.nodes.Contains(node);
        }

        /// <summary>這個角色學過的技能點總數（重置會退回的量）。</summary>
        public static int SpentSkillPoints(string character)
        {
            var record = FindSkills(character);
            return record != null ? record.spent : 0;
        }

        /// <summary>這個角色學會一個節點：從池子扣 cost 並存檔。已經學過或點數不夠時不做事、回傳 false（前置條件由 Progression 檢查）。</summary>
        public static bool LearnSkill(string character, string node, int cost)
        {
            if (string.IsNullOrEmpty(character) || string.IsNullOrEmpty(node)) return false;
            cost = Math.Max(0, cost);
            if (IsSkillLearned(character, node) || Data.skillPoints < cost) return false;
            var record = FindSkills(character);
            if (record == null)
            {
                record = new CharacterSkillsSave { character = character };
                Data.characterSkills.Add(record);
            }
            record.nodes.Add(node);
            record.spent += cost;
            Data.skillPoints -= cost;
            Save();
            return true;
        }

        /// <summary>重置這個角色的技能：清掉學過的節點，付過的點數全部退回池子並存檔；回傳退回的點數。</summary>
        public static int ResetSkills(string character)
        {
            var record = FindSkills(character);
            if (record == null || (record.nodes.Count == 0 && record.spent == 0)) return 0;
            int refund = Math.Max(0, record.spent);
            Data.skillPoints += refund;
            Data.characterSkills.Remove(record);
            Save();
            return refund;
        }

        static CharacterSkillsSave FindSkills(string character)
        {
            if (string.IsNullOrEmpty(character)) return null;
            foreach (var record in Data.characterSkills)
            {
                if (record != null && record.character == character) return record;
            }
            return null;
        }

        // ---------- 劇情與任務（T24） ----------

        /// <summary>看過休息區的開場劇情沒有。</summary>
        public static bool StoryIntroSeen => Data.storyIntroSeen;

        /// <summary>記住看過開場劇情並存檔。</summary>
        public static void MarkStoryIntroSeen()
        {
            if (Data.storyIntroSeen) return;
            Data.storyIntroSeen = true;
            Save();
        }

        /// <summary>接過這個任務、還沒回報。</summary>
        public static bool IsQuestAccepted(string id) => !string.IsNullOrEmpty(id) && Data.acceptedQuests.Contains(id);

        /// <summary>回報完這個任務。</summary>
        public static bool IsQuestCompleted(string id) => !string.IsNullOrEmpty(id) && Data.completedQuests.Contains(id);

        /// <summary>接任務並存檔；已經接過或完成時不做事、回傳 false（能不能接由 Progression 判斷）。</summary>
        public static bool AcceptQuest(string id)
        {
            if (string.IsNullOrEmpty(id) || IsQuestAccepted(id) || IsQuestCompleted(id)) return false;
            Data.acceptedQuests.Add(id);
            Save();
            return true;
        }

        /// <summary>回報任務：從「已接」移到「已完成」並存檔；沒接過時不做事、回傳 false（目標達成沒有由 Progression 判斷）。</summary>
        public static bool CompleteQuest(string id)
        {
            if (!IsQuestAccepted(id)) return false;
            Data.acceptedQuests.Remove(id);
            if (!Data.completedQuests.Contains(id)) Data.completedQuests.Add(id);
            Save();
            return true;
        }

        // ---------- 傳送門（T34） ----------

        /// <summary>傳送門清單播過這隻 Boss 的解鎖演出。</summary>
        public static bool IsPortalUnlockSeen(string bossId) => !string.IsNullOrEmpty(bossId) && Data.portalUnlocksSeen.Contains(bossId);

        /// <summary>記住播過解鎖演出並存檔；已經記過時不做事、回傳 false。</summary>
        public static bool MarkPortalUnlockSeen(string bossId)
        {
            if (string.IsNullOrEmpty(bossId) || IsPortalUnlockSeen(bossId)) return false;
            Data.portalUnlocksSeen.Add(bossId);
            Save();
            return true;
        }

        // ---------- 整份存檔 ----------

        /// <summary>清空目前欄位的存檔（ESC 選單的「重置存檔」）並寫檔。</summary>
        public static void ResetProfile()
        {
            data = new ProfileData();
            Save();
            Debug.Log($"[ProfileStore] 欄位 {ActiveSlot + 1} 的存檔已重置：{SavePath}");
        }

        /// <summary>寫進目前欄位的檔案：先寫暫存檔再取代，寫到一半當掉不會留下半個檔案。</summary>
        public static void Save()
        {
            if (data == null) return; // 還沒讀過就沒有變動
            data.version = ProfileData.CurrentVersion;
            data.lastSaved = DateTime.Now.ToString("o");
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
            MigrateLegacy();
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
                data = Normalize(loaded);
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
                Debug.LogWarning($"[ProfileStore] 欄位 {ActiveSlot + 1} 存檔讀取失敗（{e.Message}），壞檔已改名成 {backup}，開新的存檔。");
                data = new ProfileData();
                Save();
            }
        }

        // 舊檔缺的欄位補成空的（JsonUtility 讀不到的清單是 null）
        static ProfileData Normalize(ProfileData loaded)
        {
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
            if (loaded.characterSkills == null) loaded.characterSkills = new List<CharacterSkillsSave>();
            loaded.characterSkills.RemoveAll(r => r == null);
            foreach (var record in loaded.characterSkills)
            {
                if (record.nodes == null) record.nodes = new List<string>();
            }
            if (loaded.acceptedQuests == null) loaded.acceptedQuests = new List<string>();
            if (loaded.completedQuests == null) loaded.completedQuests = new List<string>();
            if (loaded.portalUnlocksSeen == null) loaded.portalUnlocksSeen = new List<string>();
            if (loaded.lastSaved == null) loaded.lastSaved = string.Empty;
            return loaded;
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
            activeSlot = -1; // 重新讀上次的欄位
            migrated = false;
            hasArrival = false;
            arrival = null;
            Application.quitting -= Save;
            Application.quitting += Save;
        }
    }
}
