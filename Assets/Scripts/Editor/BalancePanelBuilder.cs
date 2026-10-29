using BossFight.Battle;
using BossFight.Boss;
using BossFight.Cameras;
using BossFight.DebugTools;
using BossFight.Progression;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BossFight.EditorTools
{
    /// <summary>
    /// Tools > Debug > Wire Balance Panel：在 DebugTools 物件（BossDebugTools 所在）加上 F12 平衡面板（T10），接好這場戰鬥的流程（DebugToolsBuilder.FindBattleTarget：
    /// BattleManager 或序章的 PrologueCutsceneFlow）、Boss、鏡頭、序章另外的 InputLockArbiter 與可調資料的物件（守衛流程、小樹精、菊花）、
    /// PlayerSpawner 與測試修正（T19，ScriptableObjects/Debug/TestModifiers.asset，沒有才建）（玩家由 PlayerSpawner 執行時交給面板，這裡最後跑 PlayerSpawnerBuilder.Wire）。
    /// BattleUIBuilder（Build Intro 最後會跑）與 BossBuilder 結束時也會呼叫，重建 BattleManager 或 Boss 後引用不會斷。
    /// </summary>
    public static class BalancePanelBuilder
    {
        const string TestModifiersPath = "Assets/ScriptableObjects/Debug/TestModifiers.asset";

        [MenuItem("Tools/Debug/Wire Balance Panel")]
        public static void WireFromMenu()
        {
            var panel = Wire();
            if (panel != null) Selection.activeGameObject = panel.gameObject;
        }

        public static BalanceDebugPanel Wire()
        {
            var tools = Object.FindAnyObjectByType<BossDebugTools>();
            if (tools == null)
            {
                Debug.LogWarning("[Balance] 場景裡沒有 BossDebugTools（Tools > Boss > Build Boss Placeholder 會建立 DebugTools），平衡面板沒有接上。");
                return null;
            }
            var battle = DebugToolsBuilder.FindBattleTarget(out var boss);
            var cameraController = Object.FindAnyObjectByType<CameraController>();

            var panel = tools.GetComponent<BalanceDebugPanel>();
            if (panel == null) panel = Undo.AddComponent<BalanceDebugPanel>(tools.gameObject);
            var so = new SerializedObject(panel);
            so.FindProperty("battle").objectReferenceValue = battle;
            so.FindProperty("boss").objectReferenceValue = boss;
            so.FindProperty("bossHealth").objectReferenceValue = boss != null ? boss.GetComponent<BossHealth>() : null;
            so.FindProperty("stagger").objectReferenceValue = boss != null ? boss.GetComponent<BossStagger>() : null;
            so.FindProperty("cameraController").objectReferenceValue = cameraController;
            // 序章：輸入鎖經 InputLockArbiter；守衛流程、小樹精、菊花的資料也換成複本
            bool prologue = battle is PrologueCutsceneFlow;
            so.FindProperty("inputLock").objectReferenceValue = prologue ? Object.FindAnyObjectByType<InputLockArbiter>() : null;
            var roots = new System.Collections.Generic.List<GameObject>();
            if (prologue)
            {
                var guardian = Object.FindAnyObjectByType<GuardianPhaseController>();
                var actors = Object.FindAnyObjectByType<PrologueActorBinding>();
                if (guardian != null) roots.Add(guardian.gameObject);
                if (actors != null) roots.Add(actors.gameObject);
            }
            IntroCutsceneBuilder.SetArray(so.FindProperty("extraRoots"), roots);
            so.FindProperty("spawner").objectReferenceValue = PlayerSpawnerBuilder.Find();
            so.FindProperty("testModifiers").objectReferenceValue = EnsureTestModifiers();
            so.ApplyModifiedProperties();

            EditorSceneManager.MarkSceneDirty(panel.gameObject.scene);
            var spawner = PlayerSpawnerBuilder.Wire(); // 玩家由 spawner 交給面板（BindPlayer）
            string missing = (battle == null ? " 戰鬥流程" : "") + (boss == null ? " Boss" : "") +
                             (spawner == null ? " PlayerSpawner" : "") + (cameraController == null ? " 鏡頭" : "");
            Debug.Log($"[Balance] 平衡面板（F12）已接上 {panel.gameObject.name}" + (missing.Length > 0 ? $"；缺少：{missing}" : ""));
            return panel;
        }

        // T19 的測試修正（血量 +20、跑速 ×1.2）：只在不存在時建立
        static TestModifierData EnsureTestModifiers()
        {
            var data = AssetDatabase.LoadAssetAtPath<TestModifierData>(TestModifiersPath);
            if (data != null) return data;
            DaoDunDogBuilder.EnsureFolder(TestModifiersPath.Substring(0, TestModifiersPath.LastIndexOf('/')));
            data = ScriptableObject.CreateInstance<TestModifierData>();
            AssetDatabase.CreateAsset(data, TestModifiersPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Balance] 已建立 {TestModifiersPath}");
            return data;
        }
    }
}
