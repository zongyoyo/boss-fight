using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace BossFight.EditorTools
{
    /// <summary>
    /// Tools > Art：美術管線（docs/Art/ART_STYLE.md）的 Unity 端工具。
    /// - Setup Art Assets：建資料夾、套用色票貼圖的匯入設定、建 M_Palette／M_Palette_Emissive（只在不存在時建，之後在資產調）、
    ///   建雙面的 M_Palette_Foliage（草、葉片）、三個場景共用的基礎後處理 VP_LowPolyBase（只在不存在時建）、同步 ArtLibrary、重新匯入 FBX（材質依名稱換成專案裡的材質）。
    ///   Blender 第一次輸出色票或改了 palette.json 後跑一次。
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
        public const string FoliageMaterial = ArtImportPostprocessor.MaterialsRoot + "/M_Palette_Foliage.mat";
        public const string BaseVolumeProfile = "Assets/Art/Settings/VP_LowPolyBase.asset";
        const string AxisTestId = "SM_Test_AxisCube";
        const string PaletteJson = "Art/Blender/palette/palette.json";

        static Dictionary<string, Vector2Int> paletteGroups;

        /// <summary>
        /// 色格「組名.l／.m／.d」在色票貼圖上的 UV（格子中心），給 Builder 程序產生的網格（地面）用，規則同 Blender 的 kit.cell_uv。
        /// 色格座標讀 Art/Blender/palette/palette.json（每組佔同一列連續 3 格：亮、中、暗）。
        /// </summary>
        public static Vector2 PaletteUV(string cell)
        {
            if (paletteGroups == null)
            {
                paletteGroups = new Dictionary<string, Vector2Int>();
                var json = System.IO.File.ReadAllText(System.IO.Path.Combine(Application.dataPath, "..", PaletteJson));
                foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(
                             json, "\"(\\w+)\"\\s*:\\s*\\{\\s*\"row\"\\s*:\\s*(\\d+)\\s*,\\s*\"col\"\\s*:\\s*(\\d+)"))
                    paletteGroups[m.Groups[1].Value] = new Vector2Int(int.Parse(m.Groups[3].Value), int.Parse(m.Groups[2].Value));
            }
            int dot = cell.LastIndexOf('.');
            if (dot < 0 || !paletteGroups.TryGetValue(cell.Substring(0, dot), out var group))
                throw new System.ArgumentException($"色票沒有 {cell}（看 {PaletteJson}）");
            int tier = "lmd".IndexOf(cell[dot + 1]);
            if (tier < 0) throw new System.ArgumentException($"色格 {cell} 的階要是 l、m、d");
            const float grid = 16f; // palette.json 的 grid
            return new Vector2((group.x + tier + 0.5f) / grid, 1f - (group.y + 0.5f) / grid);
        }

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
            // 草、葉片這類單片網格：同一張色票、雙面（法線在 Blender 端設成朝上，正反面一樣亮）
            var foliage = EnsurePaletteMaterial(FoliageMaterial, palette, null, Color.black);
            if (foliage.GetFloat("_Cull") != (float)CullMode.Off)
            {
                foliage.SetFloat("_Cull", (float)CullMode.Off);
                foliage.doubleSidedGI = true;
                EditorUtility.SetDirty(foliage);
            }
            EnsureBaseVolumeProfile();

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

        /// <summary>
        /// 三個場景共用的基礎後處理（ART_STYLE「光照與後處理」）：Neutral 色調映射、對比與飽和 +10、Split Toning 亮部暖暗部藍、
        /// 低強度高門檻的 Bloom（只讓發光的東西亮）、暗角 0.2。場景的 Volume 優先度 0 用它，場景自己的覆寫放在更高優先度。
        /// 只在不存在時建立，之後在資產調。
        /// </summary>
        public static VolumeProfile EnsureBaseVolumeProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(BaseVolumeProfile);
            if (profile != null) return profile;
            DaoDunDogBuilder.EnsureFolder(BaseVolumeProfile.Substring(0, BaseVolumeProfile.LastIndexOf('/')));
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, BaseVolumeProfile);
            profile.Add<Tonemapping>().mode.Override(TonemappingMode.Neutral);
            var color = profile.Add<ColorAdjustments>();
            color.contrast.Override(10f);
            color.saturation.Override(10f);
            var split = profile.Add<SplitToning>();
            split.highlights.Override(new Color(0.6f, 0.53f, 0.42f));
            split.shadows.Override(new Color(0.42f, 0.5f, 0.62f));
            var bloom = profile.Add<Bloom>();
            bloom.intensity.Override(0.3f);
            bloom.threshold.Override(1.1f);
            var vignette = profile.Add<Vignette>();
            vignette.intensity.Override(0.2f);
            vignette.smoothness.Override(0.4f);
            foreach (var component in profile.components) AssetDatabase.AddObjectToAsset(component, profile);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            return profile;
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
            var foliageMat = AssetDatabase.LoadAssetAtPath<Material>(FoliageMaterial);
            if (foliageMat == null) issues.Add($"沒有 {FoliageMaterial}");
            else if (foliageMat.GetFloat("_Cull") != (float)CullMode.Off) issues.Add("M_Palette_Foliage 不是雙面（Render Face 要 Both）");

            if (AssetDatabase.LoadAssetAtPath<VolumeProfile>(BaseVolumeProfile) == null) issues.Add($"沒有 {BaseVolumeProfile}（跑 Tools > Art > Setup Art Assets）");

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
