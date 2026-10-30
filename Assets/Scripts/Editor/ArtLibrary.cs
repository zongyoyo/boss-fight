using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace BossFight.EditorTools
{
    /// <summary>
    /// 美術模型清單（docs/Art/ART_STYLE.md「Unity 端規則」）：模型 id（＝FBX 檔名，例如 SM_Tree_Pine_A）→ Assets/Art/Models 底下的 FBX。
    /// 只給 Builder 用（類別在 Editor 資料夾，執行期程式碰不到）：Builder 原本建基本形體的地方改成 Spawn(id, parent, fallback)，
    /// 有模型就放模型（保留 prefab 連結，重新匯出 FBX 後場景與 prefab 跟著更新），沒有就呼叫 fallback 建原本的佔位幾何，
    /// 所以模型可以一件一件換、遊戲隨時都能跑。
    /// 清單由 ArtImportPostprocessor 在 Assets/Art/Models 有變動時自動同步（Tools > Art > Sync Art Library 也能手動跑），不要手動加減。
    /// Builder 永遠不寫入 Assets/Art/：那裡只有 Blender 的匯出腳本會寫。
    /// </summary>
    public class ArtLibrary : ScriptableObject
    {
        public const string AssetPath = "Assets/ScriptableObjects/Art/ArtLibrary.asset";
        public const string ModelsRoot = "Assets/Art/Models";

        [Serializable]
        public class Entry
        {
            [Tooltip("模型 id（FBX 檔名）")]
            public string id;
            [Tooltip("Assets/Art/Models 底下的 FBX")]
            public GameObject model;
        }

        [Tooltip("自動同步，依 id 排序；不要手動加減")]
        public List<Entry> entries = new List<Entry>();

        public static ArtLibrary Load() => AssetDatabase.LoadAssetAtPath<ArtLibrary>(AssetPath);

        /// <summary>這個 id 的模型；沒有時回傳 null。</summary>
        public GameObject Get(string id)
        {
            foreach (var e in entries)
                if (e.id == id) return e.model;
            return null;
        }

        /// <summary>這個 id 有沒有正式模型。</summary>
        public static bool Has(string id)
        {
            var library = Load();
            return library != null && library.Get(id) != null;
        }

        /// <summary>
        /// 有正式模型就在 parent 底下放一份（prefab 連結、位置與旋轉歸零、縮放 1），否則回傳 fallback() 建的佔位物件（fallback 可以是 null）。
        /// 模型的軸心與方向照 Blender 的匯出（底部中心、正面朝 +Z），Builder 只要擺 parent。
        /// </summary>
        public static GameObject Spawn(string id, Transform parent, Func<GameObject> fallback = null)
        {
            var library = Load();
            var model = library != null ? library.Get(id) : null;
            if (model == null) return fallback?.Invoke();
            var go = (GameObject)PrefabUtility.InstantiatePrefab(model, parent);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            return go;
        }

        /// <summary>依 Assets/Art/Models 底下所有 .fbx 重建清單（沒有清單資產就建）；回傳變動的說明，沒變動時是空的。</summary>
        public static List<string> Sync()
        {
            var changes = new List<string>();
            var library = Load();
            if (library == null)
            {
                DaoDunDogBuilder.EnsureFolder(Path.GetDirectoryName(AssetPath).Replace('\\', '/'));
                library = CreateInstance<ArtLibrary>();
                AssetDatabase.CreateAsset(library, AssetPath);
                changes.Add("建立 ArtLibrary");
            }

            var found = new SortedDictionary<string, GameObject>(StringComparer.Ordinal);
            if (AssetDatabase.IsValidFolder(ModelsRoot))
            {
                foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { ModelsRoot }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (!path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase)) continue;
                    string id = Path.GetFileNameWithoutExtension(path);
                    var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (model == null) continue;
                    if (found.TryGetValue(id, out var other))
                    {
                        Debug.LogWarning($"[Art] 模型 id 重複：{path} 和 {AssetDatabase.GetAssetPath(other)}，只登錄後者。FBX 檔名要唯一。");
                        continue;
                    }
                    found.Add(id, model);
                }
            }

            var before = library.entries.Where(e => e != null).ToDictionary(e => e.id ?? "", e => e.model);
            foreach (var pair in found)
                if (!before.TryGetValue(pair.Key, out var old) || old != pair.Value) changes.Add($"登錄 {pair.Key}");
            foreach (var pair in before)
                if (!found.ContainsKey(pair.Key)) changes.Add($"移除 {pair.Key}");
            if (changes.Count == 0 && before.Count == found.Count) return changes;

            library.entries = found.Select(p => new Entry { id = p.Key, model = p.Value }).ToList();
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssetIfDirty(library);
            return changes;
        }
    }
}
