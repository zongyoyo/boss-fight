using System.Collections.Generic;
using BossFight.Player;
using BossFight.Core;
using BossFight.DebugTools;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BossFight.EditorTools
{
    /// <summary>
    /// 建立 T2 測試用木樁：底座、木柱、橫桿、草紮的頭，擋人用的實心碰撞、Hurtbox、攻擊判定、地上的範圍圈與計時圈，
    /// 數值接 Assets/ScriptableObjects/Debug/TrainingDummyData.asset（沒有就建立），並加進 PlayerSpawner 的鎖定清單（生成時交給玩家）。
    /// 選單 Tools > Debug > Build Training Dummy；重建時沿用舊木樁的位置與朝向。
    /// 尺寸、顏色都是純視覺參數；攻擊範圍讀 TrainingDummyData。
    /// </summary>
    public static class TrainingDummyBuilder
    {
        const string DataPath = "Assets/ScriptableObjects/Debug/TrainingDummyData.asset";
        const string MatDir = "Assets/Models/TrainingDummy/Materials";
        const string RootName = "TrainingDummy";

        // 預設放在玩家出生點（南方 8 m）和中心之間，面向南邊的玩家
        static readonly Vector3 DefaultPosition = new Vector3(0f, 0f, -4.5f);
        const float DefaultYaw = 180f;

        [MenuItem("Tools/Debug/Build Training Dummy")]
        public static void BuildFromMenu()
        {
            var go = Build();
            if (go != null) Selection.activeGameObject = go;
        }

        public static GameObject Build()
        {
            int hurtLayer = LayerMask.NameToLayer(DaoDunDogBuilder.HurtboxLayer);
            if (hurtLayer < 0)
            {
                Debug.LogError($"[Dummy] 找不到 Layer「{DaoDunDogBuilder.HurtboxLayer}」，請先在 Project Settings > Tags and Layers 加上。");
                return null;
            }

            Undo.SetCurrentGroupName("Build Training Dummy");
            int undoGroup = Undo.GetCurrentGroup();

            Vector3 pos = DefaultPosition;
            Quaternion rot = Quaternion.Euler(0f, DefaultYaw, 0f);
            var old = Object.FindAnyObjectByType<TrainingDummy>();
            if (old != null)
            {
                pos = old.transform.position;
                rot = old.transform.rotation;
                Undo.DestroyObjectImmediate(old.gameObject);
            }

            var data = GetData();
            var wood = GetMat("TrainingDummy_Wood", new Color(0.55f, 0.36f, 0.2f), 0.25f);
            var straw = GetMat("TrainingDummy_Straw", new Color(0.86f, 0.72f, 0.42f), 0.15f);
            var ringMat = GetRingMat();

            var root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "Build Training Dummy");
            root.transform.SetPositionAndRotation(pos, rot);

            // 擋人用的實心碰撞（Default Layer），玩家和鏡頭都會被它擋住
            var body = root.AddComponent<CapsuleCollider>();
            body.center = new Vector3(0f, 0.9f, 0f);
            body.height = 1.8f;
            body.radius = 0.35f;

            // 外觀：以底座為軸搖晃
            var visual = DaoDunDogBuilder.Node(root.transform, "Visual", Vector3.zero);
            var flash = new List<Renderer>
            {
                DaoDunDogBuilder.Prim(PrimitiveType.Cylinder, visual, "Base", new Vector3(0f, 0.05f, 0f), new Vector3(0.8f, 0.05f, 0.8f), wood).GetComponent<Renderer>(),
                DaoDunDogBuilder.Prim(PrimitiveType.Cylinder, visual, "Post", new Vector3(0f, 0.85f, 0f), new Vector3(0.22f, 0.8f, 0.22f), wood).GetComponent<Renderer>(),
                DaoDunDogBuilder.Prim(PrimitiveType.Cylinder, visual, "Arms", new Vector3(0f, 1.25f, 0f), new Vector3(0.12f, 0.6f, 0.12f), wood,
                                      Quaternion.Euler(0f, 0f, 90f)).GetComponent<Renderer>(),
                DaoDunDogBuilder.Prim(PrimitiveType.Sphere, visual, "Head", new Vector3(0f, 1.85f, 0f), new Vector3(0.5f, 0.55f, 0.5f), straw).GetComponent<Renderer>(),
                DaoDunDogBuilder.Prim(PrimitiveType.Cylinder, visual, "Rope", new Vector3(0f, 1.6f, 0f), new Vector3(0.26f, 0.03f, 0.26f), straw).GetComponent<Renderer>(),
            };

            // 受擊範圍
            var hurt = DaoDunDogBuilder.Node(root.transform, "Hurtbox", Vector3.zero);
            hurt.gameObject.layer = hurtLayer;
            var hurtCol = hurt.gameObject.AddComponent<CapsuleCollider>();
            hurtCol.isTrigger = true;
            hurtCol.center = new Vector3(0f, 1.05f, 0f);
            hurtCol.height = 2.1f;
            hurtCol.radius = 0.45f;
            var hurtbox = hurt.gameObject.AddComponent<Hurtbox>();

            // 攻擊判定：球形範圍由 TrainingDummy 依資料設定
            var hitGo = DaoDunDogBuilder.Node(root.transform, "AttackHitbox", Vector3.zero);
            var hitbox = hitGo.gameObject.AddComponent<Hitbox>();
            var hso = new SerializedObject(hitbox);
            hso.FindProperty("targetMask").intValue = 1 << hurtLayer;
            hso.FindProperty("attacker").objectReferenceValue = root.transform;
            hso.ApplyModifiedPropertiesWithoutUndo();

            var rangeRing = CreateRing(root.transform, "RangeRing", ringMat, 0.06f);
            var timingRing = CreateRing(root.transform, "TimingRing", ringMat, 0.04f);
            var labelAnchor = DaoDunDogBuilder.Node(root.transform, "LabelAnchor", new Vector3(0f, 2.4f, 0f));
            var aimPoint = DaoDunDogBuilder.Node(root.transform, "AimPoint", new Vector3(0f, 1.2f, 0f));

            var dummy = root.AddComponent<TrainingDummy>();
            var so = new SerializedObject(dummy);
            so.FindProperty("data").objectReferenceValue = data;
            so.FindProperty("palette").objectReferenceValue = CombatFeedbackBuilder.EnsureTelegraphPalette();
            so.FindProperty("attackHitbox").objectReferenceValue = hitbox;
            var renderers = so.FindProperty("flashRenderers");
            renderers.arraySize = flash.Count;
            for (int i = 0; i < flash.Count; i++) renderers.GetArrayElementAtIndex(i).objectReferenceValue = flash[i];
            so.FindProperty("rangeRing").objectReferenceValue = rangeRing;
            so.FindProperty("timingRing").objectReferenceValue = timingRing;
            so.FindProperty("wobbleRoot").objectReferenceValue = visual;
            so.FindProperty("labelAnchor").objectReferenceValue = labelAnchor;
            so.FindProperty("viewCamera").objectReferenceValue = Camera.main;
            so.ApplyModifiedPropertiesWithoutUndo();
            DaoDunDogBuilder.SetReference(hurtbox, "owner", dummy);

            var lockOn = root.AddComponent<LockOnTarget>();
            var lso = new SerializedObject(lockOn);
            lso.FindProperty("aimPoint").objectReferenceValue = aimPoint;
            lso.FindProperty("framingRadius").floatValue = 0.8f;
            lso.ApplyModifiedPropertiesWithoutUndo();
            AddToLockTargets(lockOn);

            Undo.CollapseUndoOperations(undoGroup);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(root.scene);
            Debug.Log($"[Dummy] 木樁已建立，位置 {pos}");
            return root;
        }

        // 木樁加進 PlayerSpawner 的鎖定清單（spawner 收集場景裡所有的 LockOnTarget，生成時交給玩家）
        static void AddToLockTargets(LockOnTarget target)
        {
            if (PlayerSpawnerBuilder.Wire() == null)
                Debug.LogWarning($"[Dummy] 場景裡沒有 PlayerSpawner，{target.name} 不會出現在鎖定清單。");
        }

        static LineRenderer CreateRing(Transform parent, string name, Material mat, float width)
        {
            var t = DaoDunDogBuilder.Node(parent, name, new Vector3(0f, 0.03f, 0f));
            var lr = t.gameObject.AddComponent<LineRenderer>();
            lr.sharedMaterial = mat;
            lr.useWorldSpace = false;
            lr.loop = true;
            lr.widthMultiplier = width;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.enabled = false;
            return lr;
        }

        static TrainingDummyData GetData()
        {
            var data = AssetDatabase.LoadAssetAtPath<TrainingDummyData>(DataPath);
            if (data != null) return data;
            DaoDunDogBuilder.EnsureFolder(DataPath.Substring(0, DataPath.LastIndexOf('/')));
            data = ScriptableObject.CreateInstance<TrainingDummyData>();
            AssetDatabase.CreateAsset(data, DataPath);
            return data;
        }

        /// 有 Emission 的 URP Lit 材質（預兆閃光用 MaterialPropertyBlock 改 _EmissionColor）；只在不存在時建立。
        static Material GetMat(string name, Color color, float smoothness)
        {
            DaoDunDogBuilder.EnsureFolder(MatDir);
            string path = $"{MatDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            mat = new Material(shader) { name = name };
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", smoothness);
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", Color.black);
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        // 範圍圈用頂點色（LineRenderer 的 startColor / endColor），不受光
        static Material GetRingMat()
        {
            DaoDunDogBuilder.EnsureFolder(MatDir);
            string path = $"{MatDir}/TrainingDummy_Ring.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;
            mat = new Material(Shader.Find("Sprites/Default")) { name = "TrainingDummy_Ring" };
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }
    }
}
