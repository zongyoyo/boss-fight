using System;
using System.Collections.Generic;
using System.IO;
using BossFight.Battle;
using BossFight.Player;
using BossFight.Progression;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BossFight.EditorTools
{
    /// <summary>
    /// Tools > Progression > Build Game Database：建立角色與數值管線的資產（T19），可重複執行。
    /// 1. 刀盾狗的基礎數值：T18 以前的 ScriptableObjects/Player/PlayerStats.asset 用 AssetDatabase.MoveAsset 搬成
    ///    ScriptableObjects/Progression/Characters/DaoDunDogStats.asset（GUID 不變，prefab 與鏡頭的引用不會斷）。
    /// 2. 資產（沒有才建，已存在的只補空欄位）：Characters/DaoDunDog.asset（CharacterDefinition）、CharacterRoster.asset、GameDatabase.asset。
    ///    測試狗（T21）：Characters/TestDog.asset（developerOnly）與 TestDogStats.asset（建立時複製刀盾狗的數值，改成 testDogMaxHealth、testDogMoveSpeed），
    ///    prefab 是 DaoDunDogBuilder.BuildTestVariant 的 variant（沒有時建立）；排在名冊第 2 個。
    ///    裝備（T22，Equipment/）：欄位種類 Slot_Collar（項圈）、Slot_Amulet（護符），厚皮項圈 ThickCollar（最大血量 +20）、
    ///    反擊護符 CounterAmulet（效果 ParryHeal：盾反成功回 5 HP），程序產生的佔位圖示（Models/UI/Equipment，沒有才產生）；
    ///    角色的裝備欄空著時填上（刀盾狗：項圈、護符；測試狗：護符），裝備加進 GameDatabase.equipment。數值只在建立時寫入，之後在資產調。
    ///    技能樹（T23，SkillTrees/）：刀盾狗 DaoDunDogTree（企劃書的 5 個節點，共 7 點）、測試狗 TestDogTree（疾風、厚毛 2 個節點）；
    ///    節點只在樹是空的時候寫入（之後在資產調），角色的 skillTree 空著時接上。
    ///    Boss 與任務（T24）：Bosses/OiiaCat.asset（id oiia_cat，首通：技能點 ×3、厚皮項圈）、Quests/SpinTheCat.asset（「轉暈那隻小笨貓!」：擊敗 OIIA 貓，
    ///    獎勵 技能點 ×2、反擊護符；對白是佔位文字），加進 GameDatabase.bosses／quests；BossArena 的 BattleSettings.boss 空著時接上 OIIA 貓。
    ///    獎勵與對白只在建立時寫入，之後在資產調。
    /// 3. 檢查 id（GameDatabaseValidator），再接好目前場景的 PlayerSpawner（PlayerSpawnerBuilder.Wire 會接 GameDatabase）。
    /// PlayerSpawnerBuilder 找不到 GameDatabase 時也會呼叫 EnsureAssets。
    /// </summary>
    public static class ProgressionBuilder
    {
        const string Root = "Assets/ScriptableObjects/Progression";
        const string CharactersDir = Root + "/Characters";
        public const string DatabasePath = Root + "/GameDatabase.asset";
        public const string RosterPath = Root + "/CharacterRoster.asset";
        public const string DaoDunDogPath = CharactersDir + "/DaoDunDog.asset";
        public const string DaoDunDogStatsPath = CharactersDir + "/DaoDunDogStats.asset";
        public const string TestDogPath = CharactersDir + "/TestDog.asset";
        public const string TestDogStatsPath = CharactersDir + "/TestDogStats.asset";
        // 測試狗建立時的數值（企劃書「角色系統」的例子）；之後在 TestDogStats.asset 調
        const float TestDogMaxHealth = 80f;
        const float TestDogMoveSpeed = 6f;
        // 裝備（T22）
        const string EquipmentDir = Root + "/Equipment";
        public const string CollarSlotPath = EquipmentDir + "/Slot_Collar.asset";
        public const string AmuletSlotPath = EquipmentDir + "/Slot_Amulet.asset";
        public const string ThickCollarPath = EquipmentDir + "/ThickCollar.asset";
        public const string CounterAmuletPath = EquipmentDir + "/CounterAmulet.asset";
        public const string ParryHealPath = EquipmentDir + "/ParryHeal.asset";
        const string IconDir = "Assets/Models/UI/Equipment";
        const string CollarIconPath = IconDir + "/Icon_Collar.png";
        const string AmuletIconPath = IconDir + "/Icon_Amulet.png";
        // 測試裝備建立時的數值（企劃書「裝備系統」）；之後在資產調
        const float ThickCollarMaxHealth = 20f;
        const float CounterAmuletParryHeal = 5f;
        // 技能樹（T23）
        const string SkillTreesDir = Root + "/SkillTrees";
        public const string DaoDunDogTreePath = SkillTreesDir + "/DaoDunDogTree.asset";
        public const string TestDogTreePath = SkillTreesDir + "/TestDogTree.asset";
        // Boss 與任務（T24）
        const string BossesDir = Root + "/Bosses";
        const string QuestsDir = Root + "/Quests";
        public const string OiiaCatPath = BossesDir + "/OiiaCat.asset";
        public const string SpinTheCatQuestPath = QuestsDir + "/SpinTheCat.asset";
        const string BattleSettingsPath = "Assets/ScriptableObjects/Battle/BattleSettings.asset";
        // 建立時的獎勵（企劃書「獎勵」「村長與任務」）；之後在資產調
        const int OiiaCatFirstClearSkillPoints = 3;
        const int SpinTheCatSkillPoints = 2;
        const string LegacyStatsPath = "Assets/ScriptableObjects/Player/PlayerStats.asset"; // T18 以前的位置

        [MenuItem("Tools/Progression/Build Game Database")]
        public static void BuildFromMenu()
        {
            var database = EnsureAssets();
            GameDatabaseValidator.Validate(database, true);
            PlayerSpawnerBuilder.Wire();
            Selection.activeObject = database;
        }

        /// <summary>目前的 GameDatabase（沒有時 null）。</summary>
        public static GameDatabase LoadDatabase() => AssetDatabase.LoadAssetAtPath<GameDatabase>(DatabasePath);

        /// <summary>刀盾狗的基礎數值（沒有時 null）。</summary>
        public static PlayerStats LoadDaoDunDogStats()
        {
            MoveLegacyStats();
            return AssetDatabase.LoadAssetAtPath<PlayerStats>(DaoDunDogStatsPath);
        }

        /// <summary>建立（或補齊）刀盾狗定義、名冊與 GameDatabase；回傳 GameDatabase。</summary>
        public static GameDatabase EnsureAssets()
        {
            DaoDunDogBuilder.EnsureFolder(CharactersDir);
            var stats = LoadDaoDunDogStats();
            if (stats == null)
            {
                stats = ScriptableObject.CreateInstance<PlayerStats>();
                AssetDatabase.CreateAsset(stats, DaoDunDogStatsPath);
                Debug.LogWarning($"[Progression] 找不到刀盾狗的數值，已用預設值建立 {DaoDunDogStatsPath}。");
            }

            bool dogCreated = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(DaoDunDogPath) == null;
            var dog = LoadOrCreate<CharacterDefinition>(DaoDunDogPath);
            if (dogCreated) dog.unlockedByDefault = true;
            if (string.IsNullOrEmpty(dog.id)) dog.id = "daodun_dog";
            if (string.IsNullOrEmpty(dog.displayName)) dog.displayName = "刀盾狗";
            if (dog.prefab == null) dog.prefab = AssetDatabase.LoadAssetAtPath<PlayerRig>(DaoDunDogBuilder.PrefabPath);
            if (dog.baseStats == null) dog.baseStats = stats;
            EditorUtility.SetDirty(dog);
            if (dog.prefab == null) Debug.LogWarning($"[Progression] {DaoDunDogPath} 沒有 prefab：請先跑 Tools > Player > Build DaoDun Dog，再跑一次 Build Game Database。");

            var testDog = EnsureTestDog(stats);

            var roster = LoadOrCreate<CharacterRoster>(RosterPath);
            if (!roster.characters.Contains(dog)) roster.characters.Insert(0, dog);
            if (!roster.characters.Contains(testDog)) roster.characters.Insert(Mathf.Min(1, roster.characters.Count), testDog);
            EditorUtility.SetDirty(roster);

            var database = LoadOrCreate<GameDatabase>(DatabasePath);
            if (database.characters == null) database.characters = roster;
            EnsureEquipment(database, dog, testDog, out var thickCollar, out var counterAmulet);
            EnsureSkillTrees(dog, testDog);
            EnsureBossesAndQuests(database, thickCollar, counterAmulet);
            EditorUtility.SetDirty(database);

            AssetDatabase.SaveAssets();
            return database;
        }

        // 測試狗（T21）：只在開發者模式列出；數值是刀盾狗的複本再改血量與跑速（只在建立時）
        static CharacterDefinition EnsureTestDog(PlayerStats dogStats)
        {
            var stats = AssetDatabase.LoadAssetAtPath<PlayerStats>(TestDogStatsPath);
            if (stats == null)
            {
                stats = Object.Instantiate(dogStats);
                stats.maxHealth = TestDogMaxHealth;
                stats.moveSpeed = TestDogMoveSpeed;
                AssetDatabase.CreateAsset(stats, TestDogStatsPath);
                Debug.Log($"[Progression] 已建立 {TestDogStatsPath}（血量 {TestDogMaxHealth}、跑速 {TestDogMoveSpeed}）");
            }

            bool created = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(TestDogPath) == null;
            var dog = LoadOrCreate<CharacterDefinition>(TestDogPath);
            if (created)
            {
                dog.developerOnly = true;
                dog.unlockedByDefault = true;
            }
            if (string.IsNullOrEmpty(dog.id)) dog.id = "test_dog";
            if (string.IsNullOrEmpty(dog.displayName)) dog.displayName = "測試狗";
            if (dog.prefab == null)
            {
                dog.prefab = AssetDatabase.LoadAssetAtPath<PlayerRig>(DaoDunDogBuilder.TestDogPrefabPath);
                if (dog.prefab == null) dog.prefab = DaoDunDogBuilder.BuildTestVariant();
            }
            if (dog.baseStats == null) dog.baseStats = stats;
            EditorUtility.SetDirty(dog);
            if (dog.prefab == null) Debug.LogWarning($"[Progression] {TestDogPath} 沒有 prefab：請先跑 Tools > Player > Build DaoDun Dog，再跑一次 Build Game Database。");
            return dog;
        }

        // 裝備（T22）：欄位種類、兩件測試裝備、盾反回血效果、角色的裝備欄；已存在的只補空欄位
        static void EnsureEquipment(GameDatabase database, CharacterDefinition dog, CharacterDefinition testDog, out EquipmentData thickCollar, out EquipmentData counterAmulet)
        {
            DaoDunDogBuilder.EnsureFolder(EquipmentDir);
            var collar = LoadOrCreate<EquipmentSlotType>(CollarSlotPath);
            if (string.IsNullOrEmpty(collar.displayName)) collar.displayName = "項圈";
            EditorUtility.SetDirty(collar);
            var amulet = LoadOrCreate<EquipmentSlotType>(AmuletSlotPath);
            if (string.IsNullOrEmpty(amulet.displayName)) amulet.displayName = "護符";
            EditorUtility.SetDirty(amulet);

            if (dog.equipmentSlots == null || dog.equipmentSlots.Length == 0) dog.equipmentSlots = new[] { collar, amulet };
            EditorUtility.SetDirty(dog);
            if (testDog.equipmentSlots == null || testDog.equipmentSlots.Length == 0) testDog.equipmentSlots = new[] { amulet };
            EditorUtility.SetDirty(testDog);

            bool healCreated = AssetDatabase.LoadAssetAtPath<ParryHealEffect>(ParryHealPath) == null;
            var parryHeal = LoadOrCreate<ParryHealEffect>(ParryHealPath);
            if (healCreated) parryHeal.healAmount = CounterAmuletParryHeal;
            if (string.IsNullOrEmpty(parryHeal.displayName)) parryHeal.displayName = $"盾反成功時回 {parryHeal.healAmount:0.#} HP";
            EditorUtility.SetDirty(parryHeal);

            bool collarCreated = AssetDatabase.LoadAssetAtPath<EquipmentData>(ThickCollarPath) == null;
            thickCollar = LoadOrCreate<EquipmentData>(ThickCollarPath);
            if (collarCreated) thickCollar.modifiers.Add(new StatModifier(PlayerStatId.MaxHealth, StatModifierOp.Add, ThickCollarMaxHealth));
            FillEquipment(thickCollar, "thick_collar", "厚皮項圈", $"厚實的皮革項圈，挨打也不怕。最大血量 +{ThickCollarMaxHealth:0}。", "擊敗 OIIA 貓",
                          collar, GetIcon(CollarIconPath, DrawCollar));

            bool amuletCreated = AssetDatabase.LoadAssetAtPath<EquipmentData>(CounterAmuletPath) == null;
            counterAmulet = LoadOrCreate<EquipmentData>(CounterAmuletPath);
            if (amuletCreated) counterAmulet.effects.Add(parryHeal);
            FillEquipment(counterAmulet, "counter_amulet", "反擊護符", $"刻著盾紋的護符。盾反成功時回復 {CounterAmuletParryHeal:0} HP。", "村長任務",
                          amulet, GetIcon(AmuletIconPath, DrawAmulet));

            if (database.equipment == null) database.equipment = new List<EquipmentData>();
            if (!database.equipment.Contains(thickCollar)) database.equipment.Add(thickCollar);
            if (!database.equipment.Contains(counterAmulet)) database.equipment.Add(counterAmulet);
        }

        // 技能樹（T23）：樹是空的時候才寫入節點（企劃書「技能樹」的測試樹；數值之後在資產調）
        static void EnsureSkillTrees(CharacterDefinition dog, CharacterDefinition testDog)
        {
            DaoDunDogBuilder.EnsureFolder(SkillTreesDir);
            var dogTree = LoadOrCreate<SkillTreeData>(DaoDunDogTreePath);
            if (dogTree.nodes.Count == 0)
            {
                // 左欄：堅韌 → 鐵壁 → 盾反專精；右欄：輕盈 → 骨頭收藏家
                dogTree.nodes.Add(Node("toughness", "堅韌", "皮粗肉厚。最大血量 +10。", 1, 0, 0, null,
                                       new StatModifier(PlayerStatId.MaxHealth, StatModifierOp.Add, 10f)));
                dogTree.nodes.Add(Node("iron_wall", "鐵壁", "盾舉得更穩。格擋減傷 +5%。", 1, 0, 1, "toughness",
                                       new StatModifier(PlayerStatId.BlockDamageReduction, StatModifierOp.Add, 0.05f)));
                dogTree.nodes.Add(Node("parry_master", "盾反專精", "盾反時打亂對手的節奏。盾反失衡值 +5。", 2, 0, 2, "iron_wall",
                                       new StatModifier(PlayerStatId.ParryImbalance, StatModifierOp.Add, 5f)));
                dogTree.nodes.Add(Node("light_feet", "輕盈", "腳步輕快。翻滾耐力消耗 −5。", 1, 1, 0, null,
                                       new StatModifier(PlayerStatId.RollStaminaCost, StatModifierOp.Add, -5f)));
                dogTree.nodes.Add(Node("bone_collector", "骨頭收藏家", "口袋多一格。骨頭持有上限 +1。", 2, 1, 1, "light_feet",
                                       new StatModifier(PlayerStatId.BoneCapacity, StatModifierOp.Add, 1f)));
                Debug.Log($"[Progression] {DaoDunDogTreePath}：寫入刀盾狗的測試樹（5 個節點，共 7 點）");
            }
            EditorUtility.SetDirty(dogTree);

            var testTree = LoadOrCreate<SkillTreeData>(TestDogTreePath);
            if (testTree.nodes.Count == 0)
            {
                testTree.nodes.Add(Node("swift", "疾風", "跑得更快。跑速 ×1.1。", 1, 0, 0, null,
                                        new StatModifier(PlayerStatId.MoveSpeed, StatModifierOp.Multiply, 0.1f)));
                testTree.nodes.Add(Node("thick_fur", "厚毛", "毛多不怕打。最大血量 +15。", 1, 0, 1, "swift",
                                        new StatModifier(PlayerStatId.MaxHealth, StatModifierOp.Add, 15f)));
                Debug.Log($"[Progression] {TestDogTreePath}：寫入測試狗的小樹（2 個節點）");
            }
            EditorUtility.SetDirty(testTree);

            if (dog.skillTree == null) dog.skillTree = dogTree;
            EditorUtility.SetDirty(dog);
            if (testDog.skillTree == null) testDog.skillTree = testTree;
            EditorUtility.SetDirty(testDog);
        }

        // Boss 與任務（T24）：已存在的只補空欄位；獎勵、對白只在建立時寫入
        static void EnsureBossesAndQuests(GameDatabase database, EquipmentData thickCollar, EquipmentData counterAmulet)
        {
            DaoDunDogBuilder.EnsureFolder(BossesDir);
            DaoDunDogBuilder.EnsureFolder(QuestsDir);

            bool catCreated = AssetDatabase.LoadAssetAtPath<BossDefinition>(OiiaCatPath) == null;
            var cat = LoadOrCreate<BossDefinition>(OiiaCatPath);
            if (catCreated)
            {
                cat.firstClearRewards.Add(RewardData.SkillPoints(OiiaCatFirstClearSkillPoints));
                cat.firstClearRewards.Add(RewardData.Unlock(thickCollar));
            }
            if (string.IsNullOrEmpty(cat.id)) cat.id = "oiia_cat";
            if (string.IsNullOrEmpty(cat.displayName)) cat.displayName = "OIIA 貓";
            if (string.IsNullOrEmpty(cat.sceneName)) cat.sceneName = "BossArena";
            if (string.IsNullOrEmpty(cat.portalDescription)) cat.portalDescription = "森林深處一邊唱歌一邊轉圈的怪貓。轉得越快，雷雨越大。";
            EditorUtility.SetDirty(cat);

            bool questCreated = AssetDatabase.LoadAssetAtPath<QuestData>(SpinTheCatQuestPath) == null;
            var quest = LoadOrCreate<QuestData>(SpinTheCatQuestPath);
            if (questCreated)
            {
                quest.offerLines.AddRange(new[]
                {
                    "那隻怪貓就在傳送門的另一頭，整天轉個不停。",
                    "只要讓牠轉到頭暈、乖乖停下來，森林就會恢復平靜。",
                    "這件事就交給你了，願意幫忙嗎？",
                });
                quest.activeLines.AddRange(new[]
                {
                    "那隻貓還在轉嗎？傳送門就在南邊的小路盡頭。",
                    "記得用盾牌擋住牠的爪子，看準時機反擊！",
                });
                quest.turnInLines.AddRange(new[]
                {
                    "喔喔！雷雨停了……你真的讓那隻貓停下來了！",
                    "這是村子的一點心意，收下吧。",
                });
                quest.rewards.Add(RewardData.SkillPoints(SpinTheCatSkillPoints));
                quest.rewards.Add(RewardData.Unlock(counterAmulet));
            }
            if (string.IsNullOrEmpty(quest.id)) quest.id = "spin_the_cat";
            if (string.IsNullOrEmpty(quest.title)) quest.title = "轉暈那隻小笨貓!";
            if (string.IsNullOrEmpty(quest.description)) quest.description = "擊敗森林深處的 OIIA 貓。";
            if (quest.objective == null) quest.objective = new QuestObjective();
            if (quest.objective.boss == null) quest.objective.boss = cat;
            EditorUtility.SetDirty(quest);

            if (database.bosses == null) database.bosses = new List<BossDefinition>();
            if (!database.bosses.Contains(cat)) database.bosses.Add(cat);
            if (database.quests == null) database.quests = new List<QuestData>();
            if (!database.quests.Contains(quest)) database.quests.Add(quest);

            var battle = AssetDatabase.LoadAssetAtPath<BattleSettings>(BattleSettingsPath);
            if (battle != null && battle.boss == null)
            {
                battle.boss = cat;
                EditorUtility.SetDirty(battle);
            }
        }

        static SkillNode Node(string id, string displayName, string description, int cost, int column, int row, string prerequisite, StatModifier modifier)
        {
            var node = new SkillNode
            {
                id = id,
                displayName = displayName,
                description = description,
                cost = cost,
                gridPosition = new Vector2Int(column, row),
            };
            if (!string.IsNullOrEmpty(prerequisite)) node.prerequisites.Add(prerequisite);
            node.modifiers.Add(modifier);
            return node;
        }

        static void FillEquipment(EquipmentData item, string id, string displayName, string description, string sourceHint, EquipmentSlotType slot, Sprite icon)
        {
            if (string.IsNullOrEmpty(item.id)) item.id = id;
            if (string.IsNullOrEmpty(item.displayName)) item.displayName = displayName;
            if (string.IsNullOrEmpty(item.description)) item.description = description;
            if (string.IsNullOrEmpty(item.sourceHint)) item.sourceHint = sourceHint;
            if (item.slotType == null) item.slotType = slot;
            if (item.icon == null) item.icon = icon;
            EditorUtility.SetDirty(item);
        }

        // ---------- 佔位圖示（只在不存在時產生） ----------

        const int IconSize = 128;

        static Sprite GetIcon(string path, Func<Vector2, Color> draw)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite != null) return sprite;
            DaoDunDogBuilder.EnsureFolder(IconDir);
            var tex = new Texture2D(IconSize, IconSize, TextureFormat.RGBA32, false);
            var pixels = new Color[IconSize * IconSize];
            for (int y = 0; y < IconSize; y++)
            {
                for (int x = 0; x < IconSize; x++)
                    pixels[y * IconSize + x] = draw(new Vector2((x + 0.5f) / IconSize * 2f - 1f, (y + 0.5f) / IconSize * 2f - 1f)); // -1..1，y 往上
            }
            tex.SetPixels(pixels);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        // 反鋸齒的覆蓋率：d 是到邊界的距離（裡面為負），單位是 -1..1 的座標
        static float Cover(float d) => Mathf.Clamp01(0.5f - d * IconSize * 0.5f);

        // 把 color 以覆蓋率 coverage 疊在 under 上面
        static Color Over(Color under, Color color, float coverage)
        {
            float a = color.a * coverage;
            float outA = a + under.a * (1f - a);
            if (outA <= 0f) return Color.clear;
            Color rgb = (color * a + under * (under.a * (1f - a))) / outA;
            rgb.a = outA;
            return rgb;
        }

        // 方塊的有號距離
        static float Box(Vector2 p, Vector2 center, Vector2 half)
        {
            Vector2 d = new Vector2(Mathf.Abs(p.x - center.x), Mathf.Abs(p.y - center.y)) - half;
            return new Vector2(Mathf.Max(d.x, 0f), Mathf.Max(d.y, 0f)).magnitude + Mathf.Min(Mathf.Max(d.x, d.y), 0f);
        }

        // 項圈：棕色皮環（略扁）、縫線、金色扣環、垂下的小牌子
        static Color DrawCollar(Vector2 p)
        {
            var c = Color.clear;
            Vector2 q = new Vector2(p.x, (p.y - 0.1f) / 0.75f);
            float ring = Mathf.Abs(q.magnitude - 0.62f) - 0.16f;
            c = Over(c, new Color(0.18f, 0.1f, 0.05f), Cover(ring - 0.03f));
            c = Over(c, new Color(0.55f, 0.32f, 0.16f), Cover(ring));
            float stitch = Mathf.Abs(q.magnitude - 0.62f) - 0.012f;
            bool dash = Mathf.Repeat(Mathf.Atan2(q.y, q.x) * 8f, 1f) < 0.5f;
            c = Over(c, new Color(0.85f, 0.7f, 0.45f, 0.8f), dash ? Cover(stitch) : 0f);
            float buckle = Mathf.Abs(Box(p, new Vector2(0f, -0.36f), new Vector2(0.17f, 0.15f))) - 0.045f;
            c = Over(c, new Color(0.95f, 0.78f, 0.25f), Cover(buckle));
            float tag = (p - new Vector2(0f, -0.68f)).magnitude - 0.16f;
            c = Over(c, new Color(0.45f, 0.35f, 0.15f), Cover(tag - 0.03f));
            c = Over(c, new Color(0.95f, 0.82f, 0.35f), Cover(tag));
            return c;
        }

        // 護符：金鏈、盾形的藍色墜子、中間的白十字
        static Color DrawAmulet(Vector2 p)
        {
            var c = Color.clear;
            float chain = Mathf.Abs((p - new Vector2(0f, 0.15f)).magnitude - 0.68f) - 0.03f;
            if (p.y > 0.05f) c = Over(c, new Color(0.95f, 0.78f, 0.3f), Cover(chain));
            // 盾形：上緣平、往下逐漸收成尖
            Vector2 s = new Vector2(p.x, p.y + 0.25f);
            float halfWidth = Mathf.Max(0.42f * Mathf.Clamp01((s.y + 0.55f) / 0.55f), 0.02f);
            float shield = Mathf.Max(Mathf.Abs(s.x) - halfWidth, Mathf.Abs(s.y + 0.06f) - 0.48f);
            c = Over(c, new Color(0.8f, 0.62f, 0.2f), Cover(shield - 0.05f));
            c = Over(c, new Color(0.2f, 0.45f, 0.85f), Cover(shield));
            float cross = Mathf.Min(Box(s, new Vector2(0f, 0.02f), new Vector2(0.06f, 0.26f)), Box(s, new Vector2(0f, 0.1f), new Vector2(0.2f, 0.06f)));
            c = Over(c, new Color(0.9f, 0.95f, 1f), Cover(cross));
            float loop = Mathf.Abs((p - new Vector2(0f, 0.24f)).magnitude - 0.07f) - 0.025f;
            c = Over(c, new Color(0.95f, 0.78f, 0.3f), Cover(loop));
            return c;
        }

        static void MoveLegacyStats()
        {
            if (AssetDatabase.LoadAssetAtPath<PlayerStats>(LegacyStatsPath) == null || AssetDatabase.LoadAssetAtPath<PlayerStats>(DaoDunDogStatsPath) != null) return;
            DaoDunDogBuilder.EnsureFolder(CharactersDir);
            string error = AssetDatabase.MoveAsset(LegacyStatsPath, DaoDunDogStatsPath);
            if (!string.IsNullOrEmpty(error))
            {
                Debug.LogError($"[Progression] 搬移 {LegacyStatsPath} 失敗：{error}");
                return;
            }
            Debug.Log($"[Progression] 刀盾狗的數值已搬到 {DaoDunDogStatsPath}（GUID 不變）。");
            string legacyDir = LegacyStatsPath.Substring(0, LegacyStatsPath.LastIndexOf('/'));
            if (AssetDatabase.IsValidFolder(legacyDir) && AssetDatabase.FindAssets("", new[] { legacyDir }).Length == 0)
                AssetDatabase.DeleteAsset(legacyDir);
        }

        static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            Debug.Log($"[Progression] 已建立 {path}");
            return asset;
        }
    }
}
