using BossFight.Battle;
using BossFight.Boss;
using BossFight.Cameras;
using BossFight.DebugTools;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BossFight.EditorTools
{
    /// <summary>
    /// Tools > Debug > Wire Balance Panel：在 DebugTools 物件（BossDebugTools 所在）加上 F12 平衡面板（T10），接好 BattleManager、Boss 與鏡頭（玩家由 PlayerSpawner 執行時交給面板，這裡最後跑 PlayerSpawnerBuilder.Wire）。
    /// BattleUIBuilder（Build Intro 最後會跑）與 BossBuilder 結束時也會呼叫，重建 BattleManager 或 Boss 後引用不會斷。
    /// </summary>
    public static class BalancePanelBuilder
    {
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
            var battle = Object.FindAnyObjectByType<BattleManager>();
            var boss = Object.FindAnyObjectByType<BossController>();
            var cameraController = Object.FindAnyObjectByType<CameraController>();

            var panel = tools.GetComponent<BalanceDebugPanel>();
            if (panel == null) panel = Undo.AddComponent<BalanceDebugPanel>(tools.gameObject);
            var so = new SerializedObject(panel);
            so.FindProperty("battle").objectReferenceValue = battle;
            so.FindProperty("boss").objectReferenceValue = boss;
            so.FindProperty("bossHealth").objectReferenceValue = boss != null ? boss.GetComponent<BossHealth>() : null;
            so.FindProperty("stagger").objectReferenceValue = boss != null ? boss.GetComponent<BossStagger>() : null;
            so.FindProperty("cameraController").objectReferenceValue = cameraController;
            so.ApplyModifiedProperties();

            EditorSceneManager.MarkSceneDirty(panel.gameObject.scene);
            var spawner = PlayerSpawnerBuilder.Wire(); // 玩家由 spawner 交給面板（BindPlayer）
            string missing = (battle == null ? " BattleManager" : "") + (boss == null ? " Boss" : "") +
                             (spawner == null ? " PlayerSpawner" : "") + (cameraController == null ? " 鏡頭" : "");
            Debug.Log($"[Balance] 平衡面板（F12）已接上 {panel.gameObject.name}" + (missing.Length > 0 ? $"；缺少：{missing}" : ""));
            return panel;
        }
    }
}
