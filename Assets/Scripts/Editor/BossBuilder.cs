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
    /// 建立 OIIA 貓的佔位 Boss（T3–T5）：高 2.5 m 的膠囊身體，加上耳朵、眼睛、鼻子標出面向，以及左右爪；
    /// 掛好 BossHealth、BossController、BossTelegraphFlash、OiiaCatPoser、BossHitFeedback、BossDamageFeedback（被打到的火花、閃白、數字）、
    /// LockOnTarget、實心碰撞（kinematic Rigidbody）與 Hurtbox，
    /// 失衡值與處決（BossStagger、ExecutionTarget）、頭暈時頭上轉的星星（BossStunVisual，T5），
    /// 以及 Moves 底下的招式：第一階段 5 招（T4），和第二階段的 2 個假招式（TestFlashMove，T7 換成正式招式）。
    /// 地面提示、Hazard 與特效的 prefab 由 HazardBuilder 建立後接到招式上。
    /// 數值資產（BossStats、第一階段／第二階段／狂暴、招式）只在不存在時建立並填入企劃書的數值，之後在 Inspector 改的值不會被覆蓋；
    /// 唯一例外是第一階段的開場序列還是 T3 的假招式時，換成 迴旋衝撞 → 貓掌連拍 → 魔法陣落雷。
    /// 存成 Assets/Prefabs/OiiaCat.prefab，放在場地中心、面向南方的玩家出生點；刪掉 T1 的 FakeBoss，
    /// 並把鏡頭的鎖定清單、玩家的處決清單、BossStagger 訂閱的玩家 PlayerHealth、PlayerDebugOverlay 的距離對象、
    /// DebugTools 上的 BossDebugTools 接到新的 Boss。
    /// 選單 Tools > Boss > Build Boss Placeholder；重建時沿用舊 Boss 的位置與朝向。尺寸、顏色都是純視覺參數。
    /// </summary>
    public static class BossBuilder
    {
        const string RootName = "OiiaCat";
        const string PrefabPath = "Assets/Prefabs/OiiaCat.prefab";
        const string MatDir = "Assets/Models/OiiaCat/Materials";
        const string BossDir = "Assets/ScriptableObjects/Boss";
        const string PhaseDir = BossDir + "/Phases";
        const string Phase1MoveDir = BossDir + "/Moves/Phase1";
        const string TestMoveDir = BossDir + "/Moves/Test";
        const string ArenaDataPath = "Assets/ScriptableObjects/Arena/ArenaData.asset";

        const float BodyHeight = 2.5f;
        const float BodyRadius = 0.8f;
        const float HurtboxPadding = 0.1f;
        const float PawSize = 0.42f;
        const float DefaultYaw = 180f; // 面向南方的玩家出生點
        const int StarCount = 5;
        const float StarHeight = 2.95f; // 頭頂（膠囊頂端 2.5 m、耳尖約 2.6 m）再高一點
        const float StarRadius = 0.55f;
        const float StarSize = 0.2f;

        /// 第一階段 5 招的數值資產。
        struct Phase1Moves
        {
            public PawSwipeData paw;
            public SpinChargeData charge;
            public SpinShockwaveData shockwave;
            public ThunderCircleData thunder;
            public ButtPounceData pounce;

            public BossMoveData[] All => new BossMoveData[] { paw, charge, shockwave, thunder, pounce };
        }

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

            var hazards = HazardBuilder.Build();
            if (hazards.telegraph == null) return null;
            var phase1 = GetPhase1Moves();
            var testMoves = GetTestMoves();
            var stats = GetStats(phase1);

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

            var root = BuildRoot(stats, arena, phase1, testMoves, hazards, hurtLayer);
            DaoDunDogBuilder.EnsureFolder(PrefabPath.Substring(0, PrefabPath.LastIndexOf('/')));
            PrefabUtility.SaveAsPrefabAssetAndConnect(root, PrefabPath, InteractionMode.AutomatedAction);
            Undo.RegisterCreatedObjectUndo(root, "Build Boss Placeholder");
            root.transform.SetPositionAndRotation(pos, rot);

            WireScene(root);

            Undo.CollapseUndoOperations(undoGroup);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(root.scene);
            Debug.Log($"[Boss] OIIA 貓佔位已建立：{PrefabPath}，位置 {pos}，第一階段 5 招、第二階段假招式 {testMoves.Count} 招");
            return root;
        }

        // ---------- 數值資產 ----------

        // 企劃書「第一階段招式」表格；表格沒寫的（速度、半徑、間隔）是 T4 計畫確認過的預設值
        static Phase1Moves GetPhase1Moves()
        {
            var m = new Phase1Moves();
            m.paw = GetOrCreate<PawSwipeData>($"{Phase1MoveDir}/PawSwipe.asset", d =>
            {
                // 距離上限 2.5 要比判定深度 3.0 + 玩家 Hurtbox 0.45 小，Boss 走到範圍邊緣出手才打得到
                SetMove(d, "貓掌連拍", 0f, 2.5f, 3f, 0.5f, 1.2f, 15f, true, true);
                d.strikeCount = 2;
                d.strikeInterval = 0.6f;
                d.hitWindow = 0.1f;
                d.hitReach = 3f;
                d.hitWidth = 2.4f;
                d.hitHeight = 2f;
            });
            m.charge = GetOrCreate<SpinChargeData>($"{Phase1MoveDir}/SpinCharge.asset", d =>
            {
                SetMove(d, "OIIA 迴旋衝撞", 4f, Mathf.Infinity, 6f, 1f, 2f, 25f, true, false);
                d.trackTime = 0.7f;
                d.chargeSpeed = 14f;
                d.maxChargeTime = 3f;
                d.hitRadius = 1.1f;
                d.hitHeight = 2.2f;
                d.pathWidth = 2.2f;
                d.blockStaminaMultiplier = 2f;
            });
            m.shockwave = GetOrCreate<SpinShockwaveData>($"{Phase1MoveDir}/SpinShockwave.asset", d =>
            {
                SetMove(d, "旋轉衝擊波", 0f, 5f, 8f, 1.5f, 1f, 20f, false, false);
                d.spinOutTime = 0.2f;
                d.startRadius = 1.2f;
                d.maxRadius = 0f;
                d.expandSpeed = 8f;
                d.bandWidth = 0.8f;
                d.hitHeight = 0.8f; // 比跳躍高度 1.2 低，抓準時機可以跳過
                d.telegraphRadius = 2.8f;
            });
            m.thunder = GetOrCreate<ThunderCircleData>($"{Phase1MoveDir}/ThunderCircle.asset", d =>
            {
                // recoveryTime 2.0 = 施法中 Boss 不動的時間，從紅圈出現算起
                SetMove(d, "魔法陣落雷", 6f, Mathf.Infinity, 10f, 0.6f, 2f, 20f, false, false);
                d.circleCount = 3;
                d.circleRadius = 2f;
                d.warningTime = 1f;
                d.scatterMin = 2.5f;
                d.scatterMax = 5f;
                d.minSpacing = 3f;
                d.edgeMargin = 1f;
                d.hitWindow = 0.1f;
                d.hitHeight = 6f;
            });
            m.pounce = GetOrCreate<ButtPounceData>($"{Phase1MoveDir}/ButtPounce.asset", d =>
            {
                SetMove(d, "扭屁股飛撲", 3f, 6f, 5f, 0.6f, 1.5f, 25f, true, true);
                d.flightTime = 0.5f;
                d.leapHeight = 2f;
                d.landShort = 1.2f;
                d.landingRadius = 2f;
                d.hitHeight = 2f;
                d.hitWindow = 0.1f;
            });
            return m;
        }

        static void SetMove(BossMoveData d, string label, float minDistance, float maxDistance, float cooldown,
                            float telegraph, float recovery, float damage, bool canBlock, bool canParry)
        {
            d.displayName = label;
            d.phase = 1;
            d.minDistance = minDistance;
            d.maxDistance = maxDistance;
            d.cooldown = cooldown;
            d.weight = 1f;
            d.telegraphTime = telegraph;
            d.recoveryTime = recovery;
            d.damage = damage;
            d.canBlock = canBlock;
            d.canParry = canParry;
        }

        // 第二階段的假招式：T7 做正式招式前，讓轉階段後的 Boss 還有招可出
        static List<TestFlashMoveData> GetTestMoves()
        {
            return new List<TestFlashMoveData>
            {
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

        // 企劃書「Boss 數值」表格；第一階段的開場序列是 迴旋衝撞 → 貓掌連拍 → 魔法陣落雷
        static BossStats GetStats(Phase1Moves moves)
        {
            var opening = new BossMoveData[] { moves.charge, moves.paw, moves.thunder };
            var phase1 = GetOrCreate<BossPhaseData>($"{PhaseDir}/Phase1.asset", p =>
            {
                SetPhase(p, "第一階段", 2000f, 1, 0f, 3f, 0.8f, 1.5f, 1f, 100f, 3f, 5f, 1f);
                p.openingSequence = opening;
            });
            // T3 的開場序列是假招式「遠 → 近 → 中」，換成正式招式
            bool hasTestMove = phase1.openingSequence == null || phase1.openingSequence.Length == 0;
            if (phase1.openingSequence != null)
            {
                foreach (var m in phase1.openingSequence) hasTestMove |= m == null || m is TestFlashMoveData;
            }
            if (hasTestMove)
            {
                phase1.openingSequence = opening;
                EditorUtility.SetDirty(phase1);
                Debug.Log("[Boss] 第一階段開場序列改為 迴旋衝撞 → 貓掌連拍 → 魔法陣落雷");
            }
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

        static GameObject BuildRoot(BossStats stats, ArenaData arena, Phase1Moves phase1, List<TestFlashMoveData> testMoves,
                                    HazardBuilder.Result hazards, int hurtLayer)
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

            // 左右爪：OiiaCatPoser 舉爪、拍下；預兆時跟身體一起閃光
            var poser = root.AddComponent<OiiaCatPoser>();
            var paws = new Transform[2];
            for (int side = -1; side <= 1; side += 2)
            {
                var rest = poser.pawRest;
                var paw = DaoDunDogBuilder.Prim(PrimitiveType.Sphere, model, side < 0 ? "PawL" : "PawR",
                                                new Vector3(rest.x * side, rest.y, rest.z), Vector3.one * PawSize, fur);
                flash.Add(paw.GetComponent<Renderer>());
                paws[side < 0 ? 0 : 1] = paw.transform;
            }
            poser.model = model;
            poser.pawL = paws[0];
            poser.pawR = paws[1];

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
            var moves = new List<BossMove>
            {
                AddMove<PawSwipeMove>(movesRoot, phase1.paw, root.transform, hurtLayer, true, poser, hazards.telegraph,
                                      ("slashPrefab", hazards.clawSlash)),
                AddMove<SpinChargeMove>(movesRoot, phase1.charge, root.transform, hurtLayer, true, poser, hazards.telegraph,
                                        ("impactPrefab", hazards.impactRing)),
                AddMove<SpinShockwaveMove>(movesRoot, phase1.shockwave, root.transform, hurtLayer, false, poser, hazards.telegraph,
                                           ("wavePrefab", hazards.shockwave)),
                AddMove<ThunderCircleMove>(movesRoot, phase1.thunder, root.transform, hurtLayer, false, poser, null,
                                           ("strikePrefab", hazards.lightning)),
                AddMove<ButtPounceMove>(movesRoot, phase1.pounce, root.transform, hurtLayer, true, poser, hazards.telegraph,
                                        ("impactPrefab", hazards.impactRing)),
            };
            foreach (var data in testMoves)
                moves.Add(AddMove<TestFlashMove>(movesRoot, data, root.transform, hurtLayer, true, null, null));

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

            var feedback = root.AddComponent<BossHitFeedback>();
            DaoDunDogBuilder.SetReference(feedback, "boss", controller);
            DaoDunDogBuilder.SetReference(feedback, "sparkPrefab", hazards.hitSpark);

            // Boss 被打到的反饋：火花、閃白、往後仰、傷害數字；數字面向的鏡頭在 WireScene 接
            var damageFeedback = root.AddComponent<BossDamageFeedback>();
            var dso = new SerializedObject(damageFeedback);
            dso.FindProperty("health").objectReferenceValue = health;
            dso.FindProperty("flash").objectReferenceValue = flashComp;
            dso.FindProperty("poser").objectReferenceValue = poser;
            dso.FindProperty("sparkPrefab").objectReferenceValue = hazards.hitSpark;
            dso.FindProperty("numberPrefab").objectReferenceValue = hazards.damageNumber;
            dso.ApplyModifiedPropertiesWithoutUndo();

            // 失衡值、頭暈與處決（T5）；玩家的 PlayerHealth 是場景物件，在 WireScene 接
            root.AddComponent<ExecutionTarget>();
            var stagger = root.AddComponent<BossStagger>();
            DaoDunDogBuilder.SetReference(stagger, "stats", stats);
            var stunVisual = root.AddComponent<BossStunVisual>();
            var vso = new SerializedObject(stunVisual);
            vso.FindProperty("stagger").objectReferenceValue = stagger;
            vso.FindProperty("poser").objectReferenceValue = poser;
            vso.FindProperty("starsRoot").objectReferenceValue = BuildStunStars(model);
            vso.ApplyModifiedPropertiesWithoutUndo();

            return root;
        }

        /// 頭暈時頭上轉的星星（佔位）：一圈發光的小方塊，放在 Model 底下跟著身體一起晃；平常隱藏。
        static Transform BuildStunStars(Transform model)
        {
            var mat = GetMat("OiiaCat_Star", new Color(1f, 0.85f, 0.2f), 0.5f, new Color(2.5f, 2f, 0.4f));
            var root = DaoDunDogBuilder.Node(model, "StunStars", new Vector3(0f, StarHeight, 0f));
            for (int i = 0; i < StarCount; i++)
            {
                float a = i * Mathf.PI * 2f / StarCount;
                var star = DaoDunDogBuilder.Prim(PrimitiveType.Cube, root, $"Star{i + 1}",
                                                 new Vector3(Mathf.Cos(a) * StarRadius, 0f, Mathf.Sin(a) * StarRadius),
                                                 Vector3.one * StarSize, mat, Quaternion.Euler(45f, 0f, 45f));
                star.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            root.gameObject.SetActive(false);
            return root;
        }

        /// 一招一個子物件：要的話加 Hitbox（以 Boss 根物件為攻擊方），再掛招式元件並接上資料、姿勢與 prefab。
        static T AddMove<T>(Transform movesRoot, BossMoveData data, Transform attacker, int hurtLayer, bool withHitbox,
                            OiiaCatPoser poser, Object telegraphPrefab, params (string field, Object value)[] extra)
            where T : BossMove
        {
            var go = DaoDunDogBuilder.Node(movesRoot, data.name, Vector3.zero).gameObject;
            Hitbox hitbox = null;
            if (withHitbox)
            {
                hitbox = HazardBuilder.AddHitbox(go, hurtLayer);
                DaoDunDogBuilder.SetReference(hitbox, "attacker", attacker);
            }

            var move = go.AddComponent<T>();
            var so = new SerializedObject(move);
            so.FindProperty("data").objectReferenceValue = data;
            if (hitbox != null) so.FindProperty("hitbox").objectReferenceValue = hitbox;
            if (poser != null) so.FindProperty("poser").objectReferenceValue = poser;
            if (telegraphPrefab != null) so.FindProperty("telegraphPrefab").objectReferenceValue = telegraphPrefab;
            foreach (var (field, value) in extra) so.FindProperty(field).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
            return move;
        }

        // 場景裡的引用：玩家、場地中心、鏡頭鎖定清單、玩家的處決清單與盾反事件、Debug 工具
        static void WireScene(GameObject root)
        {
            var controller = root.GetComponent<BossController>();
            var player = Object.FindAnyObjectByType<PlayerController>();
            var playerHealth = player != null ? player.GetComponent<PlayerHealth>() : null;
            var arenaGen = Object.FindAnyObjectByType<ArenaGenerator>();
            var cso = new SerializedObject(controller);
            cso.FindProperty("target").objectReferenceValue = player != null ? player.transform : null;
            cso.FindProperty("arenaCenter").objectReferenceValue = arenaGen != null ? arenaGen.transform : null;
            cso.ApplyModifiedProperties();
            if (player == null) Debug.LogWarning("[Boss] 場景裡沒有 PlayerController，BossController 的 target、BossStagger 的 player 要手動指定。");

            var mainCamera = Camera.main;
            var fso = new SerializedObject(root.GetComponent<BossDamageFeedback>());
            fso.FindProperty("viewCamera").objectReferenceValue = mainCamera != null ? mainCamera.transform : null;
            fso.ApplyModifiedProperties();
            if (mainCamera == null) Debug.LogWarning("[Boss] 場景裡沒有 Main Camera，BossDamageFeedback 的 viewCamera 要手動指定（傷害數字會不面向鏡頭）。");

            var stagger = root.GetComponent<BossStagger>();
            var sso = new SerializedObject(stagger);
            sso.FindProperty("player").objectReferenceValue = playerHealth;
            sso.ApplyModifiedProperties();
            if (player != null)
            {
                var pso = new SerializedObject(player);
                AddToList(pso.FindProperty("executionTargets"), root.GetComponent<ExecutionTarget>());
                pso.ApplyModifiedProperties();
            }

            var cam = Object.FindAnyObjectByType<CameraController>();
            if (cam != null)
            {
                var so = new SerializedObject(cam);
                AddToList(so.FindProperty("lockTargets"), root.GetComponent<LockOnTarget>());
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
            tso.FindProperty("stagger").objectReferenceValue = stagger;
            tso.FindProperty("playerHealth").objectReferenceValue = playerHealth;
            tso.ApplyModifiedProperties();
        }

        // 清掉清單裡已經失效的項目（被刪掉的舊 Boss），再加上新的
        static void AddToList(SerializedProperty list, Object value)
        {
            for (int i = list.arraySize - 1; i >= 0; i--)
            {
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == null) list.DeleteArrayElementAtIndex(i);
            }
            list.arraySize++;
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = value;
        }

        /// 有 Emission 的 URP Lit 材質（預兆閃光用 MaterialPropertyBlock 改 _EmissionColor；星星自己發光）；只在不存在時建立。
        static Material GetMat(string name, Color color, float smoothness, Color? emission = null)
        {
            DaoDunDogBuilder.EnsureFolder(MatDir);
            string path = $"{MatDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;

            mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", smoothness);
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", emission ?? Color.black);
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }
    }
}
