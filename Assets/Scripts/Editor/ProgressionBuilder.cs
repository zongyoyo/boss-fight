using BossFight.Player;
using BossFight.Progression;
using UnityEditor;
using UnityEngine;

namespace BossFight.EditorTools
{
    /// <summary>
    /// Tools > Progression > Build Game Database：建立角色與數值管線的資產（T19），可重複執行。
    /// 1. 刀盾狗的基礎數值：T18 以前的 ScriptableObjects/Player/PlayerStats.asset 用 AssetDatabase.MoveAsset 搬成
    ///    ScriptableObjects/Progression/Characters/DaoDunDogStats.asset（GUID 不變，prefab 與鏡頭的引用不會斷）。
    /// 2. 資產（沒有才建，已存在的只補空欄位）：Characters/DaoDunDog.asset（CharacterDefinition）、CharacterRoster.asset、GameDatabase.asset。
    ///    測試狗（T21）：Characters/TestDog.asset（developerOnly）與 TestDogStats.asset（建立時複製刀盾狗的數值，改成 testDogMaxHealth、testDogMoveSpeed），
    ///    prefab 是 DaoDunDogBuilder.BuildTestVariant 的 variant（沒有時建立）；排在名冊第 2 個。
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
