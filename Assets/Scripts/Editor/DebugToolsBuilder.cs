using System.Collections.Generic;
using System.Linq;
using BossFight.Battle;
using BossFight.Boss;
using BossFight.Core;
using BossFight.DebugTools;
using BossFight.Progression;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BossFight.EditorTools
{
    /// <summary>
    /// Tools > Debug > Wire Debug Tools：任何 Boss 戰場景共用的開發者工具接線（F3）。每個場景一個 DebugTools 物件、一個 DeveloperMode：
    /// 1. 找這場戰鬥的流程（IBattleDebugTarget：BossArena 的 BattleManager、序章的 PrologueCutsceneFlow）；都沒有（休息區）就不建。
    /// 2. DebugTools 物件（沿用 BossDebugTools 所在的物件，沒有就建）：BossDebugTools（F1–F7、Boss 面板）、BalanceDebugPanel（F12，BalancePanelBuilder 接）、
    ///    PlayerDebugOverlay（左上玩家狀態）；序章再加 PrologueDebugTools（F8–F11、F4 的序章面板）。
    /// 3. DeveloperMode：tools 是上面全部，關掉開發者模式時一起停用、實作 IDebugCheats 的一起 ClearCheats。場景裡其他的 DeveloperMode、
    ///    舊版序章的 Debug 物件（PrologueActorDebug）與 Missing Script 一律清掉。
    /// 會建立或重建流程的 Builder（GuardianBuilder、GuardianPhaseTwoBuilder、TutorialBuilder、PrologueActorsBuilder）結束時都會跑；
    /// BossArena 的 BossBuilder → PauseMenuBuilder.Wire 走 WireDeveloperMode。
    /// </summary>
    public static class DebugToolsBuilder
    {
        const string RootName = "DebugTools";
        const string OptionsDataPath = "Assets/ScriptableObjects/Options/GameOptionsData.asset";

        [MenuItem("Tools/Debug/Wire Debug Tools")]
        public static void WireFromMenu()
        {
            var root = Wire();
            if (root != null) Selection.activeGameObject = root;
        }

        /// <summary>這個場景的戰鬥流程（實作 IBattleDebugTarget）與它的 Boss；沒有時回傳 null。</summary>
        public static MonoBehaviour FindBattleTarget(out BossController boss)
        {
            var battle = Object.FindAnyObjectByType<BattleManager>();
            if (battle != null)
            {
                boss = new SerializedObject(battle).FindProperty("boss").objectReferenceValue as BossController;
                if (boss == null) boss = Object.FindAnyObjectByType<BossController>();
                return battle;
            }
            var flow = Object.FindAnyObjectByType<PrologueCutsceneFlow>();
            if (flow != null)
            {
                var guardian = Object.FindAnyObjectByType<GuardianPhaseController>();
                boss = guardian != null ? guardian.Boss : null;
                return flow;
            }
            boss = null;
            return null;
        }

        public static GameObject Wire()
        {
            var target = FindBattleTarget(out var boss);
            if (target == null)
            {
                Debug.Log("[DebugTools] 這個場景沒有 Boss 戰流程（BattleManager 或 PrologueCutsceneFlow），不建開發者工具。");
                return null;
            }
            var scene = target.gameObject.scene;
            RemoveLegacy(scene);

            var bossTools = Object.FindAnyObjectByType<BossDebugTools>();
            GameObject root = bossTools != null ? bossTools.gameObject : GameObject.Find(RootName);
            if (root == null)
            {
                root = new GameObject(RootName);
                Undo.RegisterCreatedObjectUndo(root, "Wire Debug Tools");
            }
            if (bossTools == null) bossTools = Undo.AddComponent<BossDebugTools>(root);
            var tso = new SerializedObject(bossTools);
            tso.FindProperty("battle").objectReferenceValue = target;
            tso.FindProperty("boss").objectReferenceValue = boss;
            tso.FindProperty("bossHealth").objectReferenceValue = boss != null ? boss.GetComponent<BossHealth>() : null;
            tso.FindProperty("stagger").objectReferenceValue = boss != null ? boss.GetComponent<BossStagger>() : null;
            tso.ApplyModifiedPropertiesWithoutUndo();

            var overlay = DaoDunDogBuilder.GetOrAdd<PlayerDebugOverlay>(root);
            DaoDunDogBuilder.SetReference(overlay, "distanceReference", boss != null ? boss.transform : null);
            BalancePanelBuilder.Wire();

            if (target is PrologueCutsceneFlow flow)
            {
                var prologue = DaoDunDogBuilder.GetOrAdd<PrologueDebugTools>(root);
                var binding = Object.FindAnyObjectByType<PrologueActorBinding>();
                var bindingSo = binding != null ? new SerializedObject(binding) : null;
                DaoDunDogBuilder.SetReference(prologue, "flow", flow);
                DaoDunDogBuilder.SetReference(prologue, "tutorial", Object.FindAnyObjectByType<TutorialDirector>());
                DaoDunDogBuilder.SetReference(prologue, "binding", binding);
                DaoDunDogBuilder.SetReference(prologue, "mentor", bindingSo?.FindProperty("mentor").objectReferenceValue);
                DaoDunDogBuilder.SetReference(prologue, "sapling", bindingSo?.FindProperty("sapling").objectReferenceValue);
            }
            else
            {
                var stray = root.GetComponent<PrologueDebugTools>();
                if (stray != null) Undo.DestroyObjectImmediate(stray);
            }

            var options = Object.FindAnyObjectByType<GameOptions>();
            if (options != null && new SerializedObject(options).FindProperty("data").objectReferenceValue == null)
                DaoDunDogBuilder.SetReference(options, "data", AssetDatabase.LoadAssetAtPath<GameOptionsData>(OptionsDataPath));
            WireDeveloperMode(root, options, Object.FindAnyObjectByType<GamePause>());
            PlayerSpawnerBuilder.Wire();
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"[DebugTools] 開發者工具已接上 {root.name}（{target.GetType().Name}）：{string.Join("、", Tools(root).Select(t => t.GetType().Name))}");
            return root;
        }

        /// <summary>DebugTools 物件上的 DeveloperMode：tools 是物件上所有的 Debug 工具；場景裡其他的 DeveloperMode 刪掉。</summary>
        public static void WireDeveloperMode(GameObject root, GameOptions options, GamePause pause)
        {
            var developer = DaoDunDogBuilder.GetOrAdd<DeveloperMode>(root);
            foreach (var other in Object.FindObjectsByType<DeveloperMode>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (other != developer) Undo.DestroyObjectImmediate(other);
            }
            var so = new SerializedObject(developer);
            so.FindProperty("options").objectReferenceValue = options;
            so.FindProperty("pause").objectReferenceValue = pause;
            IntroCutsceneBuilder.SetArray(so.FindProperty("tools"), Tools(root));
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static List<Behaviour> Tools(GameObject root)
        {
            var tools = new List<Behaviour>();
            foreach (var behaviour in root.GetComponents<Behaviour>())
            {
                if (behaviour is BossDebugTools || behaviour is BalanceDebugPanel || behaviour is PlayerDebugOverlay || behaviour is PrologueDebugTools)
                    tools.Add(behaviour);
            }
            return tools;
        }

        // 舊版序章各 Builder 自己建的 Debug 物件、刪掉的 GuardianDebugTools／PrologueActorDebugTools 留下的 Missing Script
        static void RemoveLegacy(UnityEngine.SceneManagement.Scene scene)
        {
            var old = GameObject.Find("PrologueActorDebug");
            if (old != null) Undo.DestroyObjectImmediate(old);
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) > 0)
                        GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
                }
            }
        }
    }
}
