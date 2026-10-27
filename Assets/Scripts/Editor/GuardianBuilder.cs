using System;
using System.Collections.Generic;
using System.Linq;
using BossFight.Battle;
using BossFight.Boss;
using BossFight.Boss.Moves;
using BossFight.Core;
using BossFight.DebugTools;
using BossFight.Hazards;
using BossFight.Player;
using BossFight.Progression;
using BossFight.UI;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace BossFight.EditorTools
{
    /// <summary>T28 定點重建守衛及其接線；不重建已驗收的地形、教學或相機。</summary>
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

        [MenuItem("Tools/Prologue/Build Guardian (T28)")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("請先停止 Play。");
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != PrologueBuilder.ScenePath) throw new InvalidOperationException("請開啟 Prologue。");
            foreach (var path in new[] { Root + "/Phases", Root + "/Moves/Phase1", Model + "/Materials", "Assets/ScriptableObjects/Battle", "Assets/Prefabs/Hazards", "Assets/ScriptableObjects/Progression/Bosses" })
                DaoDunDogBuilder.EnsureFolder(path);
            var layout = AssetDatabase.LoadAssetAtPath<PrologueLayoutData>(PrologueBuilder.LayoutPath);
            var battle = Get<GuardianBattleData>(BattlePath);
            var phase = Get<BossPhaseData>(Root + "/Phases/Phase1.asset", p => {
                p.displayName = "一階段"; p.healthThreshold = 3000f; p.movePool = 1;
                p.moveSpeed = 0f; p.stationary = true; p.turnSpeedOverride = 120f;
                p.moveIntervalMin = 0.6f; p.moveIntervalMax = 1f; p.chainChance = 0.3f;
                p.repositionTimeout = 0f; p.imbalanceMax = 200f; p.stunDuration = 3f; p.openingSequence = Array.Empty<BossMoveData>();
            });
            var stats = Get<BossStats>(Root + "/Stats.asset", s => {
                s.maxHealth = 3000f; s.turnSpeed = 120f; s.imbalanceDecayDelay = 3f; s.imbalanceDecayRate = 8f;
                s.stunDamageMultiplier = 1.5f; s.phases = new[] { phase };
            });
            // 外緣覆蓋到隱形牆厚度，避免草地 15m 外仍能走動的區域免傷；圈內排法另查真實碰撞。
            var arena = Get<ArenaData>(Root + "/Arena.asset");
            arena.radius = layout.clearingRadius + layout.wallOffset + layout.wallThickness;
            EditorUtility.SetDirty(arena);
            EnsureMoves(arena.radius + layout.guardianPosition.magnitude);
            EnsurePresentationData();
            var definition = Get<BossDefinition>(DefinitionPath, d => {
                d.id = "tree_guardian"; d.displayName = "大樹守衛"; d.sceneName = "Prologue";
                d.showInPortal = false; d.firstClearRewards.Clear();
            });
            var database = AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/ScriptableObjects/Progression/GameDatabase.asset");
            if (!database.bosses.Contains(definition)) { database.bosses.Add(definition); EditorUtility.SetDirty(database); }
            var ui = AssetDatabase.LoadAssetAtPath<BattleUIData>(UIPath);
            if (ui == null)
            {
                ui = Object.Instantiate(AssetDatabase.LoadAssetAtPath<BattleUIData>("Assets/ScriptableObjects/UI/BattleUIData.asset"));
                ui.fallbackBossName = "大樹守衛"; ui.bossHealthTicks = new[] { 0.8f };
                AssetDatabase.CreateAsset(ui, UIPath);
            }
            var areaPrefab = BuildArea();
            var actorPrefab = BuildActor(stats, arena, battle, areaPrefab);
            foreach (string name in new[] { "GuardianBattle", "GuardianUI" })
            { var old = GameObject.Find(name); if (old != null) Object.DestroyImmediate(old); }
            var root = new GameObject("GuardianBattle");
            var actor = (GameObject)PrefabUtility.InstantiatePrefab(actorPrefab, root.transform);
            actor.transform.SetPositionAndRotation(new Vector3(layout.guardianPosition.x, 0f, layout.guardianPosition.y), Quaternion.Euler(0f, 180f, 0f));
            var boss = actor.GetComponent<BossController>();
            var controller = root.AddComponent<GuardianPhaseController>();
            var outcome = root.AddComponent<PrologueOutcome>();
            var session = Object.FindAnyObjectByType<PrologueSession>();
            Set(controller, "data", battle); Set(controller, "boss", boss); Set(controller, "health", actor.GetComponent<BossHealth>());
            Set(controller, "stagger", actor.GetComponent<BossStagger>()); Set(controller, "barrage", actor.GetComponentInChildren<RootBarrageMove>());
            Set(controller, "hitFeedback", actor.GetComponent<BossHitFeedback>()); Set(controller, "session", session);
            Set(controller, "tutorial", Object.FindAnyObjectByType<TutorialDirector>()); Set(controller, "guidancePrefab", areaPrefab);
            Set(controller, "options", Object.FindAnyObjectByType<GameOptions>());
            Set(controller, "difficulty", AssetDatabase.LoadAssetAtPath<DifficultyData>("Assets/ScriptableObjects/Options/Difficulty.asset"));
            Set(controller, "hurtCollider", actor.transform.Find("Hurtbox").GetComponent<Collider>());
            Set(controller, "lockTarget", actor.GetComponent<LockOnTarget>()); Set(controller, "model", actor.transform.Find("Model").gameObject);
            Set(outcome, "guardian", controller); Set(outcome, "session", session);
            var obstacles = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Collider>(true))
                .Where(c => !c.isTrigger && (c.gameObject.layer == LayerMask.NameToLayer("ArenaWall") || c.name == "Trunk") && !c.transform.IsChildOf(PlayerSpawnerBuilder.Preview().transform)).ToArray();
            foreach (var move in actor.GetComponentsInChildren<GuardianMove>()) SetArray(move, "obstacles", obstacles);
            actor.GetComponent<FeedbackEmitter>().SetAudioManager(Object.FindAnyObjectByType<AudioManager>());
            Set(actor.GetComponent<BossDamageFeedback>(), "viewCamera", UnityEngine.Camera.main.transform);
            BuildUI(controller, boss, ui);
            EnsureDebugActions();
            var debug = root.AddComponent<GuardianDebugTools>(); debug.enabled = false; Set(debug, "guardian", controller);
            var gate = root.AddComponent<DeveloperMode>(); Set(gate, "options", Object.FindAnyObjectByType<GameOptions>()); Set(gate, "pause", Object.FindAnyObjectByType<GamePause>());
            SetArray(gate, "tools", new Behaviour[] { debug });
            PlayerSpawnerBuilder.Wire();
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            Debug.Log(Validate());
        }

        static void EnsureMoves(float range)
        {
            void Common(GuardianMoveData d, string name, float damage, bool block, float telegraph, float cooldown, float recovery, float near, float far, float max)
            {
                d.displayName = name; d.damage = damage; d.canBlock = d.canParry = block;
                d.telegraphTime = telegraph; d.cooldown = cooldown; d.recoveryTime = recovery;
                d.phase = 1; d.distanceWeights = true; d.nearDistance = 6f; d.nearWeight = near; d.farWeight = far; d.maxDistance = max;
                d.hitWindow = 0.15f; d.hitHeight = 2f;
            }
            Get<VineSweepData>(Root + "/Moves/Phase1/VineSweep.asset", d => { Common(d, "藤蔓橫掃", 38f, true, 0.5f, 4f, 0.8f, 6f, 2f, 9f); d.radius = 9f; d.angle = 150f; });
            Get<VineSlamData>(Root + "/Moves/Phase1/VineSlam.asset", d => { Common(d, "藤蔓砸地", 32f, true, 0.4f, 5f, 0.7f, 5f, 2f, 12f); d.reach = 12f; d.width = 1.5f; d.sideOffset = 1.5f; d.count = 2; d.interval = 0.3f; });
            Get<LeafScatterData>(Root + "/Moves/Phase1/LeafScatter.asset", d => { Common(d, "葉刃散射", 25f, true, 0.5f, 4f, 0.7f, 2f, 4f, range); d.angle = 80f; d.speed = 14f; d.reach = 0.6f; d.width = 1f; d.hitHeight = 1.6f; d.centerHeight = 0.8f; d.count = 5; });
            Get<LeafStormData>(Root + "/Moves/Phase1/LeafStorm.asset", d => { Common(d, "落葉風暴", 28f, true, 0.8f, 7f, 1f, 1f, 4f, range); d.radius = 1.5f; d.warning = 0.6f; d.count = 8; d.nearCount = 2; d.scatter = 2f; });
            Get<RootThrustData>(Root + "/Moves/Phase1/RootThrust.asset", d => { Common(d, "樹根突刺", 42f, false, 0.5f, 6f, 0.9f, 2f, 5f, range); d.radius = 1.5f; d.count = 3; d.interval = 0.65f; d.warning = 0.5f; });
            Get<RootRingData>(Root + "/Moves/Phase1/RootRing.asset", d => { Common(d, "樹根環陣", 35f, false, 0.7f, 8f, 0.9f, 1f, 4f, range); d.innerRadius = 2f; d.width = 0.6f; d.speed = 9f; d.count = 2; d.interval = 0.5f; });
            Get<RootBarrageData>(Root + "/Moves/RootBarrage.asset", d => {
                Common(d, "萬根齊發", 42f, false, 1f, 0f, 0f, 0f, 0f, range); d.weight = 0f; d.distanceWeights = false;
                d.radius = 1.5f; d.innerRadius = 6f; d.reach = 9f; d.width = 4f; d.count = 5;
                d.outerWarning = 1f; d.innerWarning = 0.8f; d.crossWarning = 0.7f; d.warning = 0.35f;
                d.sampleLead = 0.1f; d.finalWarning = 0.6f; d.safeCount = 3; d.safeRadius = 1.5f; d.reachableDistance = 2f;
            });
        }

        static AreaStrike BuildArea()
        {
            var areaFx = BuildAreaFx();
            string meshPath = Model + "/AreaQuad.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (mesh == null)
            {
                mesh = new Mesh { name = "AreaQuad", vertices = new[] { new Vector3(-0.5f,0,-0.5f), new Vector3(-0.5f,0,0.5f), new Vector3(0.5f,0,0.5f), new Vector3(0.5f,0,-0.5f) }, triangles = new[] { 0,1,2,0,2,3 } };
                mesh.RecalculateNormals(); AssetDatabase.CreateAsset(mesh, meshPath);
            }
            string materialPath = Model + "/Materials/AreaTelegraph.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (mat == null) { mat = new Material(Shader.Find("BossFight/AreaTelegraph")); AssetDatabase.CreateAsset(mat, materialPath); }
            var root = new GameObject("GuardianAreaStrike"); var strike = root.AddComponent<AreaStrike>();
            var warning = Node("Warning", root.transform, Vector3.zero).gameObject;
            warning.AddComponent<MeshFilter>().sharedMesh = mesh; var renderer = warning.AddComponent<MeshRenderer>(); renderer.sharedMaterial = mat;
            var hitbox = Node("Hitbox", root.transform, Vector3.zero).gameObject.AddComponent<Hitbox>(); SetInt(hitbox, "targetMask", 1 << LayerMask.NameToLayer("Hurtbox"));
            Set(strike, "hitbox", hitbox); Set(strike, "warningRenderer", renderer); Set(strike, "impactPrefab", Fx("Fx_ImpactRing"));
            Set(strike, "areaFxPrefab", areaFx);
            string leafMeshPath = Model + "/LeafBlade.asset";
            var leafMesh = AssetDatabase.LoadAssetAtPath<Mesh>(leafMeshPath);
            if (leafMesh == null)
            {
                leafMesh = new Mesh { name = "LeafBlade", vertices = new[] { new Vector3(-0.5f,0f,0f),new Vector3(0f,0f,0.5f),new Vector3(0.5f,0f,0f),new Vector3(0f,0f,-0.5f) }, triangles = new[] {0,1,2,0,2,3} };
                leafMesh.RecalculateNormals(); AssetDatabase.CreateAsset(leafMesh,leafMeshPath);
            }
            var blade = new GameObject("Leaf",typeof(MeshFilter),typeof(MeshRenderer)); blade.transform.SetParent(root.transform,false);
            blade.transform.localScale = new Vector3(1f,0.08f,0.6f); blade.GetComponent<MeshFilter>().sharedMesh = leafMesh;
            var leafMaterial = Mat("LeafBlade",new Color(0.7f,1f,0.4f)); leafMaterial.SetFloat("_Cull",0f);
            leafMaterial.SetColor("_EmissionColor",new Color(0.7f,1f,0.4f)*1.5f); EditorUtility.SetDirty(leafMaterial);
            blade.GetComponent<MeshRenderer>().sharedMaterial = leafMaterial;
            var trail = blade.AddComponent<TrailRenderer>(); trail.sharedMaterial = TrailMaterial(); trail.time = 0.22f;
            trail.startWidth = 0.4f; trail.endWidth = 0f; trail.minVertexDistance = 0.1f;
            trail.startColor = new Color(0.6f,1f,0.3f,0.8f); trail.endColor = new Color(0.4f,0.8f,0.2f,0f);
            blade.SetActive(false); Set(strike, "projectileVisual", blade.transform);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, AreaPath); Object.DestroyImmediate(root); return prefab.GetComponent<AreaStrike>();
        }
        static AttackAreaFx BuildAreaFx()
        {
            var root = new GameObject("Fx_GuardianArea"); var fx = root.AddComponent<AttackAreaFx>();
            Set(fx,"filter",root.AddComponent<MeshFilter>()); Set(fx,"surface",root.AddComponent<MeshRenderer>());
            var line = root.AddComponent<LineRenderer>(); line.useWorldSpace = true; line.sharedMaterial = TrailMaterial();
            line.widthMultiplier = 0.45f; line.positionCount = 0; line.enabled = false;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Set(fx,"vine",line); Set(fx,"rootMaterial",Mat("RootBurst",new Color(0.23f,0.09f,0.025f)));
            var leaf = Mat("FallingLeaves",new Color(0.5f,0.85f,0.18f)); leaf.SetFloat("_Cull",0f); EditorUtility.SetDirty(leaf);
            Set(fx,"leafMaterial",leaf);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root,AreaFxPath); Object.DestroyImmediate(root); return prefab.GetComponent<AttackAreaFx>();
        }
        static Material TrailMaterial()
        {
            string path = Model+"/Materials/VineTrail.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if(m!=null) return m;
            m = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            m.SetColor("_BaseColor",Color.white); m.SetFloat("_Surface",1f); m.SetFloat("_ZWrite",0f);
            m.SetFloat("_SrcBlend",(float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend",(float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.renderQueue = 3000;
            AssetDatabase.CreateAsset(m,path); return m;
        }
        static void EnsurePresentationData()
        {
            foreach(var name in new[] {"VineSweep","VineSlam","LeafScatter","LeafStorm","RootThrust","RootRing"})
            {
                var d=AssetDatabase.LoadAssetAtPath<GuardianMoveData>(Root+"/Moves/Phase1/"+name+".asset");
                d.visualStyle=name.StartsWith("Vine")?AreaStrikeStyle.Vine:name.StartsWith("Leaf")?AreaStrikeStyle.Leaf:AreaStrikeStyle.Root;
                EditorUtility.SetDirty(d);
            }
            var b=AssetDatabase.LoadAssetAtPath<RootBarrageData>(Root+"/Moves/RootBarrage.asset"); b.visualStyle=AreaStrikeStyle.Root; EditorUtility.SetDirty(b);
        }
        [MenuItem("Tools/Prologue/Refresh Guardian Presentation (T28)")]
        public static void RefreshPresentation()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("請先停止 Play。");
            EnsurePresentationData();
            var data=AssetDatabase.LoadAssetAtPath<GuardianBattleData>(BattlePath); data.startAfterTutorial=true; EditorUtility.SetDirty(data);
            var area=BuildArea();
            var prefab=PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                foreach(var m in prefab.GetComponentsInChildren<GuardianMove>())Set(m,"areaPrefab",area);
                PrefabUtility.SaveAsPrefabAsset(prefab,PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
            // 通常相同名稱的 prefab 元件會保留 fileID；如有差異，只補接線，不重建教學或守衛。
            bool changed=WireTutorial();
            foreach(var m in Object.FindObjectsByType<GuardianMove>(FindObjectsInactive.Include,FindObjectsSortMode.None))
                if(new SerializedObject(m).FindProperty("areaPrefab").objectReferenceValue!=area){Set(m,"areaPrefab",area);changed=true;}
            var controller=Object.FindAnyObjectByType<GuardianPhaseController>();
            if(controller!=null && new SerializedObject(controller).FindProperty("guidancePrefab").objectReferenceValue!=area){Set(controller,"guidancePrefab",area);changed=true;}
            AssetDatabase.SaveAssets();
            if(changed){var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);}
            Debug.Log("[T28] 已接教學後自動開戰，更新逐招姿勢、粗邊預警、藤蔓掃痕、葉片及立體根刺。");
        }
        /// <summary>重建教學時只補守衛的事件來源引用，不重建守衛物件。</summary>
        public static bool WireTutorial()
        {
            var controller=Object.FindAnyObjectByType<GuardianPhaseController>();
            var tutorial=Object.FindAnyObjectByType<TutorialDirector>();
            if(controller==null || tutorial==null)return false;
            if(new SerializedObject(controller).FindProperty("tutorial").objectReferenceValue==tutorial)return false;
            Set(controller,"tutorial",tutorial); return true;
        }
        static GameObject BuildActor(BossStats stats, ArenaData arena, GuardianBattleData battle, AreaStrike area)
        {
            var root = new GameObject("TreeGuardian"); root.layer = LayerMask.NameToLayer("ArenaWall");
            var health = root.AddComponent<BossHealth>(); var boss = root.AddComponent<BossController>(); var stagger = root.AddComponent<BossStagger>();
            root.AddComponent<ExecutionTarget>(); var lockOn = root.AddComponent<LockOnTarget>();
            var body = root.AddComponent<CapsuleCollider>(); body.radius = battle.bodyRadius; body.height = battle.hurtHeight; body.center = Vector3.up * battle.hurtHeight * 0.5f;
            var model = Node("Model", root.transform, Vector3.zero);
            var bark = Mat("Bark", new Color(0.12f, 0.07f, 0.035f)); var red = Mat("Crack", new Color(0.8f, 0.025f, 0.01f));
            Prim("Trunk", model, PrimitiveType.Cylinder, Vector3.up * 3f, new Vector3(2.2f, 3f, 2.2f), bark);
            for (int i = 0; i < 8; i++)
            {
                float a = i * 360f / 8f; Vector3 direction = Quaternion.Euler(0f,a,0f) * Vector3.forward;
                var crack = Prim("Crack_" + i, model, PrimitiveType.Cube, direction * 1.11f + Vector3.up * 3f, new Vector3(0.08f, 4.5f, 0.08f), red);
                crack.transform.localRotation = Quaternion.Euler(0f,a,12f);
                var rootVine = Prim("Root_" + i, model, PrimitiveType.Capsule, direction * 1.3f + Vector3.up * 0.25f, new Vector3(0.35f,1.2f,0.35f), bark);
                rootVine.transform.localRotation = Quaternion.Euler(70f,a,0f);
            }
            var crown = Node("Crown", model, Vector3.up * 7.5f);
            var green = Mat("LeafGreen", new Color(0.08f,0.22f,0.035f)); var leafRed = Mat("LeafRed", new Color(0.4f,0.025f,0.01f));
            Prim("CrownCenter", crown, PrimitiveType.Sphere, Vector3.zero, new Vector3(5f,5f,5f), green);
            foreach (float side in new[] { -1f,1f }) Prim("CrownSide", crown, PrimitiveType.Sphere, new Vector3(side * 1.8f,-0.4f,0f), new Vector3(3.5f,3.5f,3.5f), side < 0f ? leafRed : green);
            var vines = new List<Transform>();
            foreach (float side in new[] { -1f,1f })
            {
                var vine = Node("Vine", model, new Vector3(side,4f,0f)); vines.Add(vine);
                Prim("Branch", vine, PrimitiveType.Capsule, new Vector3(0f,0f,2.5f), new Vector3(0.35f,3f,0.35f), bark).transform.localRotation = Quaternion.Euler(90f,0f,0f);
                Prim("Eye", model, PrimitiveType.Sphere, new Vector3(side * 0.32f,1.5f,1.08f), new Vector3(0.25f,0.15f,0.15f), red);
            }
            var stars = Node("Stars", model, Vector3.up * 2.6f);
            for (int i = 0; i < 5; i++) Prim("Star", stars, PrimitiveType.Sphere, Quaternion.Euler(0,i*72f,0)*Vector3.forward*0.7f, Vector3.one*0.18f, Mat("StunGold", new Color(1f,0.8f,0.1f)));
            stars.gameObject.SetActive(false);
            var aim = Node("AimPoint", root.transform, new Vector3(0f,1.4f,0.8f)); Set(lockOn,"aimPoint",aim);
            var hurt = Node("Hurtbox",root.transform,Vector3.zero).gameObject; hurt.layer=LayerMask.NameToLayer("Hurtbox");
            var collider = hurt.AddComponent<CapsuleCollider>(); collider.isTrigger=true; collider.radius=battle.bodyRadius; collider.height=battle.hurtHeight; collider.center=Vector3.up*battle.hurtHeight*0.5f;
            Set(hurt.AddComponent<Hurtbox>(),"owner",health);
            Set(health,"stats",stats); Set(boss,"stats",stats); Set(boss,"arena",arena); Set(boss,"body",body); Set(stagger,"stats",stats);
            var movesRoot = Node("Moves",root.transform,Vector3.zero); var moves = new List<BossMove>();
            void Move<T>(string name, string path) where T : GuardianMove
            {
                var m=Node(name,movesRoot,Vector3.zero).gameObject.AddComponent<T>(); Set(m,"data",AssetDatabase.LoadAssetAtPath<GuardianMoveData>(path)); Set(m,"areaPrefab",area); moves.Add(m);
            }
            Move<VineSweepMove>("VineSweep",Root+"/Moves/Phase1/VineSweep.asset"); Move<VineSlamMove>("VineSlam",Root+"/Moves/Phase1/VineSlam.asset");
            Move<LeafScatterMove>("LeafScatter",Root+"/Moves/Phase1/LeafScatter.asset"); Move<LeafStormMove>("LeafStorm",Root+"/Moves/Phase1/LeafStorm.asset");
            Move<RootThrustMove>("RootThrust",Root+"/Moves/Phase1/RootThrust.asset"); Move<RootRingMove>("RootRing",Root+"/Moves/Phase1/RootRing.asset");
            Move<RootBarrageMove>("RootBarrage",Root+"/Moves/RootBarrage.asset"); SetArray(boss,"moves",moves.ToArray());
            var flash=root.AddComponent<BossTelegraphFlash>(); Set(flash,"boss",boss); SetArray(flash,"renderers",model.GetComponentsInChildren<Renderer>(true));
            var hit=root.AddComponent<BossHitFeedback>(); Set(hit,"boss",boss); Set(hit,"sparkPrefab",Fx("Fx_HitSpark")); SetBool(hit,"externalEvadeFeedback",true);
            var damage=root.AddComponent<BossDamageFeedback>(); Set(damage,"health",health); Set(damage,"flash",flash); Set(damage,"sparkPrefab",Fx("Fx_HitSpark"));
            Set(damage,"numberPrefab",AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Hazards/Fx_DamageNumber.prefab").GetComponent<DamageNumber>());
            var emitter=root.AddComponent<FeedbackEmitter>(); Set(emitter,"data",AssetDatabase.LoadAssetAtPath<CombatFeedbackData>(FeedbackPath)); Set(emitter,"impulse",root.AddComponent<CinemachineImpulseSource>());
            root.AddComponent<BossImpactFeedback>();
            var poser=root.AddComponent<GuardianPoser>(); Set(poser,"boss",boss); Set(poser,"stagger",stagger); Set(poser,"crown",crown); Set(poser,"stars",stars); SetArray(poser,"vines",vines.ToArray());
            var prefab=PrefabUtility.SaveAsPrefabAsset(root,PrefabPath); Object.DestroyImmediate(root); return prefab;
        }

        static void BuildUI(GuardianPhaseController controller, BossController boss, BattleUIData data)
        {
            var root=new GameObject("GuardianUI",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));
            root.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay; root.GetComponent<Canvas>().sortingOrder=7;
            root.GetComponent<CanvasScaler>().uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize; root.GetComponent<CanvasScaler>().referenceResolution=new Vector2(1920f,1080f);
            var bossRoot=new GameObject("Boss",typeof(RectTransform),typeof(CanvasGroup)); bossRoot.transform.SetParent(root.transform,false);
            var group=bossRoot.GetComponent<CanvasGroup>();
            var rect=(RectTransform)bossRoot.transform; rect.anchorMin=Vector2.zero; rect.anchorMax=Vector2.one; rect.offsetMin=rect.offsetMax=Vector2.zero;
            BattleUIBuilder.BuildStandaloneBossHud(rect,data,boss.GetComponent<BossHealth>(),boss.GetComponent<BossStagger>());
            var hud=group.GetComponentInChildren<BossHudUI>(); Set(hud,"inputNames",AssetDatabase.LoadAssetAtPath<InputNameData>("Assets/ScriptableObjects/UI/InputNameData.asset")); Set(hud,"hintDevice",Object.FindAnyObjectByType<InputHintDevice>());
            var hint=new GameObject("Hint",typeof(RectTransform),typeof(Text)).GetComponent<Text>(); hint.transform.SetParent(root.transform,false);
            hint.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); hint.fontSize=32; hint.alignment=TextAnchor.MiddleCenter; hint.color=Color.white; hint.raycastTarget=false;
            hint.rectTransform.anchorMin=hint.rectTransform.anchorMax=new Vector2(0.5f,1f); hint.rectTransform.anchoredPosition=new Vector2(0f,-120f); hint.rectTransform.sizeDelta=new Vector2(1500f,70f);
            var ui=root.AddComponent<GuardianBattleUI>(); Set(ui,"guardian",controller); Set(ui,"bossGroup",group); Set(ui,"hint",hint);
            var executionRoot=new GameObject("GuardianExecution",typeof(RectTransform)); executionRoot.transform.SetParent(root.transform,false);
            var er=(RectTransform)executionRoot.transform; er.anchorMin=Vector2.zero;er.anchorMax=Vector2.one;er.offsetMin=er.offsetMax=Vector2.zero;
            var marker=new GameObject("Marker",typeof(RectTransform),typeof(Image)); marker.transform.SetParent(executionRoot.transform,false);
            var image=marker.GetComponent<Image>(); image.sprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Models/UI/Ui_Ring.png"); image.color=data.executionMarkerColor; image.raycastTarget=false;
            var r=(RectTransform)marker.transform; r.anchorMin=r.anchorMax=new Vector2(0.5f,0.5f); r.sizeDelta=new Vector2(64f,64f);
            var execution=executionRoot.AddComponent<ExecutionMarkerUI>(); Set(execution,"data",data); Set(execution,"target",boss.GetComponent<ExecutionTarget>()); Set(execution,"aimPoint",boss.GetComponent<LockOnTarget>().AimPoint); Set(execution,"viewCamera",UnityEngine.Camera.main); Set(execution,"marker",r); SetArray(execution,"tinted",new[] { image });
        }
        public static void EnsureDebugActions()
        {
            const string path="Assets/InputSystem_Actions.inputactions";
            // Editor 的 Project-wide Actions 可能已啟用；修改離線複本再匯入，不能動正在啟用的實例。
            var source=AssetDatabase.LoadAssetAtPath<InputActionAsset>(path);
            var actions=InputActionAsset.FromJson(source.ToJson()); var map=actions.FindActionMap("Player",true); bool changed=false;
            foreach(var pair in new[] { ("DebugGuardianBattle","<Keyboard>/f1"),("DebugGuardianBarrage","<Keyboard>/f3"),("DebugGuardianNextMove","<Keyboard>/f4") })
                if(map.FindAction(pair.Item1,false)==null) { map.AddAction(pair.Item1,InputActionType.Button).AddBinding(pair.Item2); changed=true; }
            if(changed) { System.IO.File.WriteAllText(path,actions.ToJson()); AssetDatabase.ImportAsset(path); }
            Object.DestroyImmediate(actions);
        }
        public static string Validate()
        {
            var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if(scene.path!=PrologueBuilder.ScenePath) throw new InvalidOperationException("需在 Prologue 核對。");
            var nodes=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).ToArray();
            int missing=nodes.Sum(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject));
            if(missing!=0) throw new InvalidOperationException("Missing Script: "+missing);
            if(Object.FindAnyObjectByType<BattleManager>()!=null) throw new InvalidOperationException("序章不能掛一般 BattleManager。");
            var controller=Object.FindAnyObjectByType<GuardianPhaseController>(); var boss=controller.Boss;
            Require(controller,"data","boss","health","stagger","barrage","session","tutorial","guidancePrefab","hurtCollider","lockTarget","model","hitFeedback","options","difficulty");
            Require(Object.FindAnyObjectByType<PrologueOutcome>(),"guardian","session");
            if(boss.GetComponentsInChildren<GuardianMove>().Length!=7) throw new InvalidOperationException("招式數量不符。");
            foreach(var m in boss.GetComponentsInChildren<GuardianMove>()) { Require(m,"data","areaPrefab"); if(new SerializedObject(m).FindProperty("obstacles").arraySize==0) throw new InvalidOperationException("未接場地障礙。"); }
            var stats=AssetDatabase.LoadAssetAtPath<BossStats>(Root+"/Stats.asset");
            if(stats.maxHealth!=3000f || stats.phases[0].imbalanceMax!=200f || !stats.phases[0].stationary) throw new InvalidOperationException("Boss 數值不符。");
            var definition=AssetDatabase.LoadAssetAtPath<BossDefinition>(DefinitionPath);
            if(definition.showInPortal || definition.firstClearRewards.Count!=0) throw new InvalidOperationException("再戰／獎勵設定不符。");
            var spawner=Object.FindAnyObjectByType<PlayerSpawner>(); var list=new SerializedObject(spawner).FindProperty("receivers");
            foreach(var receiver in new MonoBehaviour[] {controller,Object.FindAnyObjectByType<PrologueOutcome>()})
            { bool found=false; for(int i=0;i<list.arraySize;i++) found|=list.GetArrayElementAtIndex(i).objectReferenceValue==receiver; if(!found)throw new InvalidOperationException("Spawner 缺接收者。"); }
            var errors=GameDatabaseValidator.Validate(AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/ScriptableObjects/Progression/GameDatabase.asset"),false);
            if(errors.Count>0) throw new InvalidOperationException(string.Join(";",errors));
            ValidateDataAndFeedback();
            ValidatePresentation();
            return $"T28 PASS (non-Play): {scene.path}; MissingScript=0; seven moves; refs/spawner/database/SFX/shader OK; HP3000/200; six move values + plan A match; no BattleManager/no rewards/no portal replay.";
        }
        public static string ValidateDataAndFeedback()
        {
            var rows=new[] { ("VineSweep",38f,0.5f,4f,0.8f,6f,2f,true),("VineSlam",32f,0.4f,5f,0.7f,5f,2f,true),
                ("LeafScatter",25f,0.5f,4f,0.7f,2f,4f,true),("LeafStorm",28f,0.8f,7f,1f,1f,4f,true),
                ("RootThrust",42f,0.5f,6f,0.9f,2f,5f,false),("RootRing",35f,0.7f,8f,0.9f,1f,4f,false) };
            foreach(var row in rows)
            {
                var d=AssetDatabase.LoadAssetAtPath<GuardianMoveData>(Root+"/Moves/Phase1/"+row.Item1+".asset");
                if(d==null || d.damage!=row.Item2 || d.telegraphTime!=row.Item3 || d.cooldown!=row.Item4 || d.recoveryTime!=row.Item5 || d.nearWeight!=row.Item6 || d.farWeight!=row.Item7 || d.canBlock!=row.Item8 || d.canParry!=row.Item8 || d.hitWindow!=0.15f)
                    throw new InvalidOperationException("初版招式數值不符："+row.Item1);
            }
            var b=AssetDatabase.LoadAssetAtPath<RootBarrageData>(Root+"/Moves/RootBarrage.asset");
            if(b.damage!=42f || b.canBlock || b.canParry || b.outerWarning!=1f || b.innerWarning!=0.8f || b.crossWarning!=0.7f || b.warning!=0.35f || b.sampleLead!=0.1f || b.finalWarning!=0.6f || b.safeCount!=3 || b.safeRadius!=1.5f || b.reachableDistance!=2f || b.count!=5 || b.reach!=9f || b.innerRadius!=6f || b.width!=4f)
                throw new InvalidOperationException("萬根方案 A 數值不符。");
            var battle=AssetDatabase.LoadAssetAtPath<GuardianBattleData>(BattlePath);
            if(battle.phaseDuration!=60f || battle.guidanceAt!=56f || battle.guidanceRadius!=9f || battle.healthFloorRatio!=0.8f)throw new InvalidOperationException("一階段門檻數值不符。");
            var data=AssetDatabase.LoadAssetAtPath<CombatFeedbackData>(FeedbackPath);
            var cues=new[] {data.swing,data.heavySwing,data.hit,data.heavyHit,data.executionHit,data.hitDeflected,data.hurt,data.block,data.parry,data.evade,data.roll,data.telegraphBlockable,data.telegraphUnblockable,data.pawSwipe,data.stunned};
            if(cues.Any(c=>c.clips==null || c.clips.Length==0 || c.clips.Any(clip=>clip==null)))throw new InvalidOperationException("回饋音效欄位留空。");
            var actor=Object.FindAnyObjectByType<GuardianPhaseController>();
            if(actor!=null)
            {
                Require(actor.Boss.GetComponent<FeedbackEmitter>(),"data","audioManager","impulse");
                Require(actor.Boss.GetComponent<BossDamageFeedback>(),"health","flash","sparkPrefab","numberPrefab","viewCamera");
                Require(actor.Boss.GetComponent<BossHitFeedback>(),"boss","sparkPrefab");
            }
            var shader=AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/GuardianTelegraph.shader");
            if(shader==null || ShaderUtil.GetShaderMessages(shader).Length>0)throw new InvalidOperationException("預警 Shader 有編譯訊息。");
            var oiia=AssetDatabase.LoadAssetAtPath<BossPhaseData>("Assets/ScriptableObjects/Boss/Phases/Phase1.asset");
            var cat=AssetDatabase.LoadAssetAtPath<BossDefinition>("Assets/ScriptableObjects/Progression/Bosses/OiiaCat.asset");
            if(oiia.stationary || oiia.chainChance!=0f || oiia.turnSpeedOverride!=0f || !cat.showInPortal)throw new InvalidOperationException("OIIA 預設路徑被改動。");
            return "T28 data/feedback PASS: six moves + plan A; 15 SFX; OIIA defaults; shader no diagnostics.";
        }
        public static string ValidatePresentation()
        {
            var area=AssetDatabase.LoadAssetAtPath<GameObject>(AreaPath);
            var fx=AssetDatabase.LoadAssetAtPath<GameObject>(AreaFxPath);
            Require(area.GetComponent<AreaStrike>(),"areaFxPrefab","projectileVisual","warningRenderer");
            Require(fx.GetComponent<AttackAreaFx>(),"filter","surface","vine","rootMaterial","leafMaterial");
            if(area.transform.Find("Leaf").GetComponent<TrailRenderer>()==null)throw new InvalidOperationException("葉刃缺少拖尾。");
            foreach(var n in new[]{"VineSweep","VineSlam","LeafScatter","LeafStorm","RootThrust","RootRing"})
            {
                var d=AssetDatabase.LoadAssetAtPath<GuardianMoveData>(Root+"/Moves/Phase1/"+n+".asset");
                var expected=n.StartsWith("Vine")?AreaStrikeStyle.Vine:n.StartsWith("Leaf")?AreaStrikeStyle.Leaf:AreaStrikeStyle.Root;
                if(d.visualStyle!=expected)throw new InvalidOperationException(n+" 表現分類錯誤。");
            }
            return "T28 presentation PASS: vine/leaf/root styles; 3D FX prefab/materials; leaf trail; tutorial completion subscribed in controller.";
        }
        static void Require(Object o,params string[] fields) { if(o==null)throw new InvalidOperationException("缺元件"); var so=new SerializedObject(o); foreach(var f in fields)if(so.FindProperty(f)?.objectReferenceValue==null)throw new InvalidOperationException(o.name+" 缺 "+f); }
        static T Get<T>(string path,Action<T> initialize=null) where T:ScriptableObject { var a=AssetDatabase.LoadAssetAtPath<T>(path); if(a!=null)return a; a=ScriptableObject.CreateInstance<T>(); initialize?.Invoke(a); AssetDatabase.CreateAsset(a,path);return a; }
        static TimedFx Fx(string name)=>AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Hazards/"+name+".prefab").GetComponent<TimedFx>();
        static Material Mat(string name,Color color)
        { string path=Model+"/Materials/"+name+".mat"; var m=AssetDatabase.LoadAssetAtPath<Material>(path); if(m!=null)return m; m=new Material(Shader.Find("Universal Render Pipeline/Lit"));m.SetColor("_BaseColor",color);m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",color*0.3f);AssetDatabase.CreateAsset(m,path);return m; }
        static Transform Node(string name,Transform parent,Vector3 position) { var t=new GameObject(name).transform;t.SetParent(parent,false);t.localPosition=position;return t; }
        static GameObject Prim(string name,Transform parent,PrimitiveType type,Vector3 pos,Vector3 scale,Material mat) { var g=GameObject.CreatePrimitive(type);g.name=name;g.transform.SetParent(parent,false);g.transform.localPosition=pos;g.transform.localScale=scale;g.GetComponent<Renderer>().sharedMaterial=mat;Object.DestroyImmediate(g.GetComponent<Collider>());return g; }
        static void Set(Object o,string field,Object value) { var s=new SerializedObject(o);var p=s.FindProperty(field);if(p==null)throw new InvalidOperationException(o.name+":"+field);p.objectReferenceValue=value;s.ApplyModifiedPropertiesWithoutUndo(); }
        static void SetInt(Object o,string field,int value) { var s=new SerializedObject(o);s.FindProperty(field).intValue=value;s.ApplyModifiedPropertiesWithoutUndo(); }
        static void SetBool(Object o,string field,bool value) { var s=new SerializedObject(o);s.FindProperty(field).boolValue=value;s.ApplyModifiedPropertiesWithoutUndo(); }
        static void SetArray<T>(Object o,string field,T[] values) where T:Object { var s=new SerializedObject(o);var p=s.FindProperty(field);p.arraySize=values.Length;for(int i=0;i<values.Length;i++)p.GetArrayElementAtIndex(i).objectReferenceValue=values[i];s.ApplyModifiedPropertiesWithoutUndo(); }
    }
}
