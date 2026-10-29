using System;
using System.Linq;
using BossFight.Battle;
using BossFight.Boss;
using BossFight.Boss.Moves;
using BossFight.Cameras;
using BossFight.Core;
using BossFight.Hazards;
using BossFight.Progression;
using BossFight.UI;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace BossFight.EditorTools
{
    /// <summary>
    /// Tools > Prologue > Build Guardian Phase 2：在已有一階段的守衛上補二階段（T29），不重建一階段、教學、地形或 Hub。
    /// 1. 資產（沒有才建，之後在資產調）：二階段 BossPhaseData、8 招的 GuardianMoveData、種子 prefab、樹皮碎片 prefab、勝利畫面的 BattleUIData。
    /// 2. 守衛 prefab：加上二階段 8 招（P2_*）、種子炸彈的種子與發射點、全力外觀 GuardianFullPowerVisual。
    /// 3. 場景：GuardianPhaseTwo（轉階段的戰鬥側）、GuardianSummonController 與兩隻召喚樹精（CombatChildren）、PrologueOutcome 的結局接線、
    ///    Boss 血條的自損白段與名稱換色（GuardianPhaseFeedbackUI）、勝利畫面（GuardianResultUI，只有「返回休息區」）。
    /// 4. 最後接回演出（PrologueCutsceneBuilder.ReconnectIfPresent）、PlayerSpawner 與開發者工具（DebugToolsBuilder.Wire）。
    /// </summary>
    public static class GuardianPhaseTwoBuilder
    {
        public const string PhasePath = GuardianBuilder.Root + "/Phases/Phase2.asset";
        public const string MovesPath = GuardianBuilder.Root + "/Moves/Phase2/";
        public const string SeedPath = "Assets/Prefabs/Hazards/GuardianSeed.prefab";
        public const string BarkPath = "Assets/Prefabs/Hazards/Fx_GuardianBark.prefab";
        public const string ResultDataPath = "Assets/ScriptableObjects/UI/GuardianVictoryUI.asset";
        const string FeedbackPath = "Assets/ScriptableObjects/Feedback/CombatFeedback.asset";

        [MenuItem("Tools/Prologue/Build Guardian Phase 2")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("請先停止 Play。");
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != PrologueBuilder.ScenePath) throw new InvalidOperationException("請開啟 Prologue。");
            DaoDunDogBuilder.EnsureFolder(MovesPath.TrimEnd('/'));
            var guardian = Object.FindAnyObjectByType<GuardianPhaseController>();
            if (guardian == null) throw new InvalidOperationException("請先跑 Tools > Prologue > Build Guardian。");

            var stats = AssetDatabase.LoadAssetAtPath<BossStats>(GuardianBuilder.Root + "/Stats.asset");
            var phase = Get<BossPhaseData>(PhasePath, p =>
            {
                p.displayName = "全力";
                p.movePool = 2;
                p.healthThreshold = 0f;
                p.transitionTime = 5f;
                p.stationary = true;
                p.moveSpeed = 0f;
                p.turnSpeedOverride = 180f;
                p.moveIntervalMin = 0.3f;
                p.moveIntervalMax = 0.6f;
                p.chainChance = 0.5f;
                p.imbalanceMax = 240f;
                p.stunDuration = 2.5f;
                p.repositionTimeout = 0f;
                p.musicSpeed = 1.2f;
                p.openingSequence = Array.Empty<BossMoveData>();
            });
            stats.phases = new[] { stats.phases[0], phase };
            EditorUtility.SetDirty(stats);
            var arena = AssetDatabase.LoadAssetAtPath<ArenaData>(GuardianBuilder.Root + "/Arena.asset");
            var layout = AssetDatabase.LoadAssetAtPath<PrologueLayoutData>(PrologueBuilder.LayoutPath);
            EnsureMoves(arena.radius + layout.guardianPosition.magnitude);
            var seed = BuildSeed();
            var bark = BuildBark();
            AddPrefabMoves(seed, bark);

            var boss = guardian.Boss;
            var root = guardian.gameObject;
            var outcome = root.GetComponent<PrologueOutcome>();
            var phaseTwo = DaoDunDogBuilder.GetOrAdd<GuardianPhaseTwo>(root);
            var summons = DaoDunDogBuilder.GetOrAdd<GuardianSummonController>(root);
            var session = Object.FindAnyObjectByType<InputLockArbiter>();
            var audio = Object.FindAnyObjectByType<AudioManager>();
            Set(phaseTwo, "guardian", guardian);
            Set(phaseTwo, "session", session);
            Set(phaseTwo, "cameraController", Object.FindAnyObjectByType<CameraController>());
            Set(phaseTwo, "audioManager", audio);
            Set(phaseTwo, "guardianFace", boss.GetComponent<LockOnTarget>().AimPoint);
            // T29 暫代轉場的鏡頭（正式演出接管後已刪）
            var oldShot = root.transform.Find("Phase2Shot");
            if (oldShot != null) Object.DestroyImmediate(oldShot.gameObject);

            Set(outcome, "bossHealth", boss.GetComponent<BossHealth>());
            Set(outcome, "definition", AssetDatabase.LoadAssetAtPath<BossDefinition>(GuardianBuilder.DefinitionPath));
            Set(outcome, "sceneFlow", Object.FindAnyObjectByType<SceneFlow>());
            Set(outcome, "audioManager", audio);
            Set(outcome, "options", Object.FindAnyObjectByType<GameOptions>());

            var move = boss.GetComponentInChildren<CallChildrenMove>(true);
            Set(summons, "guardian", guardian);
            Set(summons, "move", move);
            var angry = AssetDatabase.LoadAssetAtPath<TrainingSaplingData>(PrologueActorsBuilder.AngryDataPath);
            Set(summons, "data", angry);
            SetArray(summons, "children", BuildChildren(root.transform, ((CallChildrenData)move.Data).maxChildren, angry, audio));

            // 二階段的招式沿用一階段的場地障礙
            var obstacleList = new SerializedObject(boss.GetComponentInChildren<RootBarrageMove>()).FindProperty("obstacles");
            var obstacles = new Collider[obstacleList.arraySize];
            for (int i = 0; i < obstacles.Length; i++) obstacles[i] = (Collider)obstacleList.GetArrayElementAtIndex(i).objectReferenceValue;
            foreach (var m in boss.GetComponentsInChildren<GuardianMove>(true))
            {
                if (m.Data.phase == 2) SetArray(m, "obstacles", obstacles);
            }

            BuildUI(guardian, outcome, boss);
            PrologueCutsceneBuilder.ReconnectIfPresent();
            PlayerSpawnerBuilder.Wire();
            DebugToolsBuilder.Wire();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log(Validate());
        }

        // 二階段 8 招（只在資產不存在時寫入初版數值，之後在資產調）
        static void EnsureMoves(float range)
        {
            void Common(GuardianMoveData d, string title, float damage, bool block, float telegraph, float cooldown, float recovery, float near, float far, float max)
            {
                d.displayName = title;
                d.phase = 2;
                d.damage = damage;
                d.canBlock = d.canParry = block;
                d.telegraphTime = telegraph;
                d.cooldown = cooldown;
                d.recoveryTime = recovery;
                d.distanceWeights = true;
                d.nearDistance = 6f;
                d.nearWeight = near;
                d.farWeight = far;
                d.maxDistance = max;
                d.hitWindow = 0.15f;
                d.hitHeight = 2f;
            }
            Get<VineSweepData>(MovesPath + "VineSweep.asset", d =>
            {
                Common(d, "藤蔓迴旋", 38f, true, 0.35f, 4f, 0.6f, 6f, 2f, 9f);
                d.radius = 9f;
                d.angle = 150f;
                d.secondAngle = 360f;
                d.count = 2;
                d.interval = 0.35f;
                d.visualStyle = AreaStrikeStyle.Vine;
            });
            Get<VineSlamData>(MovesPath + "VineSlam.asset", d =>
            {
                Common(d, "藤蔓連砸", 32f, true, 0.3f, 5f, 0.5f, 5f, 2f, 12f);
                d.reach = 12f;
                d.width = 1.5f;
                d.sideOffset = 1.5f;
                d.count = 4;
                d.interval = 0.25f;
                d.trackLast = true;
                d.visualStyle = AreaStrikeStyle.Vine;
            });
            Get<LeafScatterData>(MovesPath + "LeafScatter.asset", d =>
            {
                Common(d, "葉刃暴雨", 25f, true, 0.35f, 4f, 0.45f, 2f, 4f, range);
                d.angle = 80f;
                d.count = 9;
                d.waves = 2;
                d.interval = 0.35f;
                d.angleOffset = 5f;
                d.speed = 14f;
                d.reach = 0.6f;
                d.width = 1f;
                d.hitHeight = 1.6f;
                d.centerHeight = 0.8f;
                d.visualStyle = AreaStrikeStyle.Leaf;
            });
            Get<LeafStormData>(MovesPath + "LeafStorm.asset", d =>
            {
                Common(d, "落葉狂風", 28f, true, 0.6f, 6f, 0.6f, 1f, 4f, range);
                d.radius = 1.5f;
                d.count = 14;
                d.waves = 2;
                d.interval = 0.35f;
                d.warning = 0.6f;
                d.nearCount = 2;
                d.scatter = 2f;
                d.visualStyle = AreaStrikeStyle.Leaf;
            });
            Get<RootThrustData>(MovesPath + "RootThrust.asset", d =>
            {
                Common(d, "樹根追獵", 42f, false, 0.35f, 5f, 0.5f, 2f, 5f, range);
                d.radius = 1.5f;
                d.count = 5;
                d.interval = d.warning = 0.35f;
            });
            Get<RootRingData>(MovesPath + "RootRing.asset", d =>
            {
                Common(d, "樹根三重環", 35f, false, 0.5f, 7f, 0.6f, 1f, 4f, range);
                d.innerRadius = 2f;
                d.width = 0.6f;
                d.speed = 9f;
                d.count = 3;
                d.interval = 0.4f;
                d.reverseSecond = true;
            });
            Get<SeedBombData>(MovesPath + "SeedBomb.asset", d =>
            {
                Common(d, "種子炸彈", 30f, true, 0.45f, 9f, 0.6f, 1f, 3f, range);
                d.canParry = false; // 企劃書只寫可格擋（D16，使用者決定：可格擋、不可盾反）
                d.radius = 2.5f;
                d.count = 4;
                d.warning = 1f;
                d.visualStyle = AreaStrikeStyle.Seed;
            });
            Get<CallChildrenData>(MovesPath + "CallChildren.asset", d => Common(d, "呼喚孩子", 25f, true, 0.6f, 15f, 0.6f, 1f, 2f, range));
        }

        static void AddPrefabMoves(SeedBombVisual seed, ParticleSystem bark)
        {
            var prefab = PrefabUtility.LoadPrefabContents(GuardianBuilder.PrefabPath);
            try
            {
                var parent = prefab.transform.Find("Moves");
                void Add<T>(string name) where T : GuardianMove
                {
                    string node = "P2_" + name;
                    var t = parent.Find(node);
                    if (t == null)
                    {
                        t = new GameObject(node).transform;
                        t.SetParent(parent, false);
                    }
                    var m = DaoDunDogBuilder.GetOrAdd<T>(t.gameObject);
                    Set(m, "data", AssetDatabase.LoadAssetAtPath<GuardianMoveData>(MovesPath + name + ".asset"));
                    Set(m, "areaPrefab", AssetDatabase.LoadAssetAtPath<AreaStrike>(GuardianBuilder.AreaPath));
                }
                Add<VineSweepMove>("VineSweep");
                Add<VineSlamMove>("VineSlam");
                Add<LeafScatterMove>("LeafScatter");
                Add<LeafStormMove>("LeafStorm");
                Add<RootThrustMove>("RootThrust");
                Add<RootRingMove>("RootRing");
                Add<SeedBombMove>("SeedBomb");
                Add<CallChildrenMove>("CallChildren");
                SetArray(prefab.GetComponent<BossController>(), "moves", prefab.GetComponentsInChildren<BossMove>(true));
                var seedMove = prefab.GetComponentInChildren<SeedBombMove>();
                Set(seedMove, "seedPrefab", seed);
                Set(seedMove, "launchPoint", prefab.transform.Find("Model/Crown"));

                var visual = DaoDunDogBuilder.GetOrAdd<GuardianFullPowerVisual>(prefab);
                Set(visual, "health", prefab.GetComponent<BossHealth>());
                Set(visual, "barkPrefab", bark);
                SetArray(visual, "crown", prefab.transform.Find("Model/Crown").GetComponentsInChildren<Renderer>());
                SetArray(visual, "cracks", prefab.GetComponentsInChildren<Renderer>().Where(r => r.name.StartsWith("Crack_")).ToArray());
                PrefabUtility.SaveAsPrefabAsset(prefab, GuardianBuilder.PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefab);
            }
        }

        // 呼喚孩子的召喚池：小樹精 prefab 的實例（不自己冒出，模型先藏起來），已經有的沿用
        static TrainingSapling[] BuildChildren(Transform root, int count, TrainingSaplingData data, AudioManager audio)
        {
            var pool = root.Find("CombatChildren");
            if (pool == null)
            {
                pool = new GameObject("CombatChildren").transform;
                pool.SetParent(root, false);
            }
            var children = new TrainingSapling[count];
            for (int i = 0; i < count; i++)
            {
                var t = pool.Find("Child_" + i);
                if (t == null)
                {
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrologueActorsBuilder.SaplingPrefabPath), pool);
                    go.name = "Child_" + i;
                    t = go.transform;
                }
                var child = t.GetComponent<TrainingSapling>();
                children[i] = child;
                Set(child, "data", data);
                SetBool(child, "appearOnStart", false);
                var emitter = DaoDunDogBuilder.GetOrAdd<FeedbackEmitter>(child.gameObject);
                Set(emitter, "data", AssetDatabase.LoadAssetAtPath<CombatFeedbackData>(FeedbackPath));
                Set(emitter, "audioManager", audio);
                Set(emitter, "impulse", DaoDunDogBuilder.GetOrAdd<CinemachineImpulseSource>(child.gameObject));
                var feedback = DaoDunDogBuilder.GetOrAdd<TeachingActorFeedback>(child.gameObject);
                Set(feedback, "sapling", child);
                Set(feedback, "sparkPrefab", Fx("Fx_HitSpark"));
                Set(feedback, "impactAnchor", t.Find("AimPoint"));
                PrologueActorsBuilder.WireHitFeedback(child);
                t.Find("Model").gameObject.SetActive(false);
            }
            return children;
        }

        static SeedBombVisual BuildSeed()
        {
            var existing = AssetDatabase.LoadAssetAtPath<SeedBombVisual>(SeedPath);
            if (existing != null) return existing;
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "GuardianSeed";
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.localScale = new Vector3(0.45f, 0.6f, 0.45f);
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = new Color(0.7f, 0.025f, 0.01f) };
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", Color.red);
            AssetDatabase.CreateAsset(material, "Assets/Models/Guardian/Materials/Seed.mat");
            go.GetComponent<Renderer>().sharedMaterial = material;
            go.AddComponent<SeedBombVisual>();
            var trail = go.AddComponent<TrailRenderer>();
            trail.time = 0.25f;
            trail.startWidth = 0.2f;
            trail.endWidth = 0f;
            trail.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Models/Guardian/Materials/VineTrail.mat");
            trail.startColor = Color.red;
            trail.endColor = Color.clear;
            var saved = PrefabUtility.SaveAsPrefabAsset(go, SeedPath);
            Object.DestroyImmediate(go);
            return saved.GetComponent<SeedBombVisual>();
        }

        static ParticleSystem BuildBark()
        {
            var existing = AssetDatabase.LoadAssetAtPath<ParticleSystem>(BarkPath);
            if (existing != null) return existing;
            var go = new GameObject("Fx_GuardianBark");
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = 0.8f;
            main.loop = false;
            main.startLifetime = 0.8f;
            main.startSpeed = 2f;
            main.startSize = 0.2f;
            main.gravityModifier = 0.8f;
            main.stopAction = ParticleSystemStopAction.Destroy;
            main.startColor = new Color(0.22f, 0.1f, 0.025f);
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)9) });
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            renderer.mesh = cube.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(cube);
            renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Models/Guardian/Materials/Bark.mat");
            var saved = PrefabUtility.SaveAsPrefabAsset(go, BarkPath);
            Object.DestroyImmediate(go);
            return saved.GetComponent<ParticleSystem>();
        }

        static void BuildUI(GuardianPhaseController guardian, PrologueOutcome outcome, BossController boss)
        {
            var hud = Object.FindAnyObjectByType<GuardianBattleUI>().GetComponentInChildren<BossHudUI>(true);
            var bridge = DaoDunDogBuilder.GetOrAdd<GuardianPhaseFeedbackUI>(hud.gameObject);
            Set(bridge, "guardian", guardian);
            Set(bridge, "health", boss.GetComponent<BossHealth>());
            Set(bridge, "hud", hud);
            var healthBar = new SerializedObject(hud).FindProperty("healthBar").objectReferenceValue as ResourceBar;
            BattleUIBuilder.AddSelfCostSegment(healthBar);

            var old = GameObject.Find("GuardianResultUI");
            if (old != null) Object.DestroyImmediate(old);
            var root = new GameObject("GuardianResultUI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            var data = AssetDatabase.LoadAssetAtPath<BattleUIData>(ResultDataPath);
            if (data == null)
            {
                data = Object.Instantiate(AssetDatabase.LoadAssetAtPath<BattleUIData>(GuardianBuilder.UIPath));
                data.victoryTitle = "擊敗大樹守衛";
                data.victorySubtitle = "";
                AssetDatabase.CreateAsset(data, ResultDataPath);
            }
            BattleUIBuilder.BuildStandalonePrologueResult(root.transform, data, outcome, Object.FindAnyObjectByType<EventSystem>());
        }

        /// <summary>
        /// 檢查二階段的接線（Tools > Prologue > Validate Prologue 一起跑）：沒有 Missing Script、招式池（一階段 7、二階段 8）、
        /// 轉階段與結局與召喚的引用、兩隻召喚樹精、勝利畫面只接序章結局、PlayerSpawner 的接收者與鎖定目標，以及結局存檔的純規則。
        /// 不檢查玩法數值（數值在資產調）。
        /// </summary>
        public static string Validate()
        {
            var guardian = Object.FindAnyObjectByType<GuardianPhaseController>();
            var boss = guardian.Boss;
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            int missing = scene.GetRootGameObjects().Sum(r => r.GetComponentsInChildren<Transform>(true).Sum(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)));
            if (missing != 0) throw new InvalidOperationException("Missing Script：" + missing);
            var moves = boss.GetComponentsInChildren<GuardianMove>(true).Where(m => m.Data.phase == 2).ToArray();
            if (moves.Length != 8 || boss.Moves.Count != 15) throw new InvalidOperationException("招式池數量不符（二階段要 8 招、總共 15 招）。");
            Require(Object.FindAnyObjectByType<GuardianPhaseTwo>(), "guardian", "session", "cameraController", "audioManager", "guardianFace");
            Require(Object.FindAnyObjectByType<PrologueOutcome>(), "guardian", "session", "bossHealth", "definition", "sceneFlow", "audioManager", "options");
            Require(Object.FindAnyObjectByType<GuardianSummonController>(), "guardian", "move", "data");
            foreach (var m in moves)
            {
                Require(m, "data", "areaPrefab");
                if (new SerializedObject(m).FindProperty("obstacles").arraySize == 0) throw new InvalidOperationException(m.name + " 沒有接場地障礙。");
            }
            Require(boss.GetComponent<GuardianFullPowerVisual>(), "health", "barkPrefab");
            var children = guardian.transform.Find("CombatChildren").GetComponentsInChildren<TrainingSapling>(true);
            if (children.Length != 2) throw new InvalidOperationException("召喚池不是兩隻。");
            foreach (var c in children)
            {
                Require(c, "data", "attackHitbox", "hurtCollider");
                Require(c.GetComponent<FeedbackEmitter>(), "data", "audioManager", "impulse");
                Require(c.GetComponent<TeachingActorFeedback>(), "sapling", "sparkPrefab", "impactAnchor");
                if (new SerializedObject(c).FindProperty("appearOnStart").boolValue) throw new InvalidOperationException("召喚樹精不能自己冒出。");
            }
            if (Object.FindAnyObjectByType<BattleManager>() != null) throw new InvalidOperationException("序章不能掛一般 BattleManager。");
            var result = Object.FindAnyObjectByType<BattleResultUI>();
            Require(result, "prologue", "data", "group", "hubButton", "firstClearGroup");
            if (new SerializedObject(result).FindProperty("battle").objectReferenceValue != null) throw new InvalidOperationException("勝利畫面同時接了 BattleManager。");
            var hud = Object.FindAnyObjectByType<GuardianBattleUI>().GetComponentInChildren<BossHudUI>(true);
            var healthBar = (ResourceBar)new SerializedObject(hud).FindProperty("healthBar").objectReferenceValue;
            Require(healthBar, "loss");

            var spawner = new SerializedObject(Object.FindAnyObjectByType<PlayerSpawner>());
            foreach (var c in children)
            {
                if (!Contains(spawner.FindProperty("lockTargets"), c.GetComponent<LockOnTarget>()) ||
                    !Contains(spawner.FindProperty("executionTargets"), c.GetComponent<ExecutionTarget>()))
                    throw new InvalidOperationException("召喚樹精沒有加進鎖定與處決清單。");
            }
            foreach (var c in new MonoBehaviour[] { Object.FindAnyObjectByType<GuardianPhaseTwo>(), Object.FindAnyObjectByType<GuardianSummonController>(), Object.FindAnyObjectByType<PrologueOutcome>() })
            {
                if (!Contains(spawner.FindProperty("receivers"), c)) throw new InvalidOperationException("PlayerSpawner 缺接收者：" + c.GetType().Name);
            }
            ValidatePureRules();
            return $"[Guardian] 二階段接線檢查通過：{boss.Moves.Count} 招、召喚池 {children.Length} 隻";
        }

        /// <summary>自損公式與序章結局存檔的純規則（不讀寫真的存檔或 PlayerPrefs）。</summary>
        public static void ValidatePureRules()
        {
            float hp = 2400f;
            for (int i = 0; i < 10; i++) hp = BossHealth.NonlethalSelfCost(hp, 0.05f);
            if (Mathf.Abs(hp - 2400f * Mathf.Pow(0.95f, 10)) > 0.01f || BossHealth.NonlethalSelfCost(float.Epsilon, 0.05f) <= 0f || BossHealth.NonlethalSelfCost(0.2f, 0.05f) >= 0.2f)
                throw new InvalidOperationException("自損公式或非致死保護錯誤。");
            var original = new ProfileData();
            if (!ProfileStore.PreparePrologueCompletion(original, PrologueResult.Victory, "test_guardian", 1, new[] { "test_equipment", "test_equipment" }, new[] { "test_character" }, out var next, out bool first) ||
                !first || original.prologueDone || original.skillPoints != 0 || original.defeatedBosses.Count != 0 ||
                next.skillPoints != 1 || next.unlockedEquipment.Count != 1 || !next.prologueDone || next.prologueResult != PrologueResult.Victory)
                throw new InvalidOperationException("結局存檔的快照或來源隔離錯誤。");
            if (ProfileStore.PreparePrologueCompletion(next, PrologueResult.Victory, "test_guardian", 1, null, null, out _, out _))
                throw new InvalidOperationException("結局存檔重複提交。");
            var existing = new ProfileData();
            existing.defeatedBosses.Add("test_guardian");
            if (!ProfileStore.PreparePrologueCompletion(existing, PrologueResult.Victory, "test_guardian", 1, null, null, out var again, out bool repeat) || repeat || again.skillPoints != 0)
                throw new InvalidOperationException("已擊敗過又發了首通獎勵。");
        }

        // ---------- 小工具 ----------

        static bool Contains(SerializedProperty p, Object value)
        {
            for (int i = 0; i < p.arraySize; i++)
            {
                if (p.GetArrayElementAtIndex(i).objectReferenceValue == value) return true;
            }
            return false;
        }

        static void Require(Object o, params string[] names)
        {
            if (o == null) throw new InvalidOperationException("缺元件。");
            var so = new SerializedObject(o);
            foreach (var n in names)
            {
                if (so.FindProperty(n)?.objectReferenceValue == null) throw new InvalidOperationException(o.name + " 缺 " + n);
            }
        }

        static T Get<T>(string path, Action<T> init) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<T>();
            init(asset);
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        static TimedFx Fx(string name) => AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Hazards/" + name + ".prefab").GetComponent<TimedFx>();

        static void Set(Object o, string field, Object value)
        {
            var so = new SerializedObject(o);
            var p = so.FindProperty(field);
            if (p == null) throw new InvalidOperationException(o.name + " 沒有欄位 " + field);
            p.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetBool(Object o, string field, bool value)
        {
            var so = new SerializedObject(o);
            so.FindProperty(field).boolValue = value;
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
