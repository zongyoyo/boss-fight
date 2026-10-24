using System.Collections.Generic;
using System.Linq;
using BossFight.Boss;
using BossFight.Core;
using BossFight.Player;
using BossFight.Progression;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BossFight.EditorTools
{
    /// <summary>
    /// 場景裡的 PlayerSpawner（T18）：Build 在出生位置建立 spawner（刪掉舊的 spawner 與場景裡舊的玩家實例），底下放 EditorOnly 的預覽模型
    /// （角色 prefab 的實例，編輯模式給 Timeline 綁軌道、預覽鏡頭用；進 Play 時 spawner 關掉它，正式 build 不會有）。
    /// 生成哪個角色由 GameDatabase（ProgressionBuilder 建立）與存檔決定，預覽模型是名冊的預設角色。
    /// Wire 自動收集場景裡的接收者（所有實作 IPlayerBinding 的元件）、Main Camera、LockOnTarget、ExecutionTarget、AudioManager 與 BossController 接到 spawner，可重複執行；
    /// 重建會動到這些物件的 Builder（鏡頭、Boss、開場、轉階段、戰鬥 UI、平衡面板、ESC 選單、打擊回饋、木樁）結束時都會呼叫。
    /// 選單 Tools > Player > Wire Player Spawner 可以單獨跑。
    /// </summary>
    public static class PlayerSpawnerBuilder
    {
        public const string SpawnerName = "PlayerSpawner";
        const string PreviewName = "PreviewCharacter";

        [MenuItem("Tools/Player/Wire Player Spawner")]
        public static void WireFromMenu()
        {
            var spawner = Wire();
            if (spawner != null) Selection.activeGameObject = spawner.gameObject;
        }

        /// <summary>場景裡的 spawner（沒有時 null）。</summary>
        public static PlayerSpawner Find() => Object.FindAnyObjectByType<PlayerSpawner>(FindObjectsInactive.Include);

        /// <summary>spawner 底下的預覽模型；Builder 用它綁 Timeline、量臉的位置、接鏡頭。沒有 spawner 時 null。</summary>
        public static PlayerRig Preview()
        {
            var spawner = Find();
            if (spawner == null) return null;
            var preview = new SerializedObject(spawner).FindProperty("previewCharacter").objectReferenceValue as GameObject;
            return preview != null ? preview.GetComponent<PlayerRig>() : null;
        }

        /// <summary>
        /// 在 position、rotation 建立 spawner 與預覽模型；spawnPoints 是換場景進來時的出生點（null = 沿用舊 spawner 的清單）。
        /// 場景裡舊的 spawner、舊的玩家實例（T17 以前直接放在場景裡的刀盾狗）都會刪掉。
        /// </summary>
        public static PlayerSpawner Build(Vector3 position, Quaternion rotation, params (string id, Transform point)[] spawnPoints)
        {
            // 預覽模型用名冊的預設角色（T19）；資產還沒建立時順便建立
            var database = ProgressionBuilder.EnsureAssets();
            var character = database != null ? database.DefaultCharacter : null;
            var prefab = character != null ? character.prefab : null;
            if (prefab == null)
            {
                Debug.LogError($"[PlayerSpawner] 名冊的預設角色沒有 prefab（{DaoDunDogBuilder.PrefabPath}），請先跑 Tools > Player > Build DaoDun Dog。");
                return null;
            }

            (string, Transform)[] keep = null;
            var old = Find();
            if (old != null)
            {
                if (spawnPoints == null || spawnPoints.Length == 0) keep = ReadSpawnPoints(old);
                Undo.DestroyObjectImmediate(old.gameObject);
            }
            foreach (var stray in Object.FindObjectsByType<PlayerController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Undo.DestroyObjectImmediate(stray.gameObject);

            var go = new GameObject(SpawnerName);
            Undo.RegisterCreatedObjectUndo(go, "Build Player Spawner");
            go.transform.SetPositionAndRotation(position, rotation);
            var spawner = go.AddComponent<PlayerSpawner>();

            var preview = (GameObject)PrefabUtility.InstantiatePrefab(prefab.gameObject, go.scene);
            preview.name = PreviewName;
            preview.tag = "EditorOnly";
            preview.transform.SetParent(go.transform, false);
            preview.transform.localPosition = Vector3.zero;
            preview.transform.localRotation = Quaternion.identity;

            var so = new SerializedObject(spawner);
            so.FindProperty("previewCharacter").objectReferenceValue = preview;
            so.ApplyModifiedPropertiesWithoutUndo();
            SetSpawnPoints(spawner, spawnPoints != null && spawnPoints.Length > 0 ? spawnPoints : keep ?? new (string, Transform)[0]);

            Wire();
            EditorSceneManager.MarkSceneDirty(go.scene);
            Debug.Log($"[PlayerSpawner] 已建立：{position}，預覽模型 {PreviewName}（EditorOnly）");
            return spawner;
        }

        /// <summary>換場景進來時的出生點清單。</summary>
        public static void SetSpawnPoints(PlayerSpawner spawner, params (string id, Transform point)[] points)
        {
            var so = new SerializedObject(spawner);
            var list = so.FindProperty("spawnPoints");
            list.arraySize = points.Length;
            for (int i = 0; i < points.Length; i++)
            {
                var element = list.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("id").stringValue = points[i].id;
                element.FindPropertyRelative("point").objectReferenceValue = points[i].point;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static (string, Transform)[] ReadSpawnPoints(PlayerSpawner spawner)
        {
            var list = new SerializedObject(spawner).FindProperty("spawnPoints");
            var points = new (string, Transform)[list.arraySize];
            for (int i = 0; i < points.Length; i++)
            {
                var element = list.GetArrayElementAtIndex(i);
                points[i] = (element.FindPropertyRelative("id").stringValue, element.FindPropertyRelative("point").objectReferenceValue as Transform);
            }
            return points;
        }

        /// <summary>接好 spawner 的場景物件與接收者；場景裡沒有 spawner 時什麼都不做。</summary>
        public static PlayerSpawner Wire()
        {
            var spawner = Find();
            if (spawner == null) return null;

            var preview = Preview();
            bool InPreview(Component c) => preview != null && c.transform.IsChildOf(preview.transform);

            var receivers = Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                                  .Where(c => c is IPlayerBinding && !InPreview(c))
                                  .OrderBy(c => c.gameObject.name).ThenBy(c => c.GetType().Name).ToList();
            var lockTargets = Object.FindObjectsByType<LockOnTarget>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                                    .Where(t => !InPreview(t)).OrderBy(t => t.name).ToList();
            var executionTargets = Object.FindObjectsByType<ExecutionTarget>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                                         .Where(t => !InPreview(t)).OrderBy(t => t.name).ToList();
            var bosses = Object.FindObjectsByType<BossController>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                               .OrderBy(b => b.name).ToList();
            var mainCamera = Camera.main;

            var database = ProgressionBuilder.LoadDatabase();
            if (database == null) database = ProgressionBuilder.EnsureAssets();

            var so = new SerializedObject(spawner);
            so.FindProperty("database").objectReferenceValue = database;
            so.FindProperty("cameraTransform").objectReferenceValue = mainCamera != null ? mainCamera.transform : null;
            so.FindProperty("audioManager").objectReferenceValue = Object.FindAnyObjectByType<AudioManager>(FindObjectsInactive.Include);
            SetList(so.FindProperty("lockTargets"), lockTargets);
            SetList(so.FindProperty("executionTargets"), executionTargets);
            SetList(so.FindProperty("bosses"), bosses);
            SetList(so.FindProperty("receivers"), receivers);
            so.ApplyModifiedProperties();
            EditorSceneManager.MarkSceneDirty(spawner.gameObject.scene);

            string names = receivers.Count > 0 ? string.Join("、", receivers.Select(r => r.GetType().Name)) : "（沒有）";
            Debug.Log($"[PlayerSpawner] 已接線：接收者 {receivers.Count} 個（{names}）、鎖定 {lockTargets.Count}、處決 {executionTargets.Count}、Boss {bosses.Count}" +
                      (mainCamera == null ? "；缺少 Main Camera" : ""));
            return spawner;
        }

        static void SetList<T>(SerializedProperty list, IList<T> items) where T : Object
        {
            list.arraySize = items.Count;
            for (int i = 0; i < items.Count; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
        }
    }
}
