using System.Collections.Generic;
using BossFight.Boss;
using BossFight.Boss.Moves;
using BossFight.Cameras;
using BossFight.Core;
using BossFight.DebugTools;
using BossFight.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BossFight.EditorTools
{
    /// <summary>
    /// 建立 OIIA 貓的佔位 Boss（T3）：高 2.5 m 的膠囊身體，加上耳朵、眼睛、鼻子標出面向；
    /// 掛好 BossHealth、BossController、BossTelegraphFlash、LockOnTarget、實心碰撞（kinematic Rigidbody）與 Hurtbox，
    /// 以及 Moves 底下的假招式（TestFlashMove，各自帶 Hitbox）。
    /// 數值資產（BossStats、第一階段／第二階段／狂暴、假招式）只在不存在時建立並填入企劃書的數值，之後在 Inspector 改的值不會被覆蓋。
    /// 存成 Assets/Prefabs/OiiaCat.prefab，放在場地中心、面向南方的玩家出生點；刪掉 T1 的 FakeBoss，
    /// 並把鏡頭的鎖定清單、PlayerDebugOverlay 的距離對象、DebugTools 上的 BossDebugTools 接到新的 Boss。
    /// 選單 Tools > Boss > Build Boss Placeholder；重建時沿用舊 Boss 的位置與朝向。尺寸、顏色都是純視覺參數。
    /// </summary>
    public static class BossBuilder
    {
        const string RootName = "OiiaCat";
        const string PrefabPath = "Assets/Prefabs/OiiaCat.prefab";
        const string MatDir = "Assets/Models/OiiaCat/Materials";
        const string BossDir = "Assets/ScriptableObjects/Boss";
        const string PhaseDir = BossDir + "/Phases";
        const string TestMoveDir = BossDir + "/Moves/Test";
        const string ArenaDataPath = "Assets/ScriptableObjects/Arena/ArenaData.asset";

        const float BodyHeight = 2.5f;
        const float BodyRadius = 0.8f;
        const float HurtboxPadding = 0.1f;
        const float DefaultYaw = 180f; // 面向南方的玩家出生點

        [MenuItem("Tools/Boss/Build Boss Placeholder")]
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
                Debug.LogError($"[Boss] 找不到 Layer「{DaoDunDogBuilder.HurtboxLayer}」，請先在 Project Settings > Tags and Layers 加上。");
                return null;
            }
            var arena = AssetDatabase.LoadAssetAtPath<ArenaData>(ArenaDataPath);
            if (arena == null) Debug.LogWarning($"[Boss] 找不到 {ArenaDataPath}，BossController 的 arena 要手動指定。");

            var moves = GetTestMoves();
            var stats = GetStats(moves);

            Undo.SetCurrentGroupName("Build Boss Placeholder");
            int undoGroup = Undo.GetCurrentGroup();

            // 位置：沿用舊 Boss，否則放在場地中心
            Vector3 pos = Vector3.zero;
            Quaternion rot = Quaternion.Euler(0f, DefaultYaw, 0f);
            var old = Object.FindAnyObjectByType<BossController>();
            if (old != null)
            {
                pos = old.transform.position;
                rot = old.transform.rotation;
                Undo.DestroyObjectImmediate(old.gameObject);
            }
            var fake = GameObject.Find("FakeBoss");
            if (fake != null) Undo.DestroyObjectImmediate(fake);

            var root = BuildRoot(stats, arena, moves, hurtLayer);
            DaoDunDogBuilder.EnsureFolder(PrefabPath.Substring(0, PrefabPath.LastIndexOf('/')));
            PrefabUtility.SaveAsPrefabAssetAndConnect(root, PrefabPath, InteractionMode.AutomatedAction);
            Undo.RegisterCreatedObjectUndo(root, "Build Boss Placeholder");
            root.transform.SetPositionAndRotation(pos, rot);

            WireScene(root);

            Undo.CollapseUndoOperations(undoGroup);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(root.scene);
            Debug.Log($"[Boss] OIIA 貓佔位已建立：{PrefabPath}，位置 {pos}，假招式 {moves.Count} 招");
            return root;
        }

        // ---------- 數值資產 ----------

        static List<TestFlashMoveData> GetTestMoves()
        {
            return new List<TestFlashMoveData>
            {
                // 「近」的距離上限要比判定半徑 + 玩家 Hurtbox 小，Boss 走到範圍邊緣出手才打得到
                TestMove("Test_Near", "假招式·近", 1, 0f, 2.5f, 0.8f, 1f, 3f, 3f),
                TestMove("Test_Mid", "假招式·中", 1, 2.5f, 6f, 1f, 1.2f, 5f, 2f),
                TestMove("Test_Far", "假招式·遠", 1, 5f, Mathf.Infinity, 1.2f, 1.5f, 8f, 1f),
                TestMove("Test_P2_Near", "假招式·二階近", 2, 0f, 4f, 0.6f, 0.8f, 2f, 1f),
                TestMove("Test_P2_Far", "假招式·二階遠", 2, 3f, Mathf.Infinity, 0.8f, 1f, 3f, 1f),
            };
        }

        static TestFlashMoveData TestMove(string file, string label, int phase, float minDistance, float maxDistance,
                                          float telegraph, float recovery, float cooldown, float weight)
        {
            return GetOrCreate<TestFlashMoveData>($"{TestMoveDir}/{file}.asset", d =>
            {
                d.displayName = label;
                d.phase = phase;
                d.minDistance = minDistance;
                d.maxDistance = maxDistance;
                d.telegraphTime = telegraph;
                d.recoveryTime = recovery;
                d.cooldown = cooldown;
                d.weight = weight;
                d.damage = 10f;
                d.canBlock = true;
                d.canParry = true;
            });
        }

        // 企劃書「Boss 數值」表格；第一階段的開場序列先用假招式「遠 → 近 → 中」，T4 換成 迴旋衝撞 → 貓掌連拍 → 魔法陣落雷
        static BossStats GetStats(List<TestFlashMoveData> moves)
        {
            var phase1 = GetOrCreate<BossPhaseData>($"{PhaseDir}/Phase1.asset", p =>
            {
                SetPhase(p, "第一階段", 2000f, 1, 0f, 3f, 0.8f, 1.5f, 1f, 100f, 3f, 5f, 1f);
                p.openingSequence = new BossMoveData[] { moves[2], moves[0], moves[1] };
            });
            var phase2 = GetOrCreate<BossPhaseData>($"{PhaseDir}/Phase2.asset",
                p => SetPhase(p, "第二階段", 1000f, 2, 5f, 4f, 0.5f, 0.8f, 1f, 120f, 2.5f, 8f, 1.2f));
            var berserk = GetOrCreate<BossPhaseData>($"{PhaseDir}/Berserk.asset",
                p => SetPhase(p, "狂暴", 400f, 2, 0f, 4.5f, 0.3f, 0.3f, 0.75f, 120f, 2.5f, 8f, 1.3f));

            return GetOrCreate<BossStats>($"{BossDir}/BossStats.asset", s =>
            {
                s.maxHealth = 2000f;
                s.turnSpeed = 360f;
                s.phases = new[] { phase1, phase2, berserk };
            });
        }

        static void SetPhase(BossPhaseData p, string label, float threshold, int pool, float transition, float speed,
                             float intervalMin, float intervalMax, float cooldownMul, float imbalanceMax, float stun,
                             float contact, float music)
        {
            p.displayName = label;
            p.healthThreshold = threshold;
            p.movePool = pool;
            p.transitionTime = transition;
            p.moveSpeed = speed;
            p.moveIntervalMin = intervalMin;
            p.moveIntervalMax = intervalMax;
            p.cooldownMultiplier = cooldownMul;
            p.imbalanceMax = imbalanceMax;
            p.stunDuration = stun;
            p.contactDamage = contact;
            p.musicSpeed = music;
        }

        static T GetOrCreate<T>(string path, System.Action<T> init) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            DaoDunDogBuilder.EnsureFolder(path.Substring(0, path.LastIndexOf('/')));
            asset = ScriptableObject.CreateInstance<T>();
            init(asset);
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        // ---------- 物件 ----------

        static GameObject BuildRoot(BossStats stats, ArenaData arena, List<TestFlashMoveData> moveData, int hurtLayer)
        {
            var fur = GetMat("OiiaCat_Fur", new Color(0.62f, 0.6f, 0.58f), 0.2f);
            var earInner = GetMat("OiiaCat_EarInner", new Color(0.95f, 0.66f, 0.7f), 0.2f);
            var dark = GetMat("OiiaCat_Dark", new Color(0.04f, 0.04f, 0.04f), 0.8f);

            var root = new GameObject(RootName);

            // 實心碰撞：玩家和鏡頭會被擋住；Boss 用 transform 移動，所以加 kinematic Rigidbody
            var rb = root.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            var body = root.AddComponent<CapsuleCollider>();
            body.center = new Vector3(0f, BodyHeight * 0.5f, 0f);
            body.height = BodyHeight;
            body.radius = BodyRadius;

            // 外觀：+Z 是前方。膠囊頂端的半球心在 BodyHeight − BodyRadius
            var model = DaoDunDogBuilder.Node(root.transform, "Model", Vector3.zero);
            var flash = new List<Renderer>
            {
                DaoDunDogBuilder.Prim(PrimitiveType.Capsule, model, "Body", new Vector3(0f, BodyHeight * 0.5f, 0f),
                                      new Vector3(BodyRadius * 2f, BodyHeight * 0.5f, BodyRadius * 2f), fur).GetComponent<Renderer>(),
            };
            for (int side = -1; side <= 1; side += 2)
            {
                string s = side < 0 ? "L" : "R";
                flash.Add(DaoDunDogBuilder.Prim(PrimitiveType.Cube, model, "Ear" + s, new Vector3(0.4f * side, 2.42f, 0.05f),
                                                new Vector3(0.36f, 0.36f, 0.14f), fur, Quaternion.Euler(0f, 0f, 45f)).GetComponent<Renderer>());
                DaoDunDogBuilder.Prim(PrimitiveType.Cube, model, "EarInner" + s, new Vector3(0.4f * side, 2.4f, 0.12f),
                                      new Vector3(0.22f, 0.22f, 0.02f), earInner, Quaternion.Euler(0f, 0f, 45f));
                DaoDunDogBuilder.Prim(PrimitiveType.Sphere, model, "Eye" + s, new Vector3(0.25f * side, 1.95f, 0.71f),
                                      new Vector3(0.16f, 0.22f, 0.1f), dark);
            }
            DaoDunDogBuilder.Prim(PrimitiveType.Sphere, model, "Nose", new Vector3(0f, 1.8f, 0.78f), new Vector3(0.13f, 0.09f, 0.08f), earInner);

            // 受擊範圍：比實心碰撞大一點的 trigger 膠囊
            var hurt = DaoDunDogBuilder.Node(root.transform, "Hurtbox", Vector3.zero);
            hurt.gameObject.layer = hurtLayer;
            var hurtCol = hurt.gameObject.AddComponent<CapsuleCollider>();
            hurtCol.isTrigger = true;
            hurtCol.center = body.center;
            hurtCol.height = BodyHeight + HurtboxPadding * 2f;
            hurtCol.radius = BodyRadius + HurtboxPadding;
            var hurtbox = hurt.gameObject.AddComponent<Hurtbox>();

            var aimPoint = DaoDunDogBuilder.Node(root.transform, "AimPoint", new Vector3(0f, BodyHeight * 0.5f, 0f));

            var health = root.AddComponent<BossHealth>();
            DaoDunDogBuilder.SetReference(health, "stats", stats);
            DaoDunDogBuilder.SetReference(hurtbox, "owner", health);

            // 招式：每招一個子物件，Hitbox 以 Boss 根物件為攻擊方
            var movesRoot = DaoDunDogBuilder.Node(root.transform, "Moves", Vector3.zero);
            var moves = new List<BossMove>();
            foreach (var data in moveData)
            {
                var moveGo = DaoDunDogBuilder.Node(movesRoot, data.name, Vector3.zero).gameObject;
                var hitbox = moveGo.AddComponent<Hitbox>();
                var hso = new SerializedObject(hitbox);
                hso.FindProperty("targetMask").intValue = 1 << hurtLayer;
                hso.FindProperty("attacker").objectReferenceValue = root.transform;
                hso.ApplyModifiedPropertiesWithoutUndo();

                var move = moveGo.AddComponent<TestFlashMove>();
                var mso = new SerializedObject(move);
                mso.FindProperty("data").objectReferenceValue = data;
                mso.FindProperty("hitbox").objectReferenceValue = hitbox;
                mso.ApplyModifiedPropertiesWithoutUndo();
                moves.Add(move);
            }

            var controller = root.AddComponent<BossController>();
            var cso = new SerializedObject(controller);
            cso.FindProperty("stats").objectReferenceValue = stats;
            cso.FindProperty("arena").objectReferenceValue = arena;
            cso.FindProperty("body").objectReferenceValue = body;
            var list = cso.FindProperty("moves");
            list.arraySize = moves.Count;
            for (int i = 0; i < moves.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = moves[i];
            cso.ApplyModifiedPropertiesWithoutUndo();

            var flashComp = root.AddComponent<BossTelegraphFlash>();
            var fso = new SerializedObject(flashComp);
            fso.FindProperty("boss").objectReferenceValue = controller;
            var renderers = fso.FindProperty("renderers");
            renderers.arraySize = flash.Count;
            for (int i = 0; i < flash.Count; i++) renderers.GetArrayElementAtIndex(i).objectReferenceValue = flash[i];
            fso.ApplyModifiedPropertiesWithoutUndo();

            var lockOn = root.AddComponent<LockOnTarget>();
            var lso = new SerializedObject(lockOn);
            lso.FindProperty("aimPoint").objectReferenceValue = aimPoint;
            lso.FindProperty("framingRadius").floatValue = 1.3f;
            lso.ApplyModifiedPropertiesWithoutUndo();

            return root;
        }

        // 場景裡的引用：玩家、場地中心、鏡頭鎖定清單、Debug 工具
        static void WireScene(GameObject root)
        {
            var controller = root.GetComponent<BossController>();
            var player = Object.FindAnyObjectByType<PlayerController>();
            var arenaGen = Object.FindAnyObjectByType<ArenaGenerator>();
            var cso = new SerializedObject(controller);
            cso.FindProperty("target").objectReferenceValue = player != null ? player.transform : null;
            cso.FindProperty("arenaCenter").objectReferenceValue = arenaGen != null ? arenaGen.transform : null;
            cso.ApplyModifiedProperties();
            if (player == null) Debug.LogWarning("[Boss] 場景裡沒有 PlayerController，BossController 的 target 要手動指定。");

            var cam = Object.FindAnyObjectByType<CameraController>();
            if (cam != null)
            {
                var so = new SerializedObject(cam);
                var targets = so.FindProperty("lockTargets");
                for (int i = targets.arraySize - 1; i >= 0; i--)
                {
                    if (targets.GetArrayElementAtIndex(i).objectReferenceValue == null) targets.DeleteArrayElementAtIndex(i);
                }
                targets.arraySize++;
                targets.GetArrayElementAtIndex(targets.arraySize - 1).objectReferenceValue = root.GetComponent<LockOnTarget>();
                so.ApplyModifiedProperties();
            }
            else
            {
                Debug.LogWarning("[Boss] 場景裡沒有 CameraController，Boss 不會出現在鎖定清單。");
            }

            var overlay = Object.FindAnyObjectByType<PlayerDebugOverlay>();
            GameObject debugGo;
            if (overlay != null)
            {
                var oso = new SerializedObject(overlay);
                oso.FindProperty("distanceReference").objectReferenceValue = root.transform;
                oso.ApplyModifiedProperties();
                debugGo = overlay.gameObject;
            }
            else
            {
                debugGo = GameObject.Find("DebugTools");
                if (debugGo == null)
                {
                    debugGo = new GameObject("DebugTools");
                    Undo.RegisterCreatedObjectUndo(debugGo, "Create DebugTools");
                }
            }

            var tools = debugGo.GetComponent<BossDebugTools>();
            if (tools == null) tools = Undo.AddComponent<BossDebugTools>(debugGo);
            var tso = new SerializedObject(tools);
            tso.FindProperty("boss").objectReferenceValue = controller;
            tso.FindProperty("bossHealth").objectReferenceValue = root.GetComponent<BossHealth>();
            tso.FindProperty("playerHealth").objectReferenceValue = player != null ? player.GetComponent<PlayerHealth>() : null;
            tso.ApplyModifiedProperties();
        }

        /// 有 Emission 的 URP Lit 材質（預兆閃光用 MaterialPropertyBlock 改 _EmissionColor）；只在不存在時建立。
        static Material GetMat(string name, Color color, float smoothness)
        {
            DaoDunDogBuilder.EnsureFolder(MatDir);
            string path = $"{MatDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;

            mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", smoothness);
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", Color.black);
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }
    }
}
