using System.Collections.Generic;
using BossFight.Boss;
using BossFight.Boss.Moves;
using BossFight.Core;
using BossFight.DebugTools;
using BossFight.Hazards;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BossFight.EditorTools
{
    /// <summary>
    /// 建立 OIIA 貓的佔位 Boss（T3–T7）：高 2.5 m 的膠囊身體，加上耳朵、眼睛、鼻子標出面向，以及左右爪；
    /// 掛好 BossHealth、BossController、BossTelegraphFlash、OiiaCatPoser、BossHitFeedback、BossDamageFeedback（被打到的火花、閃白、數字）、
    /// LockOnTarget、實心碰撞（kinematic Rigidbody）與 Hurtbox，
    /// 失衡值與處決（BossStagger、ExecutionTarget）、頭暈時頭上轉的星星（BossStunVisual，T5），
    /// 強化階段的外觀（BossPhaseVisual：身上的紅色電光 ElectricAura、殘影，T7），
    /// 以及 Moves 底下的招式：第一階段 5 招（T4），和第二階段 4 個強化招（T7，沿用第一階段的招式類別、換成 Phase2 的資料），
    /// 第二階段新招 OIIA 龍捲風、幻影分身（幻影 prefab Assets/Prefabs/OiiaCatPhantom.prefab、本體的發光項圈）與終極技 OIIA 大迴旋（T8，狂暴的進場招式）。
    /// 地面提示、Hazard 與特效的 prefab 由 HazardBuilder 建立後接到招式上。
    /// 數值資產（BossStats、第一階段／第二階段／狂暴、招式）只在不存在時建立並填入企劃書的數值，之後在 Inspector 改的值不會被覆蓋；
    /// 例外：第一階段的開場序列是空的或有遺失的招式時，換成 迴旋衝撞 → 貓掌連拍 → 魔法陣落雷；狂暴的進場招式是空的時，填入 OIIA 大迴旋。
    /// 存成 Assets/Prefabs/OiiaCat.prefab，放在場地中心、面向南方的玩家出生點；刪掉 T1 的 FakeBoss，
    /// 並把玩家的鎖定清單與處決清單、PlayerDebugOverlay 的距離對象、
    /// DebugTools 上的 BossDebugTools 接到新的 Boss；場景裡的魔法陣接到幻影分身與大迴旋（IntroCutsceneBuilder 重建魔法陣後也會再接一次）；
    /// 最後重接 F12 平衡面板（BalancePanelBuilder）與打擊回饋（CombatFeedbackBuilder：BossImpactFeedback 的音效與鏡頭震動，T11）。
    /// 選單 Tools > Boss > Build Boss Placeholder；重建時沿用舊 Boss 的位置與朝向。尺寸、顏色都是純視覺參數。
    /// </summary>
    public static class BossBuilder
    {
        const string RootName = "OiiaCat";
        const string PrefabPath = "Assets/Prefabs/OiiaCat.prefab";
        const string PhantomPrefabPath = "Assets/Prefabs/OiiaCatPhantom.prefab";
        const string MatDir = "Assets/Models/OiiaCat/Materials";
        const string BossDir = "Assets/ScriptableObjects/Boss";
        const string PhaseDir = BossDir + "/Phases";
        const string Phase1MoveDir = BossDir + "/Moves/Phase1";
        const string Phase2MoveDir = BossDir + "/Moves/Phase2";
        const string ArenaDataPath = "Assets/ScriptableObjects/Arena/ArenaData.asset";
        const string FxMaterialPath = "Assets/Models/Hazards/Materials/Fx_Additive.mat";

        const float BodyHeight = 2.5f;
        const float BodyRadius = 0.8f;
        const float HurtboxPadding = 0.1f;
        const float PawSize = 0.42f;
        const float DefaultYaw = 180f; // 面向南方的玩家出生點
        const int StarCount = 5;
        const float StarHeight = 2.95f; // 頭頂（膠囊頂端 2.5 m、耳尖約 2.6 m）再高一點
        const float StarRadius = 0.55f;
        const float StarSize = 0.2f;
        const float CollarHeight = 1.45f;
        const float CollarThickness = 0.14f;

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

        /// 第二階段的數值資產：4 個強化招（類別和第一階段同一招一樣）、2 個新招與終極技。
        struct Phase2Moves
        {
            public PawSwipeData paw;
            public SpinChargeData charge;
            public SpinShockwaveData shockwave;
            public ThunderCircleData thunder;
            public TornadoData tornado;
            public PhantomPounceData phantom;
            public GrandSpinData grandSpin;
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
            var phase2 = GetPhase2Moves();
            var stats = GetStats(phase1, phase2);

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

            var root = BuildRoot(stats, arena, phase1, phase2, hazards, hurtLayer);
            DaoDunDogBuilder.EnsureFolder(PrefabPath.Substring(0, PrefabPath.LastIndexOf('/')));
            PrefabUtility.SaveAsPrefabAssetAndConnect(root, PrefabPath, InteractionMode.AutomatedAction);
            Undo.RegisterCreatedObjectUndo(root, "Build Boss Placeholder");
            root.transform.SetPositionAndRotation(pos, rot);

            WireScene(root);

            Undo.CollapseUndoOperations(undoGroup);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(root.scene);
            Debug.Log($"[Boss] OIIA 貓佔位已建立：{PrefabPath}，位置 {pos}，第一階段 5 招、第二階段 6 招與終極技");
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
                            float telegraph, float recovery, float damage, bool canBlock, bool canParry, int phase = 1)
        {
            d.displayName = label;
            d.phase = phase;
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

        // 企劃書「第二階段招式」表格（第一階段 4 招的強化版）；距離、冷卻、權重與表格沒寫的數值是 T7 計畫確認過的預設值（和第一階段同一招一樣）
        static Phase2Moves GetPhase2Moves()
        {
            var m = new Phase2Moves();
            m.paw = GetOrCreate<PawSwipeData>($"{Phase2MoveDir}/PawTriple.asset", d =>
            {
                SetMove(d, "貓掌三連拍", 0f, 2.5f, 3f, 0.4f, 1f, 18f, true, true, 2);
                d.strikeCount = 3;
                d.strikeInterval = 0.6f;
                d.hitWindow = 0.1f;
                d.hitReach = 3f;
                d.hitWidth = 2.4f;
                d.hitHeight = 2f;
                d.enhancedFinisher = true; // 第三下：24、帶電、範圍 ×1.5、紅光不可擋
                d.finisherDamage = 24f;
                d.finisherCanBlock = false;
                d.finisherCanParry = false;
                d.finisherRangeScale = 1.5f;
                d.finisherInterval = 0.8f;
                d.finisherElectric = true;
            });
            m.charge = GetOrCreate<SpinChargeData>($"{Phase2MoveDir}/ChainCharge.asset", d =>
            {
                SetMove(d, "連環迴旋衝撞", 4f, Mathf.Infinity, 6f, 0.8f, 2f, 30f, true, false, 2);
                d.trackTime = 0.5f;
                d.chargeSpeed = 14f;
                d.maxChargeTime = 3f;
                d.hitRadius = 1.1f;
                d.hitHeight = 2.2f;
                d.pathWidth = 2.2f;
                d.blockStaminaMultiplier = 2f;
                d.chargeCount = 3;
                d.rechargeTime = 0.6f;
                d.rechargeTrackTime = 0.3f;
            });
            m.shockwave = GetOrCreate<SpinShockwaveData>($"{Phase2MoveDir}/DoubleShockwave.asset", d =>
            {
                SetMove(d, "雙重衝擊波", 0f, 5f, 8f, 1.2f, 1f, 24f, false, false, 2);
                d.spinOutTime = 0.2f;
                d.startRadius = 1.2f;
                d.maxRadius = 0f;
                d.expandSpeed = 8f;
                d.bandWidth = 0.8f;
                d.hitHeight = 0.8f;
                d.telegraphRadius = 2.8f;
                d.waveCount = 2;
                d.waveInterval = 0.6f;
            });
            m.thunder = GetOrCreate<ThunderCircleData>($"{Phase2MoveDir}/TrackingThunder.asset", d =>
            {
                // 施法中 2.5 = 第一波紅圈出現 → 1.0 第一波落雷、第二波（玩家當下位置）出現 → 2.0 第二波落雷 → 0.5 破綻
                SetMove(d, "追蹤落雷", 6f, Mathf.Infinity, 10f, 0.6f, 2.5f, 24f, false, false, 2);
                d.circleCount = 5;
                d.circleRadius = 2f;
                d.warningTime = 1f;
                d.scatterMin = 2.5f;
                d.scatterMax = 5f;
                d.minSpacing = 3f;
                d.edgeMargin = 1f;
                d.hitWindow = 0.1f;
                d.hitHeight = 6f;
                d.waveCount = 2;
                d.waveInterval = 1f;
            });

            // T8 新招與終極技；企劃書沒寫的距離、冷卻與判定大小是 T8 計畫確認過的預設值
            m.tornado = GetOrCreate<TornadoData>($"{Phase2MoveDir}/Tornado.asset", d =>
            {
                SetMove(d, "OIIA 龍捲風", 0f, Mathf.Infinity, 12f, 1f, 2.5f, 10f, false, false, 2);
                d.duration = 5f;
                d.chaseSpeed = 4f;
                d.tickInterval = 0.5f;
                d.hitRadius = 1.8f;
                d.hitHeight = 4f;
                d.endImbalance = 40f;
            });
            // 取代扭屁股飛撲：距離、冷卻和飛撲本身的數值照第一階段的飛撲
            m.phantom = GetOrCreate<PhantomPounceData>($"{Phase2MoveDir}/PhantomPounce.asset", d =>
            {
                SetMove(d, "幻影分身", 3f, 6f, 5f, 1f, 1.5f, 20f, true, true, 2);
                d.flightTime = 0.5f;
                d.leapHeight = 2f;
                d.landShort = 1.2f;
                d.landingRadius = 2f;
                d.hitHeight = 2f;
                d.hitWindow = 0.1f;
                d.phantomCount = 2;
                d.spawnDistance = 3f;
                d.pounceWindup = 0.6f;
                d.pounceInterval = 0.8f;
                d.realPouncesLast = true;
            });
            // 權重 0：不會被隨機選到，只由狂暴的進場招式（或 F3）出
            m.grandSpin = GetOrCreate<GrandSpinData>($"{Phase2MoveDir}/GrandSpin.asset", d =>
            {
                SetMove(d, "OIIA 大迴旋", 0f, Mathf.Infinity, 0f, 1f, 0f, 60f, false, false, 2);
                d.weight = 0f;
                d.floatHeight = 4f;
                d.countdownTime = 4f;
                d.fallTime = 0.25f;
                d.hitHeight = 6f;
                d.hitWindow = 0.1f;
                d.landingStunTime = 3f;
                d.safeZoneCount = 2;
                d.safeZoneRadius = 2f;
                d.safeZoneMinRadius = 4f;
                d.safeZoneMaxRadius = 8.5f;
                d.safeZoneMinSpacing = 8f;
            });
            return m;
        }

        // 企劃書「Boss 數值」表格；第一階段的開場序列是 迴旋衝撞 → 貓掌連拍 → 魔法陣落雷
        static BossStats GetStats(Phase1Moves moves, Phase2Moves phase2Moves)
        {
            var opening = new BossMoveData[] { moves.charge, moves.paw, moves.thunder };
            var phase1 = GetOrCreate<BossPhaseData>($"{PhaseDir}/Phase1.asset", p =>
            {
                SetPhase(p, "第一階段", 2000f, 1, 0f, 3f, 0.8f, 1.5f, 1f, 100f, 3f, 5f, 1f);
                p.openingSequence = opening;
            });
            // 開場序列是空的或有遺失的招式（例如刪掉的 T3 假招式）時，換成正式招式
            bool broken = phase1.openingSequence == null || phase1.openingSequence.Length == 0;
            if (phase1.openingSequence != null)
            {
                foreach (var m in phase1.openingSequence) broken |= m == null;
            }
            if (broken)
            {
                phase1.openingSequence = opening;
                EditorUtility.SetDirty(phase1);
                Debug.Log("[Boss] 第一階段開場序列改為 迴旋衝撞 → 貓掌連拍 → 魔法陣落雷");
            }
            var phase2 = GetOrCreate<BossPhaseData>($"{PhaseDir}/Phase2.asset",
                p => SetPhase(p, "第二階段", 1000f, 2, 5f, 4f, 0.5f, 0.8f, 1f, 120f, 2.5f, 8f, 1.2f, true));
            var berserk = GetOrCreate<BossPhaseData>($"{PhaseDir}/Berserk.asset",
                p => SetPhase(p, "狂暴", 400f, 2, 0f, 4.5f, 0.3f, 0.3f, 0.75f, 120f, 2.5f, 8f, 1.3f, true));
            // 企劃書：血量首次降到 400 先放一次大迴旋，放完才進入狂暴
            if (berserk.entryMove == null)
            {
                berserk.entryMove = phase2Moves.grandSpin;
                EditorUtility.SetDirty(berserk);
                Debug.Log("[Boss] 狂暴的進場招式設為 OIIA 大迴旋");
            }

            return GetOrCreate<BossStats>($"{BossDir}/BossStats.asset", s =>
            {
                s.maxHealth = 2000f;
                s.turnSpeed = 360f;
                s.phases = new[] { phase1, phase2, berserk };
            });
        }

        static void SetPhase(BossPhaseData p, string label, float threshold, int pool, float transition, float speed,
                             float intervalMin, float intervalMax, float cooldownMul, float imbalanceMax, float stun,
                             float contact, float music, bool electrified = false)
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
            p.electrified = electrified;
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

        static GameObject BuildRoot(BossStats stats, ArenaData arena, Phase1Moves phase1, Phase2Moves phase2,
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
                                      ("slashPrefab", hazards.clawSlash), ("boltPrefab", hazards.bolt)),
                AddMove<SpinChargeMove>(movesRoot, phase1.charge, root.transform, hurtLayer, true, poser, hazards.telegraph,
                                        ("impactPrefab", hazards.impactRing)),
                AddMove<SpinShockwaveMove>(movesRoot, phase1.shockwave, root.transform, hurtLayer, false, poser, hazards.telegraph,
                                           ("wavePrefab", hazards.shockwave)),
                AddMove<ThunderCircleMove>(movesRoot, phase1.thunder, root.transform, hurtLayer, false, poser, null,
                                           ("strikePrefab", hazards.lightning)),
                AddMove<ButtPounceMove>(movesRoot, phase1.pounce, root.transform, hurtLayer, true, poser, hazards.telegraph,
                                        ("impactPrefab", hazards.impactRing)),
            };
            // 第二階段：同樣的招式類別，換成強化版的資料
            moves.Add(AddMove<PawSwipeMove>(movesRoot, phase2.paw, root.transform, hurtLayer, true, poser, hazards.telegraph,
                                            ("slashPrefab", hazards.clawSlash), ("boltPrefab", hazards.bolt)));
            moves.Add(AddMove<SpinChargeMove>(movesRoot, phase2.charge, root.transform, hurtLayer, true, poser, hazards.telegraph,
                                              ("impactPrefab", hazards.impactRing)));
            moves.Add(AddMove<SpinShockwaveMove>(movesRoot, phase2.shockwave, root.transform, hurtLayer, false, poser, hazards.telegraph,
                                                 ("wavePrefab", hazards.shockwave)));
            moves.Add(AddMove<ThunderCircleMove>(movesRoot, phase2.thunder, root.transform, hurtLayer, false, poser, null,
                                                 ("strikePrefab", hazards.lightning)));
            // 第二階段新招與終極技（T8）；魔法陣、倒數數字面向的鏡頭是場景物件，在 WireScene 接
            var collar = BuildCollar(model);
            moves.Add(AddMove<TornadoMove>(movesRoot, phase2.tornado, root.transform, hurtLayer, false, poser, null,
                                           ("vortexPrefab", hazards.tornado)));
            moves.Add(AddMove<PhantomPounceMove>(movesRoot, phase2.phantom, root.transform, hurtLayer, true, poser, hazards.telegraph,
                                                 ("impactPrefab", hazards.impactRing), ("phantomPrefab", BuildPhantomPrefab(hazards, hurtLayer)),
                                                 ("collar", collar)));
            moves.Add(AddMove<GrandSpinMove>(movesRoot, phase2.grandSpin, root.transform, hurtLayer, false, poser, null,
                                             ("fieldPrefab", hazards.grandSpin), ("impactPrefab", hazards.impactRing),
                                             ("countdownPrefab", hazards.damageNumber)));

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
            fso.FindProperty("palette").objectReferenceValue = CombatFeedbackBuilder.EnsureTelegraphPalette();
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

            // 失衡值、頭暈與處決（T5）；處決清單由 PlayerSpawner 交給生成的角色
            root.AddComponent<ExecutionTarget>();
            var stagger = root.AddComponent<BossStagger>();
            DaoDunDogBuilder.SetReference(stagger, "stats", stats);
            var stunVisual = root.AddComponent<BossStunVisual>();
            var vso = new SerializedObject(stunVisual);
            vso.FindProperty("stagger").objectReferenceValue = stagger;
            vso.FindProperty("poser").objectReferenceValue = poser;
            vso.FindProperty("starsRoot").objectReferenceValue = BuildStunStars(model);
            vso.ApplyModifiedPropertiesWithoutUndo();

            // 強化階段的外觀（T7）：身上的紅色電光（平常隱藏）與殘影
            var aura = BuildAura(root.transform);
            var phaseVisual = root.AddComponent<BossPhaseVisual>();
            var pvso = new SerializedObject(phaseVisual);
            pvso.FindProperty("boss").objectReferenceValue = controller;
            pvso.FindProperty("aura").objectReferenceValue = aura;
            pvso.FindProperty("model").objectReferenceValue = model;
            pvso.FindProperty("ghostMaterial").objectReferenceValue = GetGhostMaterial();
            pvso.ApplyModifiedPropertiesWithoutUndo();

            return root;
        }

        /// 第二階段身上纏繞的紅色電光（佔位）：沿身體圓柱面由下往上繞的鋸齒線，強度 0 時隱藏。
        static ElectricArcs BuildAura(Transform root)
        {
            var go = DaoDunDogBuilder.Node(root, "ElectricAura", Vector3.zero).gameObject;
            var arcs = go.AddComponent<ElectricArcs>();
            var so = new SerializedObject(arcs);
            so.FindProperty("material").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>(FxMaterialPath);
            so.FindProperty("layout").enumValueIndex = (int)ArcLayout.Wrap;
            so.FindProperty("arcCount").intValue = 6;
            so.FindProperty("radius").floatValue = BodyRadius + 0.15f;
            so.FindProperty("baseHeight").floatValue = 0.15f;
            so.FindProperty("height").floatValue = BodyHeight - 0.1f;
            so.ApplyModifiedPropertiesWithoutUndo();
            return arcs;
        }

        /// 殘影材質：半透明（Alpha 混合）的 Particles/Unlit，顏色由 BossPhaseVisual 用 MaterialPropertyBlock 改 _BaseColor；只在不存在時建立。
        static Material GetGhostMaterial()
        {
            DaoDunDogBuilder.EnsureFolder(MatDir);
            string path = $"{MatDir}/OiiaCat_Ghost.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;
            mat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")) { name = "OiiaCat_Ghost" };
            mat.SetFloat("_Surface", 1f); // Transparent
            mat.SetFloat("_Blend", 0f);   // Alpha
            mat.SetColor("_BaseColor", new Color(1f, 0.15f, 0.12f, 0.45f));
            BaseShaderGUI.SetupMaterialBlendMode(mat);
            UnityEditor.Rendering.Universal.ShaderGUI.ParticleGUI.SetMaterialKeywords(mat);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        /// 幻影分身的本體標記：脖子上一圈發光的金色項圈（扁圓柱，比身體粗一點），放在 Model 底下；平常隱藏，由招式打開。
        static GameObject BuildCollar(Transform model)
        {
            var mat = GetMat("OiiaCat_Collar", new Color(1f, 0.8f, 0.25f), 0.6f, new Color(3f, 2.2f, 0.5f));
            float diameter = (BodyRadius + 0.05f) * 2f;
            var collar = DaoDunDogBuilder.Prim(PrimitiveType.Cylinder, model, "Collar", new Vector3(0f, CollarHeight, 0f),
                                               new Vector3(diameter, CollarThickness * 0.5f, diameter), mat);
            collar.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            collar.SetActive(false);
            return collar;
        }

        /// 幻影分身的幻影（佔位）：本體的身體、耳朵、爪子，全部換成半透明的殘影材質；OiiaCatPoser 做伏低與飛撲姿勢，
        /// 自己的 Hurtbox（一擊即碎）與落地的 Hitbox（攻擊方是幻影自己）。存成 Assets/Prefabs/OiiaCatPhantom.prefab（每次重建覆寫）。
        static BossPhantom BuildPhantomPrefab(HazardBuilder.Result hazards, int hurtLayer)
        {
            var ghost = GetGhostMaterial();
            var root = new GameObject("OiiaCatPhantom");
            var model = DaoDunDogBuilder.Node(root.transform, "Model", Vector3.zero);
            var renderers = new List<Renderer>
            {
                DaoDunDogBuilder.Prim(PrimitiveType.Capsule, model, "Body", new Vector3(0f, BodyHeight * 0.5f, 0f),
                                      new Vector3(BodyRadius * 2f, BodyHeight * 0.5f, BodyRadius * 2f), ghost).GetComponent<Renderer>(),
            };
            for (int side = -1; side <= 1; side += 2)
            {
                renderers.Add(DaoDunDogBuilder.Prim(PrimitiveType.Cube, model, side < 0 ? "EarL" : "EarR", new Vector3(0.4f * side, 2.42f, 0.05f),
                                                    new Vector3(0.36f, 0.36f, 0.14f), ghost, Quaternion.Euler(0f, 0f, 45f)).GetComponent<Renderer>());
            }
            var poser = root.AddComponent<OiiaCatPoser>();
            var paws = new Transform[2];
            for (int side = -1; side <= 1; side += 2)
            {
                var rest = poser.pawRest;
                var paw = DaoDunDogBuilder.Prim(PrimitiveType.Sphere, model, side < 0 ? "PawL" : "PawR",
                                                new Vector3(rest.x * side, rest.y, rest.z), Vector3.one * PawSize, ghost);
                renderers.Add(paw.GetComponent<Renderer>());
                paws[side < 0 ? 0 : 1] = paw.transform;
            }
            foreach (var r in renderers) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            poser.model = model;
            poser.pawL = paws[0];
            poser.pawR = paws[1];

            var hitbox = HazardBuilder.AddHitbox(root, hurtLayer);
            DaoDunDogBuilder.SetReference(hitbox, "attacker", root.transform);

            var hurt = DaoDunDogBuilder.Node(root.transform, "Hurtbox", Vector3.zero);
            hurt.gameObject.layer = hurtLayer;
            var hurtCol = hurt.gameObject.AddComponent<CapsuleCollider>();
            hurtCol.isTrigger = true;
            hurtCol.center = new Vector3(0f, BodyHeight * 0.5f, 0f);
            hurtCol.height = BodyHeight + HurtboxPadding * 2f;
            hurtCol.radius = BodyRadius + HurtboxPadding;
            var hurtbox = hurt.gameObject.AddComponent<Hurtbox>();

            var phantom = root.AddComponent<BossPhantom>();
            DaoDunDogBuilder.SetReference(hurtbox, "owner", phantom);
            var so = new SerializedObject(phantom);
            so.FindProperty("poser").objectReferenceValue = poser;
            so.FindProperty("hitbox").objectReferenceValue = hitbox;
            so.FindProperty("hurtCollider").objectReferenceValue = hurtCol;
            so.FindProperty("model").objectReferenceValue = model.gameObject;
            so.FindProperty("shatterPrefab").objectReferenceValue = hazards.hitSpark;
            var list = so.FindProperty("renderers");
            list.arraySize = renderers.Count;
            for (int i = 0; i < renderers.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = renderers[i];
            so.ApplyModifiedPropertiesWithoutUndo();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PhantomPrefabPath);
            Object.DestroyImmediate(root);
            return prefab.GetComponent<BossPhantom>();
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

        // 場景裡的引用：場地中心、Debug 工具；玩家（BossController.SetTarget）與玩家的鎖定、處決清單由 PlayerSpawner 執行時設定（最後跑 PlayerSpawnerBuilder.Wire）
        static void WireScene(GameObject root)
        {
            var controller = root.GetComponent<BossController>();
            var arenaGen = Object.FindAnyObjectByType<ArenaGenerator>();
            var cso = new SerializedObject(controller);
            cso.FindProperty("arenaCenter").objectReferenceValue = arenaGen != null ? arenaGen.transform : null;
            cso.ApplyModifiedProperties();
            if (PlayerSpawnerBuilder.Find() == null) Debug.LogWarning("[Boss] 場景裡沒有 PlayerSpawner（Tools > Player > Build DaoDun Dog），Boss 不會有目標。");

            var mainCamera = Camera.main;
            var fso = new SerializedObject(root.GetComponent<BossDamageFeedback>());
            fso.FindProperty("viewCamera").objectReferenceValue = mainCamera != null ? mainCamera.transform : null;
            fso.ApplyModifiedProperties();
            if (mainCamera == null) Debug.LogWarning("[Boss] 場景裡沒有 Main Camera，BossDamageFeedback 的 viewCamera 要手動指定（傷害數字會不面向鏡頭）。");
            foreach (var spin in root.GetComponentsInChildren<GrandSpinMove>(true))
            {
                var sso = new SerializedObject(spin);
                sso.FindProperty("viewCamera").objectReferenceValue = mainCamera != null ? mainCamera.transform : null;
                sso.ApplyModifiedProperties();
            }
            WireMagicCircle(controller, Object.FindAnyObjectByType<MagicCircle>());

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
            tso.FindProperty("stagger").objectReferenceValue = root.GetComponent<BossStagger>();
            tso.ApplyModifiedProperties();

            PlayerSpawnerBuilder.Wire(); // Boss、鎖定與處決目標、BossDebugTools 接到 spawner
            BalancePanelBuilder.Wire();
            CombatFeedbackBuilder.Wire();
            PauseMenuBuilder.Wire(); // 開發者模式開關的 Debug 工具清單
        }

        /// 把場景的魔法陣接到會用它的招式（幻影分身的閃爍、大迴旋的安全區）；IntroCutsceneBuilder 重建魔法陣後也會呼叫。circle 為 null 時清掉。
        internal static void WireMagicCircle(BossController boss, MagicCircle circle)
        {
            if (boss == null) return;
            foreach (var move in boss.GetComponentsInChildren<BossMove>(true))
            {
                var so = new SerializedObject(move);
                var prop = so.FindProperty("magicCircle");
                if (prop == null) continue;
                prop.objectReferenceValue = circle;
                so.ApplyModifiedProperties();
            }
            if (circle == null) Debug.LogWarning("[Boss] 場景裡沒有 MagicCircle（先跑 Tools > Cutscene > Build Intro），幻影分身不會閃法陣、大迴旋的安全區不會熄滅符文。");
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
