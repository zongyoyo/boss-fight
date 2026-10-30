using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace BossFight.EditorTools
{
    /// <summary>
    /// Assets/Art 的匯入規則（docs/Art/ART_STYLE.md「Unity 端規則」，取代手動套用的 Preset）：
    /// - Assets/Art/Models 的 FBX：Scale 1、用檔案的單位、不烤座標軸轉換（Blender 端已烤）、Normals = Import（保留平面著色的硬邊）、
    ///   不要切線、Animation Type = None、Read/Write 關、不加碰撞體（碰撞體由 Builder 用簡單形體建）。
    ///   材質依名稱換成專案裡的同名材質資產（Blender 的 M_Palette → Assets/Art/Materials/M_Palette.mat；
    ///   OiiaCat_Collar 這類獨立材質槽找 Assets/Models 底下的同名材質），找不到時在 Console 警告（Unity 會先內嵌一個預設材質）。
    /// - Assets/Art/Textures 的貼圖（色票）：Point、無 mipmap、不壓縮、Clamp，色票才不會滲色。
    /// Assets/Art/Models 有變動時自動同步 ArtLibrary。
    /// </summary>
    public class ArtImportPostprocessor : AssetPostprocessor
    {
        public const string ModelsRoot = "Assets/Art/Models/";
        public const string TexturesRoot = "Assets/Art/Textures/";
        public const string MaterialsRoot = "Assets/Art/Materials";
        const string LegacyMaterialsRoot = "Assets/Models";

        public override uint GetVersion() => 2;

        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(ModelsRoot, StringComparison.Ordinal)) return;
            var importer = (ModelImporter)assetImporter;
            ApplyModelSettings(importer);
        }

        /// <summary>FBX 的匯入設定（Validate 也用這份比對）。</summary>
        public static void ApplyModelSettings(ModelImporter importer)
        {
            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.bakeAxisConversion = false;
            importer.importNormals = ModelImporterNormals.Import;
            importer.importTangents = ModelImporterTangents.None;
            importer.importBlendShapes = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importVisibility = false;
            importer.animationType = ModelImporterAnimationType.None;
            importer.importAnimation = false;
            importer.isReadable = false;
            importer.addCollider = false;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
        }

        /// <summary>FBX 的設定和規則不同的項目；空的就是符合。</summary>
        public static string[] ModelSettingIssues(ModelImporter importer)
        {
            var issues = new System.Collections.Generic.List<string>();
            if (!Mathf.Approximately(importer.globalScale, 1f)) issues.Add($"Scale Factor {importer.globalScale}");
            if (importer.bakeAxisConversion) issues.Add("Bake Axis Conversion 開著");
            if (importer.importNormals != ModelImporterNormals.Import) issues.Add($"Normals {importer.importNormals}");
            if (importer.animationType != ModelImporterAnimationType.None) issues.Add($"Animation Type {importer.animationType}");
            if (importer.isReadable) issues.Add("Read/Write 開著");
            if (importer.materialImportMode != ModelImporterMaterialImportMode.ImportStandard) issues.Add($"Material Import {importer.materialImportMode}");
            return issues.ToArray();
        }

        // OnAssignMaterialModel 在材質內嵌（InPrefab）時不會被呼叫，所以匯入完再依名稱換：FBX 裡仍會留一份沒人用的內嵌材質
        void OnPostprocessModel(GameObject root)
        {
            if (!assetPath.StartsWith(ModelsRoot, StringComparison.Ordinal)) return;
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                var mats = renderer.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null) continue;
                    var found = FindMaterial(mats[i].name);
                    if (found == null)
                    {
                        Debug.LogWarning($"[Art] {assetPath} 的材質「{mats[i].name}」在專案裡找不到同名的材質資產（先跑 Tools > Art > Setup Art Assets，或建好那個材質後重新匯入 FBX）。");
                        continue;
                    }
                    context.DependsOnSourceAsset(AssetDatabase.GetAssetPath(found));
                    mats[i] = found;
                }
                renderer.sharedMaterials = mats;
            }
        }

        /// <summary>同名的材質資產：先找 Assets/Art/Materials，再找 Assets/Models（守衛的裂縫、貓的項圈這類獨立材質槽）。</summary>
        public static Material FindMaterial(string name)
        {
            foreach (var root in new[] { MaterialsRoot, LegacyMaterialsRoot })
            {
                if (!AssetDatabase.IsValidFolder(root)) continue;
                var paths = AssetDatabase.FindAssets($"{name} t:Material", new[] { root })
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .Where(p => Path.GetFileNameWithoutExtension(p) == name)
                    .ToArray();
                if (paths.Length > 1) Debug.LogWarning($"[Art] 有 {paths.Length} 個叫「{name}」的材質，用 {paths[0]}。");
                if (paths.Length > 0) return AssetDatabase.LoadAssetAtPath<Material>(paths[0]);
            }
            return null;
        }

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(TexturesRoot, StringComparison.Ordinal)) return;
            ApplyTextureSettings((TextureImporter)assetImporter);
        }

        /// <summary>色票貼圖的匯入設定（Validate 也用這份比對）。</summary>
        public static void ApplyTextureSettings(TextureImporter importer)
        {
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaSource = TextureImporterAlphaSource.None;
        }

        /// <summary>色票貼圖的設定和規則不同的項目；空的就是符合。</summary>
        public static string[] TextureSettingIssues(TextureImporter importer)
        {
            var issues = new System.Collections.Generic.List<string>();
            if (importer.filterMode != FilterMode.Point) issues.Add($"Filter {importer.filterMode}");
            if (importer.mipmapEnabled) issues.Add("mipmap 開著");
            if (importer.textureCompression != TextureImporterCompression.Uncompressed) issues.Add($"壓縮 {importer.textureCompression}");
            if (importer.wrapMode != TextureWrapMode.Clamp) issues.Add($"Wrap {importer.wrapMode}");
            return issues.ToArray();
        }

        static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            bool touched = imported.Concat(deleted).Concat(moved).Concat(movedFrom)
                .Any(p => p.StartsWith(ModelsRoot, StringComparison.Ordinal) && p.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase));
            if (!touched) return;
            EditorApplication.delayCall -= SyncLibrary;
            EditorApplication.delayCall += SyncLibrary;
        }

        static void SyncLibrary()
        {
            foreach (var change in ArtLibrary.Sync()) Debug.Log($"[Art] ArtLibrary：{change}");
        }
    }
}
