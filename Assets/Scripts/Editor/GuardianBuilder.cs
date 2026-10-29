using System;
using System.Collections.Generic;
using System.Linq;
using BossFight.Battle;
using BossFight.Boss;
using BossFight.Boss.Moves;
using BossFight.Core;
using BossFight.Hazards;
using BossFight.Progression;
using BossFight.UI;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace BossFight.EditorTools
{
    /// <summary>
    /// Tools > Prologue > Build Guardian：在 Prologue 場景重建大樹守衛一階段（T28）與它的接線，不動地形、教學與鏡頭。
    /// 1. 資產（沒有才建，之後在資產調）：BossStats、一階段 BossPhaseData、Arena（半徑照 PrologueLayout 算，每次更新）、
    ///    一階段 6 招與萬根齊發的 GuardianMoveData、GuardianBattleData、BossDefinition（加進 GameDatabase）、守衛的 BattleUIData 複本。
    /// 2. prefab（每次重建）：範圍攻擊 GuardianAreaStrike（GroundTelegraph 預兆、種子爆炸、葉刃拖尾）、Fx_GuardianArea、守衛本體 TreeGuardian
    ///    （Default layer，D6；模型、四條藤、鎖定構圖、受擊、預兆閃光、打擊回饋、GuardianPoser）。
    /// 3. 場景：GuardianBattle（GuardianPhaseController、PrologueOutcome）與守衛實例、GuardianUI（Boss 面板、提示、處決標記）。
    /// 4. 已經有二階段時接著跑 GuardianPhaseTwoBuilder.Build（二階段、結局、演出接線），最後 DebugToolsBuilder.Wire 接開發者工具。
    /// 開戰只走登場演出（PrologueCutsceneFlow）或開發者工具，這裡不接「教學完成就開戰」。
    /// </summary>
    public static class GuardianBuilder
    {
        public const string Root = "Assets/ScriptableObjects/Boss/Guardian";
        public const string PrefabPath = "Assets/Prefabs/TreeGuardian.prefab";
        public const string AreaPath = "Assets/Prefabs/Hazards/GuardianAreaStrike.prefab";
        public const string AreaFxPath = "Assets/Prefabs/Hazards/Fx_GuardianArea.prefab";
        public const string BattlePath = "Assets/ScriptableObjects/Battle/GuardianBattle.asset";
        public const string DefinitionPath = "Assets/ScriptableObjects/Progression/Bosses/TreeGuardian.asset";
        public const string UIPath = "Assets/ScriptableObjects/UI/GuardianBattleUI.asset";
        const string Model = "Assets/Models/Guardian";
        const string FeedbackPath = "Assets/ScriptableObjects/Feedback/CombatFeedback.asset";

        // 藤蔓模型的位置：前兩條是一階段的左右藤，後兩條給二階段四條連砸
        static readonly Vector3[] VinePositions =
        {
            new Vector3(-1f, 4f, 0f), new Vector3(1f, 4f, 0f), new Vector3(-1.6f, 5.2f, -0.3f), new Vector3(1.6f, 5.2f, -0.3f),
        };

        [MenuItem("Tools/Prologue/Build Guardian")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("請先停止 Play。");
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != PrologueBuilder.ScenePath) throw new InvalidOperationException("請開啟 Prologue。");
            foreach (var path in new[] { Root + "/Phases", Root + "/Moves/Phase1", Model + "/Materials", "Assets/ScriptableObjects/Battle", "Assets/Prefabs/Hazards", "Assets/ScriptableObjects/Progression/Bosses" })
                DaoDunDogBuilder.EnsureFolder(path);

            var layout = AssetDatabase.LoadAssetAtPath<PrologueLayoutData>(PrologueBuilder.LayoutPath);
            var battle = Get<GuardianBattleData>(BattlePath);
            var phase = Get<BossPhaseData>(Root + "/Phases/Phase1.asset", p =>
            {
                p.displayName = "一階段";
                p.healthThreshold = 3000f;
                p.movePool = 1;
                p.moveSpeed = 0f;
                p.stationary = true;
                p.turnSpeedOverride = 120f;
                p.moveIntervalMin = 0.6f;
                p.moveIntervalMax = 1f;
                p.chainChance = 0.3f;
                p.repositionTimeout = 0f;
                p.imbalanceMax = 200f;
                p.stunDuration = 3f;
                p.openingSequence = Array.Empty<BossMoveData>();
            });
            var stats = Get<BossStats>(Root + "/Stats.asset", s =>
            {
                s.maxHealth = 3000f;
                s.turnSpeed = 120f;
                s.imbalanceDecayDelay = 3f;
                s.imbalanceDecayRate = 8f;
                s.stunDamageMultiplier = 1.5f;
                s.phases = new[] { phase };
            });
            // 外緣覆蓋到隱形牆厚度，避免草地 15 m 外仍能走動的區域免傷
            var arena = Get<ArenaData>(Root + "/Arena.asset");
            arena.radius = layout.clearingRadius + layout.wallOffset + layout.wallThickness;
            EditorUtility.SetDirty(arena);
            EnsureMoves(arena.radius + layout.guardianPosition.magnitude);
            EnsurePresentationData();
            var definition = Get<BossDefinition>(DefinitionPath, d =>
            {
                d.id = "tree_guardian";
                d.displayName = "大樹守衛";
                d.sceneName = "Prologue";
                d.showInPortal = false;
                d.firstClearRewards.Clear();
            });
            var database = AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/ScriptableObjects/Progression/GameDatabase.asset");
            if (!database.bosses.Contains(definition))
            {
                database.bosses.Add(definition);
                EditorUtility.SetDirty(database);
            }
            var ui = AssetDatabase.LoadAssetAtPath<BattleUIData>(UIPath);
            if (ui == null)
            {
                ui = Object.Instantiate(AssetDatabase.LoadAssetAtPath<BattleUIData>("Assets/ScriptableObjects/UI/BattleUIData.asset"));
                ui.fallbackBossName = "大樹守衛";
                ui.bossHealthTicks = new[] { 0.8f };
                AssetDatabase.CreateAsset(ui, UIPath);
            }

            var areaPrefab = BuildArea();
            var actorPrefab = BuildActor(stats, arena, battle, areaPrefab);
            foreach (string name in new[] { "GuardianBattle", "GuardianUI" })
            {
                var old = GameObject.Find(name);
                if (old != null) Object.DestroyImmediate(old);
            }
            var root = new GameObject("GuardianBattle");
            var actor = (GameObject)PrefabUtility.InstantiatePrefab(actorPrefab, root.transform);
            actor.transform.SetPositionAndRotation(new Vector3(layout.guardianPosition.x, 0f, layout.guardianPosition.y), Quaternion.Euler(0f, 180f, 0f));
            var boss = actor.GetComponent<BossController>();
            var controller = root.AddComponent<GuardianPhaseController>();
            var outcome = root.AddComponent<PrologueOutcome>();
            var session = Object.FindAnyObjectByType<InputLockArbiter>();
            var options = Object.FindAnyObjectByType<GameOptions>();
            Set(controller, "data", battle);
            Set(controller, "boss", boss);
            Set(controller, "health", actor.GetComponent<BossHealth>());
            Set(controller, "stagger", actor.GetComponent<BossStagger>());
            Set(controller, "barrage", actor.GetComponentInChildren<RootBarrageMove>());
            Set(controller, "session", session);
            Set(controller, "tutorial", Object.FindAnyObjectByType<TutorialDirector>());
            Set(controller, "guidancePrefab", areaPrefab);
            Set(controller, "options", options);
            Set(controller, "difficulty", AssetDatabase.LoadAssetAtPath<DifficultyData>("Assets/ScriptableObjects/Options/Difficulty.asset"));
            Set(controller, "hurtCollider", actor.transform.Find("Hurtbox").GetComponent<Collider>());
            Set(controller, "lockTarget", actor.GetComponent<LockOnTarget>());
            Set(controller, "model", actor.transform.Find("Model").gameObject);
            Set(outcome, "guardian", controller);
            Set(outcome, "session", session);

            // 場地障礙：外牆（ArenaWall）、樹幹，以及守衛本體（Default layer，另外加進來）
            var preview = PlayerSpawnerBuilder.Preview().transform;
            var obstacles = scene.GetRootGameObjects()
                .SelectMany(r => r.GetComponentsInChildren<Collider>(true))
                .Where(c => !c.isTrigger && !c.transform.IsChildOf(preview) &&
                            (c.gameObject.layer == LayerMask.NameToLayer("ArenaWall") || c.name == "Trunk" || c.transform == actor.transform))
                .ToArray();
            foreach (var move in actor.GetComponentsInChildren<GuardianMove>()) SetArray(move, "obstacles", obstacles);
            actor.GetComponent<FeedbackEmitter>().SetAudioManager(Object.FindAnyObjectByType<AudioManager>());
            Set(actor.GetComponent<BossDamageFeedback>(), "viewCamera", UnityEngine.Camera.main.transform);
            BuildUI(controller, boss, ui);
            BattleUIBuilder.WireAllExecutionMarkers(); // 守衛的處決標記重建了，教學的鎖定標記要重接（不然 Validate Prologue 擋下）
            PlayerSpawnerBuilder.Wire();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            // 已經有二階段時，接著補回二階段、結局與演出的接線
            if (AssetDatabase.LoadAssetAtPath<BossPhaseData>(GuardianPhaseTwoBuilder.PhasePath) != null) GuardianPhaseTwoBuilder.Build();
            else DebugToolsBuilder.Wire();
            Debug.Log(Validate());
        }

        // 一階段 6 招與萬根齊發（只在資產不存在時寫入初版數值，之後在資產調）
        static void EnsureMoves(float range)
        {
            void Common(GuardianMoveData d, string name, float damage, bool block, float telegraph, float cooldown, float recovery, float near, float far, float max)
            {
                d.displayName = name;
                d.damage = damage;
                d.canBlock = d.canParry = block;
                d.telegraphTime = telegraph;
                d.cooldown = cooldown;
                d.recoveryTime = recovery;
                d.phase = 1;
                d.distanceWeights = true;
                d.nearDistance = 6f;
                d.nearWeight = near;
                d.farWeight = far;
                d.maxDistance = max;
                d.hitWindow = 0.15f;
                d.hitHeight = 2f;
            }
            string path = Root + "/Moves/Phase1/";
            Get<VineSweepData>(path + "VineSweep.asset", d =>
            {
                Common(d, "藤蔓橫掃", 38f, true, 0.5f, 4f, 0.8f, 6f, 2f, 9f);
                d.radius = 9f;
                d.angle = 150f;
            });
            Get<VineSlamData>(path + "VineSlam.asset", d =>
            {
                Common(d, "藤蔓砸地", 32f, true, 0.4f, 5f, 0.7f, 5f, 2f, 12f);
                d.reach = 12f;
                d.width = 1.5f;
                d.sideOffset = 1.5f;
                d.count = 2;
                d.interval = 0.3f;
            });
            Get<LeafScatterData>(path + "LeafScatter.asset", d =>
            {
                Common(d, "葉刃散射", 25f, true, 0.5f, 4f, 0.7f, 2f, 4f, range);
                d.angle = 80f;
                d.speed = 14f;
                d.reach = 0.6f;
                d.width = 1f;
                d.hitHeight = 1.6f;
                d.centerHeight = 0.8f;
                d.count = 5;
            });
            Get<LeafStormData>(path + "LeafStorm.asset", d =>
            {
                Common(d, "落葉風暴", 28f, true, 0.8f, 7f, 1f, 1f, 4f, range);
                d.radius = 1.5f;
                d.warning = 0.6f;
                d.count = 8;
                d.nearCount = 2;
                d.scatter = 2f;
            });
            Get<RootThrustData>(path + "RootThrust.asset", d =>
            {
                Common(d, "樹根突刺", 42f, false, 0.5f, 6f, 0.9f, 2f, 5f, range);
                d.radius = 1.5f;
                d.count = 3;
                d.interval = 0.65f;
                d.warning = 0.5f;
            });
            Get<RootRingData>(path + "RootRing.asset", d =>
            {
                Common(d, "樹根環陣", 35f, false, 0.7f, 8f, 0.9f, 1f, 4f, range);
                d.innerRadius = 2f;
                d.width = 0.6f;
                d.speed = 9f;
                d.count = 2;
                d.interval = 0.5f;
            });
            Get<RootBarrageData>(Root + "/Moves/RootBarrage.asset", d =>
            {
                Common(d, "萬根齊發", 42f, false, 1f, 0f, 0f, 0f, 0f, range);
                d.weight = 0f;
                d.distanceWeights = false;
                d.radius = 1.5f;
                d.innerRadius = 6f;
                d.reach = 9f;
                d.width = 4f;
                d.count = 5;
                d.outerWarning = 1f;
                d.innerWarning = 0.8f;
                d.crossWarning = 0.7f;
                d.warning = 0.35f;
                d.sampleLead = 0.1f;
                d.finalWarning = 0.6f;
                d.safeCount = 3;
                d.safeRadius = 1.5f;
                d.reachableDistance = 2f;
            });
        }

        // 招式的表現樣式（藤蔓、葉、樹根）跟著名稱走，每次重建都寫入
        static void EnsurePresentationData()
        {
            foreach (var name in new[] { "VineSweep", "VineSlam", "LeafScatter", "LeafStorm", "RootThrust", "RootRing" })
            {
                var d = AssetDatabase.LoadAssetAtPath<GuardianMoveData>(Root + "/Moves/Phase1/" + name + ".asset");
                d.visualStyle = StyleFor(name);
                EditorUtility.SetDirty(d);
            }
            var barrage = AssetDatabase.LoadAssetAtPath<RootBarrageData>(Root + "/Moves/RootBarrage.asset");
            barrage.visualStyle = AreaStrikeStyle.Root;
            EditorUtility.SetDirty(barrage);
        }

        static AreaStrikeStyle StyleFor(string name) =>
            name.StartsWith("Vine") ? AreaStrikeStyle.Vine : name.StartsWith("Leaf") ? AreaStrikeStyle.Leaf : AreaStrikeStyle.Root;

        /// <summary>重建範圍攻擊 prefab：GroundTelegraph 預兆、衝擊環、種子爆炸、葉刃（含拖尾）與招式表現。</summary>
        static AreaStrike BuildArea()
        {
            var areaFx = BuildAreaFx();
            var extra = HazardBuilder.BuildExtraFx();
            var telegraph = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Hazards/GroundTelegraph.prefab");
            if (telegraph == null) throw new InvalidOperationException("缺少 GroundTelegraph prefab，先跑 Tools > Boss > Build Hazards。");
            var root = new GameObject("GuardianAreaStrike");
            var strike = root.AddComponent<AreaStrike>();
            var hitbox = Node("Hitbox", root.transform, Vector3.zero).gameObject.AddComponent<Hitbox>();
            SetInt(hitbox, "targetMask", 1 << LayerMask.NameToLayer("Hurtbox"));
            Set(strike, "hitbox", hitbox);
            Set(strike, "telegraphPrefab", telegraph.GetComponent<GroundTelegraph>());
            Set(strike, "impactPrefab", Fx("Fx_ImpactRing"));
            Set(strike, "seedBlastPrefab", extra.seedBlast);
            Set(strike, "areaFxPrefab", areaFx);

            string leafMeshPath = Model + "/LeafBlade.asset";
            var leafMesh = AssetDatabase.LoadAssetAtPath<Mesh>(leafMeshPath);
            if (leafMesh == null)
            {
                leafMesh = new Mesh
                {
                    name = "LeafBlade",
                    vertices = new[] { new Vector3(-0.5f, 0f, 0f), new Vector3(0f, 0f, 0.5f), new Vector3(0.5f, 0f, 0f), new Vector3(0f, 0f, -0.5f) },
                    triangles = new[] { 0, 1, 2, 0, 2, 3 },
                };
                leafMesh.RecalculateNormals();
                AssetDatabase.CreateAsset(leafMesh, leafMeshPath);
            }
            var blade = new GameObject("Leaf", typeof(MeshFilter), typeof(MeshRenderer));
            blade.transform.SetParent(root.transform, false);
            blade.transform.localScale = new Vector3(1f, 0.08f, 0.6f);
            blade.GetComponent<MeshFilter>().sharedMesh = leafMesh;
            var leafMaterial = Mat("LeafBlade", new Color(0.7f, 1f, 0.4f));
            leafMaterial.SetFloat("_Cull", 0f);
            leafMaterial.SetColor("_EmissionColor", new Color(0.7f, 1f, 0.4f) * 1.5f);
            EditorUtility.SetDirty(leafMaterial);
            blade.GetComponent<MeshRenderer>().sharedMaterial = leafMaterial;
            // 葉刃拖尾（加長加寬，飛行中看得出方向）
            var trail = blade.AddComponent<TrailRenderer>();
            trail.sharedMaterial = TrailMaterial();
            trail.time = 0.4f;
            trail.startWidth = 0.6f;
            trail.endWidth = 0f;
            trail.minVertexDistance = 0.1f;
            trail.startColor = new Color(0.6f, 1f, 0.3f, 0.8f);
            trail.endColor = new Color(0.4f, 0.8f, 0.2f, 0f);
            blade.SetActive(false);
            Set(strike, "projectileVisual", blade.transform);

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, AreaPath);
            Object.DestroyImmediate(root);
            return prefab.GetComponent<AreaStrike>();
        }

        static AttackAreaFx BuildAreaFx()
        {
            var root = new GameObject("Fx_GuardianArea");
            var fx = root.AddComponent<AttackAreaFx>();
            Set(fx, "filter", root.AddComponent<MeshFilter>());
            Set(fx, "surface", root.AddComponent<MeshRenderer>());
            var line = root.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.sharedMaterial = TrailMaterial();
            line.widthMultiplier = 0.45f;
            line.positionCount = 0;
            line.enabled = false;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Set(fx, "vine", line);
            Set(fx, "rootMaterial", Mat("RootBurst", new Color(0.23f, 0.09f, 0.025f)));
            var leaf = Mat("FallingLeaves", new Color(0.5f, 0.85f, 0.18f));
            leaf.SetFloat("_Cull", 0f);
            EditorUtility.SetDirty(leaf);
            Set(fx, "leafMaterial", leaf);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, AreaFxPath);
            Object.DestroyImmediate(root);
            return prefab.GetComponent<AttackAreaFx>();
        }

        static Material TrailMaterial()
        {
            string path = Model + "/Materials/VineTrail.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) return m;
            m = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = 3000;
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        /// <summary>教學重建後補守衛的教學引用（開戰時中止教學）與演出接線；不重建守衛物件。</summary>
        public static bool WireTutorial()
        {
            var controller = Object.FindAnyObjectByType<GuardianPhaseController>();
            var tutorial = Object.FindAnyObjectByType<TutorialDirector>();
            if (controller == null || tutorial == null) return false;
            bool changed = new SerializedObject(controller).FindProperty("tutorial").objectReferenceValue != tutorial;
            if (changed) Set(controller, "tutorial", tutorial);
            PrologueCutsceneBuilder.ReconnectIfPresent();
            if (AssetDatabase.LoadAssetAtPath<BossFight.Cutscene.PrologueCutsceneData>(PrologueCutsceneBuilder.DataPath) != null) changed = true;
            return changed;
        }

        static GameObject BuildActor(BossStats stats, ArenaData arena, GuardianBattleData battle, AreaStrike area)
        {
            // 守衛本體用 Default layer：ArenaWall 只給外牆、入口門與門柱（撞牆判定、召喚避障）
            var root = new GameObject("TreeGuardian");
            var health = root.AddComponent<BossHealth>();
            var boss = root.AddComponent<BossController>();
            var stagger = root.AddComponent<BossStagger>();
            root.AddComponent<ExecutionTarget>();
            var lockOn = root.AddComponent<LockOnTarget>();
            var body = root.AddComponent<CapsuleCollider>();
            body.radius = battle.bodyRadius;
            body.height = battle.hurtHeight;
            body.center = Vector3.up * battle.hurtHeight * 0.5f;

            var model = Node("Model", root.transform, Vector3.zero);
            var bark = Mat("Bark", new Color(0.12f, 0.07f, 0.035f));
            var red = CrackMaterial();
            Prim("Trunk", model, PrimitiveType.Cylinder, Vector3.up * 3f, new Vector3(2.2f, 3f, 2.2f), bark);
            for (int i = 0; i < 8; i++)
            {
                float a = i * 360f / 8f;
                Vector3 direction = Quaternion.Euler(0f, a, 0f) * Vector3.forward;
                var crack = Prim("Crack_" + i, model, PrimitiveType.Cube, direction * 1.11f + Vector3.up * 3f, new Vector3(0.08f, 4.5f, 0.08f), red);
                crack.transform.localRotation = Quaternion.Euler(0f, a, 12f);
                var rootVine = Prim("Root_" + i, model, PrimitiveType.Capsule, direction * 1.3f + Vector3.up * 0.25f, new Vector3(0.35f, 1.2f, 0.35f), bark);
                rootVine.transform.localRotation = Quaternion.Euler(70f, a, 0f);
            }
            var crown = Node("Crown", model, Vector3.up * 7.5f);
            var green = Mat("LeafGreen", new Color(0.08f, 0.22f, 0.035f));
            var leafRed = Mat("LeafRed", new Color(0.4f, 0.025f, 0.01f));
            Prim("CrownCenter", crown, PrimitiveType.Sphere, Vector3.zero, new Vector3(5f, 5f, 5f), green);
            foreach (float side in new[] { -1f, 1f })
                Prim("CrownSide", crown, PrimitiveType.Sphere, new Vector3(side * 1.8f, -0.4f, 0f), new Vector3(3.5f, 3.5f, 3.5f), side < 0f ? leafRed : green);
            foreach (float side in new[] { -1f, 1f })
                Prim("Eye", model, PrimitiveType.Sphere, new Vector3(side * 0.32f, 1.5f, 1.08f), new Vector3(0.25f, 0.15f, 0.15f), red);
            var vines = EnsureVines(model, bark);
            var stars = Node("Stars", model, Vector3.up * 2.6f);
            var gold = Mat("StunGold", new Color(1f, 0.8f, 0.1f));
            for (int i = 0; i < 5; i++)
                Prim("Star", stars, PrimitiveType.Sphere, Quaternion.Euler(0f, i * 72f, 0f) * Vector3.forward * 0.7f, Vector3.one * 0.18f, gold);
            stars.gameObject.SetActive(false);
            Node("AimPoint", root.transform, battle.markerOffset);
            WireLockFraming(lockOn, battle);

            var hurt = Node("Hurtbox", root.transform, Vector3.zero).gameObject;
            hurt.layer = LayerMask.NameToLayer("Hurtbox");
            var collider = hurt.AddComponent<CapsuleCollider>();
            collider.isTrigger = true;
            collider.radius = battle.bodyRadius;
            collider.height = battle.hurtHeight;
            collider.center = Vector3.up * battle.hurtHeight * 0.5f;
            Set(hurt.AddComponent<Hurtbox>(), "owner", health);
            Set(health, "stats", stats);
            Set(boss, "stats", stats);
            Set(boss, "arena", arena);
            Set(boss, "body", body);
            Set(stagger, "stats", stats);

            var movesRoot = Node("Moves", root.transform, Vector3.zero);
            var moves = new List<BossMove>();
            void AddMove<T>(string name, string path) where T : GuardianMove
            {
                var m = Node(name, movesRoot, Vector3.zero).gameObject.AddComponent<T>();
                Set(m, "data", AssetDatabase.LoadAssetAtPath<GuardianMoveData>(path));
                Set(m, "areaPrefab", area);
                moves.Add(m);
            }
            string phase1 = Root + "/Moves/Phase1/";
            AddMove<VineSweepMove>("VineSweep", phase1 + "VineSweep.asset");
            AddMove<VineSlamMove>("VineSlam", phase1 + "VineSlam.asset");
            AddMove<LeafScatterMove>("LeafScatter", phase1 + "LeafScatter.asset");
            AddMove<LeafStormMove>("LeafStorm", phase1 + "LeafStorm.asset");
            AddMove<RootThrustMove>("RootThrust", phase1 + "RootThrust.asset");
            AddMove<RootRingMove>("RootRing", phase1 + "RootRing.asset");
            AddMove<RootBarrageMove>("RootBarrage", Root + "/Moves/RootBarrage.asset");
            SetArray(boss, "moves", moves.ToArray());

            var flash = root.AddComponent<BossTelegraphFlash>();
            Set(flash, "boss", boss);
            Set(flash, "palette", CombatFeedbackBuilder.EnsureTelegraphPalette());
            SetArray(flash, "renderers", model.GetComponentsInChildren<Renderer>(true));
            var hit = root.AddComponent<BossHitFeedback>();
            Set(hit, "boss", boss);
            Set(hit, "sparkPrefab", Fx("Fx_HitSpark"));
            var damage = root.AddComponent<BossDamageFeedback>();
            Set(damage, "health", health);
            Set(damage, "flash", flash);
            Set(damage, "sparkPrefab", Fx("Fx_HitSpark"));
            Set(damage, "numberPrefab", AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Hazards/Fx_DamageNumber.prefab").GetComponent<DamageNumber>());
            var emitter = root.AddComponent<FeedbackEmitter>();
            Set(emitter, "data", AssetDatabase.LoadAssetAtPath<CombatFeedbackData>(FeedbackPath));
            Set(emitter, "impulse", root.AddComponent<CinemachineImpulseSource>());
            root.AddComponent<BossImpactFeedback>();
            var poser = root.AddComponent<GuardianPoser>();
            Set(poser, "boss", boss);
            Set(poser, "stagger", stagger);
            Set(poser, "crown", crown);
            Set(poser, "stars", stars);
            SetArray(poser, "vines", vines.ToArray());

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        // 裂縫要開 Emission（PrologueCinematicStage 的發光、GuardianFullPowerVisual 的熾白都改 _EmissionColor）
        static Material CrackMaterial()
        {
            var m = Mat("Crack", new Color(0.8f, 0.025f, 0.01f));
            if (!m.IsKeywordEnabled("_EMISSION"))
            {
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                EditorUtility.SetDirty(m);
            }
            return m;
        }

        static void BuildUI(GuardianPhaseController controller, BossController boss, BattleUIData data)
        {
            var root = new GameObject("GuardianUI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 7;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            var bossRoot = new GameObject("Boss", typeof(RectTransform), typeof(CanvasGroup));
            bossRoot.transform.SetParent(root.transform, false);
            var group = bossRoot.GetComponent<CanvasGroup>();
            var rect = (RectTransform)bossRoot.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            BattleUIBuilder.BuildStandaloneBossHud(rect, data, boss.GetComponent<BossHealth>(), boss.GetComponent<BossStagger>());
            var hud = group.GetComponentInChildren<BossHudUI>();
            Set(hud, "inputNames", AssetDatabase.LoadAssetAtPath<InputNameData>("Assets/ScriptableObjects/UI/InputNameData.asset"));
            Set(hud, "hintDevice", Object.FindAnyObjectByType<InputHintDevice>());

            var hint = new GameObject("Hint", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            hint.transform.SetParent(root.transform, false);
            hint.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            hint.fontSize = 32;
            hint.alignment = TextAnchor.MiddleCenter;
            hint.color = Color.white;
            hint.raycastTarget = false;
            hint.rectTransform.anchorMin = hint.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            hint.rectTransform.anchoredPosition = new Vector2(0f, -120f);
            hint.rectTransform.sizeDelta = new Vector2(1500f, 70f);
            var ui = root.AddComponent<GuardianBattleUI>();
            Set(ui, "data", data);
            Set(ui, "guardian", controller);
            Set(ui, "bossGroup", group);
            Set(ui, "hint", hint);

            var executionRoot = new GameObject("GuardianExecution", typeof(RectTransform));
            executionRoot.transform.SetParent(root.transform, false);
            var er = (RectTransform)executionRoot.transform;
            er.anchorMin = Vector2.zero;
            er.anchorMax = Vector2.one;
            er.offsetMin = er.offsetMax = Vector2.zero;
            var marker = new GameObject("Marker", typeof(RectTransform), typeof(Image));
            marker.transform.SetParent(executionRoot.transform, false);
            var image = marker.GetComponent<Image>();
            image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Models/UI/Ui_Ring.png");
            image.color = data.executionMarkerColor;
            image.raycastTarget = false;
            var r = (RectTransform)marker.transform;
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
            r.sizeDelta = new Vector2(64f, 64f);
            var execution = executionRoot.AddComponent<ExecutionMarkerUI>();
            Set(execution, "data", data);
            Set(execution, "target", boss.GetComponent<ExecutionTarget>());
            Set(execution, "aimPoint", boss.GetComponent<LockOnTarget>().MarkerPoint);
            Set(execution, "viewCamera", UnityEngine.Camera.main);
            Set(execution, "marker", r);
            SetArray(execution, "tinted", new[] { image });
        }

        /// <summary>
        /// 檢查一階段的接線（Tools > Prologue > Validate Prologue 一起跑）：沒有 Missing Script、序章沒有一般 BattleManager、
        /// 守衛流程與結局的引用、一階段 7 招都接了資料與障礙、守衛不在 ArenaWall layer、PlayerSpawner 收到接收者、GameDatabase 的 id、地面預兆 shader。
        /// 不檢查玩法數值（數值在資產調）。
        /// </summary>
        public static string Validate()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != PrologueBuilder.ScenePath) throw new InvalidOperationException("需在 Prologue 檢查。");
            var nodes = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
            int missing = nodes.Sum(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject));
            if (missing != 0) throw new InvalidOperationException("Missing Script：" + missing);
            if (Object.FindAnyObjectByType<BattleManager>() != null) throw new InvalidOperationException("序章不能掛一般 BattleManager。");

            var controller = Object.FindAnyObjectByType<GuardianPhaseController>();
            var boss = controller.Boss;
            Require(controller, "data", "boss", "health", "stagger", "barrage", "session", "tutorial", "guidancePrefab", "hurtCollider", "lockTarget", "model", "options", "difficulty");
            Require(Object.FindAnyObjectByType<PrologueOutcome>(), "guardian", "session");
            if (boss.gameObject.layer == LayerMask.NameToLayer("ArenaWall")) throw new InvalidOperationException("守衛不能放在 ArenaWall layer。");
            var phase1 = boss.GetComponentsInChildren<GuardianMove>().Where(m => m.Data.phase == 1).ToArray();
            if (phase1.Length != 7) throw new InvalidOperationException("一階段招式不是 6 招加萬根齊發。");
            foreach (var m in boss.GetComponentsInChildren<GuardianMove>())
            {
                Require(m, "data", "areaPrefab");
                if (new SerializedObject(m).FindProperty("obstacles").arraySize == 0) throw new InvalidOperationException(m.name + " 沒有接場地障礙。");
            }
            Require(boss.GetComponent<FeedbackEmitter>(), "data", "audioManager", "impulse");
            Require(boss.GetComponent<BossDamageFeedback>(), "health", "flash", "sparkPrefab", "numberPrefab", "viewCamera");
            Require(boss.GetComponent<BossHitFeedback>(), "boss", "sparkPrefab");
            Require(boss.GetComponent<BossTelegraphFlash>(), "boss", "palette");
            Require(Object.FindAnyObjectByType<GuardianBattleUI>(), "data", "guardian", "bossGroup", "hint");

            var area = AssetDatabase.LoadAssetAtPath<GameObject>(AreaPath);
            var fx = AssetDatabase.LoadAssetAtPath<GameObject>(AreaFxPath);
            Require(area.GetComponent<AreaStrike>(), "areaFxPrefab", "projectileVisual", "telegraphPrefab", "impactPrefab", "seedBlastPrefab");
            Require(fx.GetComponent<AttackAreaFx>(), "filter", "surface", "vine", "rootMaterial", "leafMaterial");
            if (area.transform.Find("Leaf").GetComponent<TrailRenderer>() == null) throw new InvalidOperationException("葉刃缺少拖尾。");

            var spawner = Object.FindAnyObjectByType<PlayerSpawner>();
            var receivers = new SerializedObject(spawner).FindProperty("receivers");
            foreach (var receiver in new MonoBehaviour[] { controller, Object.FindAnyObjectByType<PrologueOutcome>() })
            {
                if (!Contains(receivers, receiver)) throw new InvalidOperationException("PlayerSpawner 缺接收者：" + receiver.GetType().Name);
            }
            var errors = GameDatabaseValidator.Validate(AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/ScriptableObjects/Progression/GameDatabase.asset"), false);
            if (errors.Count > 0) throw new InvalidOperationException(string.Join("；", errors));
            var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/TelegraphDecal.shadergraph");
            if (shader == null || ShaderUtil.ShaderHasError(shader)) throw new InvalidOperationException("地面預兆 Shader（TelegraphDecal）有錯誤。");
            return $"[Guardian] 一階段接線檢查通過：{scene.path}（{phase1.Length} 招）";
        }

        /// <summary>
        /// 守衛的鎖定構圖：鏡頭看樹幹中段（LockAim），鎖定菱形與處決標記在樹洞（AimPoint）；半徑、俯視角度、距離照 GuardianBattleData。
        /// 可對已存在的守衛 prefab 單獨重跑。
        /// </summary>
        public static void WireLockFraming(LockOnTarget lockOn, GuardianBattleData battle)
        {
            var root = lockOn.transform;
            var aim = root.Find("LockAim");
            if (aim == null) aim = Node("LockAim", root, Vector3.zero);
            aim.localPosition = new Vector3(0f, battle.lockAimHeight, 0f);
            var marker = root.Find("AimPoint");
            if (marker == null) marker = Node("AimPoint", root, battle.markerOffset);
            marker.localPosition = battle.markerOffset;
            var so = new SerializedObject(lockOn);
            so.FindProperty("aimPoint").objectReferenceValue = aim;
            so.FindProperty("markerPoint").objectReferenceValue = marker;
            so.FindProperty("framingRadius").floatValue = battle.lockFramingRadius;
            so.FindProperty("lockOnPitch").floatValue = battle.lockOnPitch;
            so.FindProperty("lockOnDistance").floatValue = battle.lockOnDistance;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>補齊守衛模型的四條藤（已經有的不動），回傳照順序的藤節點，再接到 GuardianPoser.vines。</summary>
        public static List<Transform> EnsureVines(Transform model, Material bark)
        {
            var vines = new List<Transform>();
            foreach (Transform child in model)
            {
                if (child.name == "Vine") vines.Add(child);
            }
            for (int i = vines.Count; i < VinePositions.Length; i++)
            {
                var vine = Node("Vine", model, VinePositions[i]);
                vines.Add(vine);
                var branch = Prim("Branch", vine, PrimitiveType.Capsule, new Vector3(0f, 0f, 2.5f), new Vector3(0.35f, 3f, 0.35f), bark);
                branch.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            }
            return vines;
        }

        // ---------- 小工具 ----------

        static bool Contains(SerializedProperty list, Object value)
        {
            for (int i = 0; i < list.arraySize; i++)
            {
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == value) return true;
            }
            return false;
        }

        static void Require(Object o, params string[] fields)
        {
            if (o == null) throw new InvalidOperationException("缺元件。");
            var so = new SerializedObject(o);
            foreach (var f in fields)
            {
                if (so.FindProperty(f)?.objectReferenceValue == null) throw new InvalidOperationException(o.name + " 缺 " + f);
            }
        }

        static T Get<T>(string path, Action<T> initialize = null) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<T>();
            initialize?.Invoke(asset);
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        static TimedFx Fx(string name) => AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Hazards/" + name + ".prefab").GetComponent<TimedFx>();

        static Material Mat(string name, Color color)
        {
            string path = Model + "/Materials/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) return m;
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.SetColor("_BaseColor", color);
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", color * 0.3f);
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        static Transform Node(string name, Transform parent, Vector3 position)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = position;
            return t;
        }

        static GameObject Prim(string name, Transform parent, PrimitiveType type, Vector3 position, Vector3 scale, Material material)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = material;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        static void Set(Object o, string field, Object value)
        {
            var so = new SerializedObject(o);
            var p = so.FindProperty(field);
            if (p == null) throw new InvalidOperationException(o.name + " 沒有欄位 " + field);
            p.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetInt(Object o, string field, int value)
        {
            var so = new SerializedObject(o);
            so.FindProperty(field).intValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetArray<T>(Object o, string field, T[] values) where T : Object
        {
            var so = new SerializedObject(o);
            var p = so.FindProperty(field);
            p.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
