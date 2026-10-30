using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BossFight.EditorTools
{
    /// <summary>
    /// Tools > Art：美術管線（docs/Art/ART_STYLE.md）的 Unity 端工具。
    /// - Setup Art Assets：建資料夾、套用色票貼圖的匯入設定、建 M_Palette／M_Palette_Emissive（只在不存在時建，之後在資產調）、
    ///   同步 ArtLibrary、重新匯入 FBX（材質依名稱換成專案裡的材質）。Blender 第一次輸出色票或改了 palette.json 後跑一次。
    /// - Sync Art Library：依 Assets/Art/Models 的 FBX 重建清單（平常匯入時會自動跑）。
    /// - Validate Art：檢查色票貼圖、兩個色票材質、每個 FBX 的匯入設定、軸向（Rotation 0、Scale 1）與材質都換成了專案資產。
    /// - Place Axis Test：在目前場景的 ScaleRef_2m 旁邊放軸向測試方塊（EditorOnly），沒有模型時放一個佔位方塊。
    /// </summary>
    public static class ArtTools
    {
        public const string PaletteTexture = "Assets/Art/Textures/T_Palette.png";
        public const string EmissiveTexture = "Assets/Art/Textures/T_Palette_Emissive.png";
        public const string PaletteMaterial = ArtImportPostprocessor.MaterialsRoot + "/M_Palette.mat";
        public const string EmissiveMaterial = ArtImportPostprocessor.MaterialsRoot + "/M_Palette_Emissive.mat";
        const string AxisTestId = "SM_Test_AxisCube";

        [MenuItem("Tools/Art/Setup Art Assets")]
        public static void Setup()
        {
            DaoDunDogBuilder.EnsureFolder("Assets/Art/Models");
            DaoDunDogBuilder.EnsureFolder("Assets/Art/Textures");
            DaoDunDogBuilder.EnsureFolder(ArtImportPostprocessor.MaterialsRoot);
            foreach (var path in new[] { PaletteTexture, EmissiveTexture })
                if (AssetDatabase.LoadAssetAtPath<Texture2D>(path) != null)
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            var palette = AssetDatabase.LoadAssetAtPath<Texture2D>(PaletteTexture);
            var glow = AssetDatabase.LoadAssetAtPath<Texture2D>(EmissiveTexture);
            if (palette == null) Debug.LogWarning($"[Art] 沒有 {PaletteTexture}：先在 Blender 跑 Art/Blender/palette/build_palette.py。");
            EnsurePaletteMaterial(PaletteMaterial, palette, null, Color.black);
            EnsurePaletteMaterial(EmissiveMaterial, palette, glow, Color.white);

            foreach (var change in ArtLibrary.Sync()) Debug.Log($"[Art] ArtLibrary：{change}");
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { ArtLibrary.ModelsRoot }))
                AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(guid), ImportAssetOptions.ForceUpdate);
            AssetDatabase.SaveAssets();
            Validate();
        }

        /// <summary>
        /// 色票材質：URP Lit、霧面（Smoothness 0、Metallic 0）、GPU Instancing。開 Emission 但一般色票不接 emission 貼圖、顏色黑：
        /// 閃白與預兆閃光用 MaterialPropertyBlock 寫 _EmissionColor，接了貼圖的話黑色的格子乘出來還是黑（docs/Art/CHARACTER_INTERFACE.md 1.3）。
        /// 只在不存在時建立；已存在時只補上空掉的貼圖。
        /// </summary>
        static Material EnsurePaletteMaterial(string path, Texture2D baseMap, Texture2D emissionMap, Color emission)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null)
            {
                if (mat.GetTexture("_BaseMap") == null && baseMap != null) mat.SetTexture("_BaseMap", baseMap);
                if (emissionMap != null && mat.GetTexture("_EmissionMap") == null) mat.SetTexture("_EmissionMap", emissionMap);
                EditorUtility.SetDirty(mat);
                return mat;
            }
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = System.IO.Path.GetFileNameWithoutExtension(path) };
            mat.SetTexture("_BaseMap", baseMap);
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_Smoothness", 0f);
            mat.SetFloat("_Metallic", 0f);
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", emission);
            if (emissionMap != null) mat.SetTexture("_EmissionMap", emissionMap);
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            mat.enableInstancing = true;
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        [MenuItem("Tools/Art/Sync Art Library")]
        public static void SyncFromMenu()
        {
            var changes = ArtLibrary.Sync();
            Debug.Log(changes.Count == 0 ? "[Art] ArtLibrary 沒有變動。" : "[Art] ArtLibrary：" + string.Join("、", changes));
        }

        [MenuItem("Tools/Art/Validate Art")]
        public static void ValidateFromMenu() => Validate();

        /// <summary>檢查美術資產，結果印到 Console；回傳問題清單（空的 = 通過）。</summary>
        public static List<string> Validate()
        {
            var issues = new List<string>();
            foreach (var path in new[] { PaletteTexture, EmissiveTexture })
            {
                if (AssetImporter.GetAtPath(path) is TextureImporter ti)
                    issues.AddRange(ArtImportPostprocessor.TextureSettingIssues(ti).Select(i => $"{path}：{i}"));
                else issues.Add($"沒有 {path}");
            }

            var palette = AssetDatabase.LoadAssetAtPath<Material>(PaletteMaterial);
            if (palette == null) issues.Add($"沒有 {PaletteMaterial}");
            else
            {
                if (palette.GetTexture("_BaseMap") == null) issues.Add("M_Palette 沒接色票貼圖");
                if (!palette.IsKeywordEnabled("_EMISSION")) issues.Add("M_Palette 沒開 Emission（閃白會失效）");
                if (palette.GetTexture("_EmissionMap") != null) issues.Add("M_Palette 接了 emission 貼圖（閃白只會亮在發光格）");
                if (palette.GetColor("_EmissionColor").maxColorComponent > 0f) issues.Add("M_Palette 的 Emission 顏色不是黑");
            }
            var emissive = AssetDatabase.LoadAssetAtPath<Material>(EmissiveMaterial);
            if (emissive == null) issues.Add($"沒有 {EmissiveMaterial}");
            else if (emissive.GetTexture("_EmissionMap") == null) issues.Add("M_Palette_Emissive 沒接 emission 貼圖");

            var library = ArtLibrary.Load();
            int models = 0;
            if (library == null) issues.Add($"沒有 {ArtLibrary.AssetPath}（跑 Tools > Art > Setup Art Assets）");
            else
            {
                foreach (var dup in library.entries.GroupBy(e => e.id).Where(g => g.Count() > 1))
                    issues.Add($"ArtLibrary 的 id 重複：{dup.Key}");
                foreach (var entry in library.entries)
                {
                    if (entry.model == null) { issues.Add($"ArtLibrary 的 {entry.id} 沒有模型"); continue; }
                    models++;
                    string path = AssetDatabase.GetAssetPath(entry.model);
                    if (AssetImporter.GetAtPath(path) is ModelImporter mi)
                        issues.AddRange(ArtImportPostprocessor.ModelSettingIssues(mi).Select(i => $"{entry.id}：{i}"));
                    foreach (var t in entry.model.GetComponentsInChildren<Transform>(true))
                    {
                        if (Quaternion.Angle(t.localRotation, Quaternion.identity) > 0.01f || (t.localScale - Vector3.one).sqrMagnitude > 1e-8f)
                            issues.Add($"{entry.id}/{t.name}：Rotation {t.localEulerAngles}、Scale {t.localScale}（Blender 端要先套用旋轉與縮放）");
                    }
                    foreach (var r in entry.model.GetComponentsInChildren<Renderer>(true))
                        foreach (var m in r.sharedMaterials)
                            if (m == null || AssetDatabase.GetAssetPath(m) == path)
                                issues.Add($"{entry.id}/{r.name}：材質「{(m != null ? m.name : "空")}」沒有換成專案裡的材質資產");
                }
            }

            var report = new StringBuilder();
            report.Append(issues.Count == 0 ? $"[Art] 檢查通過：{models} 個模型。" : $"[Art] 檢查發現 {issues.Count} 個問題：");
            foreach (var issue in issues) report.Append("\n- ").Append(issue);
            if (issues.Count == 0) Debug.Log(report.ToString());
            else Debug.LogWarning(report.ToString());
            return issues;
        }

        [MenuItem("Tools/Art/Place Axis Test")]
        public static void PlaceAxisTest()
        {
            const string rootName = "ArtAxisTest";
            var old = GameObject.Find(rootName);
            if (old != null) Undo.DestroyObjectImmediate(old);
            var root = new GameObject(rootName) { tag = "EditorOnly" };
            Undo.RegisterCreatedObjectUndo(root, "Place Axis Test");
            var scaleRef = Resources.FindObjectsOfTypeAll<Transform>()
                .FirstOrDefault(t => t.name == "ScaleRef_2m" && t.gameObject.scene.IsValid());
            root.transform.position = scaleRef != null ? scaleRef.position + Vector3.right * 1.5f : Vector3.zero;
            root.transform.position = new Vector3(root.transform.position.x, scaleRef != null ? scaleRef.position.y - scaleRef.lossyScale.y : 0f, root.transform.position.z);
            ArtLibrary.Spawn(AxisTestId, root.transform, () =>
            {
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.name = AxisTestId + "_Fallback";
                cube.transform.SetParent(root.transform, false);
                cube.transform.localPosition = Vector3.up * 0.5f;
                return cube;
            });
            Selection.activeGameObject = root;
            EditorSceneManager.MarkSceneDirty(root.scene);
        }
    }
}
