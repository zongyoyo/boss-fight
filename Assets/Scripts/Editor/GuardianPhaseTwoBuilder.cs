using System;
using System.Linq;
using BossFight.Battle;
using BossFight.Boss;
using BossFight.Boss.Moves;
using BossFight.Cameras;
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
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace BossFight.EditorTools
{
    /// <summary>T29 定點補上二階段，不重建一階段、教學、地形或 Hub。</summary>
    public static class GuardianPhaseTwoBuilder
    {
        public const string PhasePath = GuardianBuilder.Root + "/Phases/Phase2.asset";
        public const string MovesPath = GuardianBuilder.Root + "/Moves/Phase2/";
        public const string SeedPath = "Assets/Prefabs/Hazards/GuardianSeed.prefab";
        public const string BarkPath = "Assets/Prefabs/Hazards/Fx_GuardianBark.prefab";
        public const string ResultDataPath = "Assets/ScriptableObjects/UI/GuardianVictoryUI.asset";
        const string FeedbackPath = "Assets/ScriptableObjects/Feedback/CombatFeedback.asset";

        [MenuItem("Tools/Prologue/Build Guardian Phase 2 (T29)")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("請先停止 Play。");
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != PrologueBuilder.ScenePath) throw new InvalidOperationException("請開啟 Prologue。");
            DaoDunDogBuilder.EnsureFolder(MovesPath.TrimEnd('/'));
            var guardian = Object.FindAnyObjectByType<GuardianPhaseController>();
            if (guardian == null) throw new InvalidOperationException("請先完成 T28 守衛。");
            if (guardian.Data.transitionText.Contains("T29")) guardian.Data.transitionText = "守衛準備全力應戰……";
            if (guardian.Data.placeholderVictoryHint.Contains("T29")) guardian.Data.placeholderVictoryHint = "大樹守衛倒下了……";
            var stats = AssetDatabase.LoadAssetAtPath<BossStats>(GuardianBuilder.Root + "/Stats.asset");
            var phase = Get<BossPhaseData>(PhasePath, p => {
                p.displayName = "全力"; p.movePool = 2; p.healthThreshold = 0f; p.transitionTime = 5f;
                p.stationary = true; p.moveSpeed = 0f; p.turnSpeedOverride = 180f;
                p.moveIntervalMin = 0.3f; p.moveIntervalMax = 0.6f; p.chainChance = 0.5f;
                p.imbalanceMax = 240f; p.stunDuration = 2.5f; p.repositionTimeout = 0f; p.musicSpeed = 1.2f;
                p.openingSequence = Array.Empty<BossMoveData>();
            });
            stats.phases = new[] { stats.phases[0], phase }; EditorUtility.SetDirty(stats);
            var arena = AssetDatabase.LoadAssetAtPath<ArenaData>(GuardianBuilder.Root + "/Arena.asset");
            var layout = AssetDatabase.LoadAssetAtPath<PrologueLayoutData>(PrologueBuilder.LayoutPath);
            EnsureMoves(arena.radius + layout.guardianPosition.magnitude);
            var seed = BuildSeed(); var bark = BuildBark();
            AddPrefabMoves(seed, bark);
            var boss = guardian.Boss;
            var root = guardian.gameObject;
            var outcome = root.GetComponent<PrologueOutcome>();
            var phaseTwo = DaoDunDogBuilder.GetOrAdd<GuardianPhaseTwo>(root);
            var summons = DaoDunDogBuilder.GetOrAdd<GuardianSummonController>(root);
            var session = Object.FindAnyObjectByType<PrologueSession>();
            var audio = Object.FindAnyObjectByType<AudioManager>();
            Set(phaseTwo,"guardian",guardian); Set(phaseTwo,"session",session);
            Set(phaseTwo,"dialogue",new SerializedObject(Object.FindAnyObjectByType<TutorialDirector>()).FindProperty("dialogue").objectReferenceValue);
            Set(phaseTwo,"cameraController",Object.FindAnyObjectByType<CameraController>());
            Set(phaseTwo,"audioManager",audio); Set(phaseTwo,"wavePrefab",Fx("Fx_ImpactRing"));
            Set(phaseTwo,"guardianFace",boss.GetComponent<LockOnTarget>().AimPoint);
            var shot = root.transform.Find("Phase2Shot");
            if (shot == null) shot = new GameObject("Phase2Shot").transform;
            shot.SetParent(root.transform, false);
            shot.position = boss.transform.position + new Vector3(0f, 2f, -8f);
            shot.rotation = Quaternion.LookRotation(boss.transform.position + Vector3.up * 5f - shot.position);
            var camera = DaoDunDogBuilder.GetOrAdd<CinemachineCamera>(shot.gameObject); camera.Priority = 0;
            Set(phaseTwo,"shot",camera);
            Set(outcome,"bossHealth",boss.GetComponent<BossHealth>());
            Set(outcome,"definition",AssetDatabase.LoadAssetAtPath<BossDefinition>(GuardianBuilder.DefinitionPath));
            Set(outcome,"sceneFlow",Object.FindAnyObjectByType<SceneFlow>()); Set(outcome,"audioManager",audio);
            Set(outcome,"options",Object.FindAnyObjectByType<GameOptions>());
            var move = boss.GetComponentInChildren<CallChildrenMove>(true);
            Set(summons,"guardian",guardian); Set(summons,"move",move);
            var angry = AssetDatabase.LoadAssetAtPath<TrainingSaplingData>(PrologueActorsBuilder.AngryDataPath);
            Set(summons,"data",angry);
            var pool = root.transform.Find("CombatChildren");
            if (pool == null) { pool = new GameObject("CombatChildren").transform; pool.SetParent(root.transform,false); }
            var children = new TrainingSapling[((CallChildrenData)move.Data).maxChildren];
            for (int i=0; i<children.Length; i++)
            {
                var t=pool.Find("Child_"+i);
                if(t==null)
                {
                    var go=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrologueActorsBuilder.SaplingPrefabPath),pool);
                    go.name="Child_"+i; t=go.transform;
                }
                var child=t.GetComponent<TrainingSapling>(); children[i]=child;
                Set(child,"data",angry); SetBool(child,"appearOnStart",false);
                var emitter=DaoDunDogBuilder.GetOrAdd<FeedbackEmitter>(child.gameObject);
                Set(emitter,"data",AssetDatabase.LoadAssetAtPath<CombatFeedbackData>(FeedbackPath));
                Set(emitter,"audioManager",audio); Set(emitter,"impulse",DaoDunDogBuilder.GetOrAdd<CinemachineImpulseSource>(child.gameObject));
                var feedback=DaoDunDogBuilder.GetOrAdd<TeachingActorFeedback>(child.gameObject);
                Set(feedback,"sapling",child); Set(feedback,"sparkPrefab",Fx("Fx_HitSpark")); Set(feedback,"impactAnchor",t.Find("AimPoint"));
                PrologueActorsBuilder.WireSaplingFeedback(child);
                var m=t.Find("Model"); m.gameObject.SetActive(false);
            }
            SetArray(summons,"children",children);
            var obstacles=boss.GetComponentInChildren<RootBarrageMove>();
            var obstacleList=new SerializedObject(obstacles).FindProperty("obstacles");
            var cols=new Collider[obstacleList.arraySize];
            for(int i=0;i<cols.Length;i++)cols[i]=(Collider)obstacleList.GetArrayElementAtIndex(i).objectReferenceValue;
            foreach(var m in boss.GetComponentsInChildren<GuardianMove>(true)) if(m.Data.phase==2) SetArray(m,"obstacles",cols);
            BuildUI(guardian,outcome,boss);
            EnsureAction();
            var debug=root.GetComponent<GuardianDebugTools>(); Set(debug,"phaseTwo",phaseTwo); Set(debug,"outcome",outcome);
            PlayerSpawnerBuilder.Wire();
            EditorUtility.SetDirty(guardian.Data);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            Debug.Log(Validate());
        }
        static void EnsureMoves(float range)
        {
            void Common(GuardianMoveData d,string title,float damage,bool block,float telegraph,float cooldown,float recovery,float near,float far,float max)
            {
                d.displayName=title; d.phase=2; d.damage=damage; d.canBlock=d.canParry=block;
                d.telegraphTime=telegraph; d.cooldown=cooldown; d.recoveryTime=recovery;
                d.distanceWeights=true; d.nearDistance=6f; d.nearWeight=near; d.farWeight=far; d.maxDistance=max;
                d.hitWindow=0.15f; d.hitHeight=2f;
            }
            Get<VineSweepData>(MovesPath+"VineSweep.asset",d=>{Common(d,"藤蔓迴旋",38f,true,0.35f,4f,0.6f,6f,2f,9f);d.radius=9f;d.angle=150f;d.secondAngle=360f;d.count=2;d.interval=0.35f;d.visualStyle=AreaStrikeStyle.Vine;});
            Get<VineSlamData>(MovesPath+"VineSlam.asset",d=>{Common(d,"藤蔓連砸",32f,true,0.3f,5f,0.5f,5f,2f,12f);d.reach=12f;d.width=1.5f;d.sideOffset=1.5f;d.count=4;d.interval=0.25f;d.trackLast=true;d.visualStyle=AreaStrikeStyle.Vine;});
            Get<LeafScatterData>(MovesPath+"LeafScatter.asset",d=>{Common(d,"葉刃暴雨",25f,true,0.35f,4f,0.45f,2f,4f,range);d.angle=80f;d.count=9;d.waves=2;d.interval=0.35f;d.angleOffset=5f;d.speed=14f;d.reach=0.6f;d.width=1f;d.hitHeight=1.6f;d.centerHeight=0.8f;d.visualStyle=AreaStrikeStyle.Leaf;});
            Get<LeafStormData>(MovesPath+"LeafStorm.asset",d=>{Common(d,"落葉狂風",28f,true,0.6f,6f,0.6f,1f,4f,range);d.radius=1.5f;d.count=14;d.waves=2;d.interval=0.35f;d.warning=0.6f;d.nearCount=2;d.scatter=2f;d.visualStyle=AreaStrikeStyle.Leaf;});
            Get<RootThrustData>(MovesPath+"RootThrust.asset",d=>{Common(d,"樹根追獵",42f,false,0.35f,5f,0.5f,2f,5f,range);d.radius=1.5f;d.count=5;d.interval=d.warning=0.35f;});
            Get<RootRingData>(MovesPath+"RootRing.asset",d=>{Common(d,"樹根三重環",35f,false,0.5f,7f,0.6f,1f,4f,range);d.innerRadius=2f;d.width=0.6f;d.speed=9f;d.count=3;d.interval=0.4f;d.reverseSecond=true;});
            Get<SeedBombData>(MovesPath+"SeedBomb.asset",d=>{Common(d,"種子炸彈",30f,true,0.45f,9f,0.6f,1f,3f,range);d.radius=2.5f;d.count=4;d.warning=1f;d.visualStyle=AreaStrikeStyle.Seed;});
            Get<CallChildrenData>(MovesPath+"CallChildren.asset",d=>Common(d,"呼喚孩子",25f,true,0.6f,15f,0.6f,1f,2f,range));
        }
        static void AddPrefabMoves(SeedBombVisual seed,ParticleSystem bark)
        {
            var prefab=PrefabUtility.LoadPrefabContents(GuardianBuilder.PrefabPath);
            try
            {
                var parent=prefab.transform.Find("Moves");
                void Add<T>(string name) where T:GuardianMove
                {
                    string node="P2_"+name; var t=parent.Find(node);
                    if(t==null){t=new GameObject(node).transform;t.SetParent(parent,false);}
                    var m=DaoDunDogBuilder.GetOrAdd<T>(t.gameObject);
                    Set(m,"data",AssetDatabase.LoadAssetAtPath<GuardianMoveData>(MovesPath+name+".asset"));
                    Set(m,"areaPrefab",AssetDatabase.LoadAssetAtPath<AreaStrike>(GuardianBuilder.AreaPath));
                }
                Add<VineSweepMove>("VineSweep");Add<VineSlamMove>("VineSlam");Add<LeafScatterMove>("LeafScatter");Add<LeafStormMove>("LeafStorm");
                Add<RootThrustMove>("RootThrust");Add<RootRingMove>("RootRing");Add<SeedBombMove>("SeedBomb");Add<CallChildrenMove>("CallChildren");
                SetArray(prefab.GetComponent<BossController>(),"moves",prefab.GetComponentsInChildren<BossMove>(true));
                Set(prefab.GetComponentInChildren<SeedBombMove>(),"seedPrefab",seed);Set(prefab.GetComponentInChildren<SeedBombMove>(),"launchPoint",prefab.transform.Find("Model/Crown"));
                var visual=DaoDunDogBuilder.GetOrAdd<GuardianFullPowerVisual>(prefab);
                Set(visual,"boss",prefab.GetComponent<BossController>());Set(visual,"health",prefab.GetComponent<BossHealth>());Set(visual,"barkPrefab",bark);
                SetArray(visual,"crown",prefab.transform.Find("Model/Crown").GetComponentsInChildren<Renderer>());
                SetArray(visual,"cracks",prefab.GetComponentsInChildren<Renderer>().Where(r=>r.name.StartsWith("Crack_")).ToArray());
                PrefabUtility.SaveAsPrefabAsset(prefab,GuardianBuilder.PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
        }
        static SeedBombVisual BuildSeed()
        {
            var existing=AssetDatabase.LoadAssetAtPath<SeedBombVisual>(SeedPath);if(existing!=null)return existing;
            var go=GameObject.CreatePrimitive(PrimitiveType.Sphere);go.name="GuardianSeed";Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.localScale=new Vector3(0.45f,0.6f,0.45f);
            var material=new Material(Shader.Find("Universal Render Pipeline/Lit"));material.color=new Color(0.7f,0.025f,0.01f);material.EnableKeyword("_EMISSION");material.SetColor("_EmissionColor",Color.red);
            string path="Assets/Models/Guardian/Materials/Seed.mat";AssetDatabase.CreateAsset(material,path);go.GetComponent<Renderer>().sharedMaterial=material;
            go.AddComponent<SeedBombVisual>();
            var trail=go.AddComponent<TrailRenderer>();trail.time=0.25f;trail.startWidth=0.2f;trail.endWidth=0f;
            trail.sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/Models/Guardian/Materials/VineTrail.mat");trail.startColor=Color.red;trail.endColor=Color.clear;
            var saved=PrefabUtility.SaveAsPrefabAsset(go,SeedPath);Object.DestroyImmediate(go);return saved.GetComponent<SeedBombVisual>();
        }
        static ParticleSystem BuildBark()
        {
            var existing=AssetDatabase.LoadAssetAtPath<ParticleSystem>(BarkPath);if(existing!=null)return existing;
            var go=new GameObject("Fx_GuardianBark");var ps=go.AddComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=ps.main;main.duration=0.8f;main.loop=false;main.startLifetime=0.8f;main.startSpeed=2f;main.startSize=0.2f;main.gravityModifier=0.8f;main.stopAction=ParticleSystemStopAction.Destroy;main.startColor=new Color(0.22f,0.1f,0.025f);
            var emission=ps.emission;emission.rateOverTime=0f;emission.SetBursts(new[]{new ParticleSystem.Burst(0f,(short)9)});
            var renderer=ps.GetComponent<ParticleSystemRenderer>();renderer.renderMode=ParticleSystemRenderMode.Mesh;
            var cube=GameObject.CreatePrimitive(PrimitiveType.Cube);renderer.mesh=cube.GetComponent<MeshFilter>().sharedMesh;Object.DestroyImmediate(cube);
            renderer.sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/Models/Guardian/Materials/Bark.mat");
            var saved=PrefabUtility.SaveAsPrefabAsset(go,BarkPath);Object.DestroyImmediate(go);return saved.GetComponent<ParticleSystem>();
        }
        static void BuildUI(GuardianPhaseController guardian,PrologueOutcome outcome,BossController boss)
        {
            var hud=Object.FindAnyObjectByType<GuardianBattleUI>().GetComponentInChildren<BossHudUI>(true);
            var bridge=DaoDunDogBuilder.GetOrAdd<GuardianPhaseFeedbackUI>(hud.gameObject);
            Set(bridge,"guardian",guardian);Set(bridge,"health",boss.GetComponent<BossHealth>());Set(bridge,"hud",hud);
            Set(Object.FindAnyObjectByType<GuardianBattleUI>(),"outcome",outcome);
            var old=GameObject.Find("GuardianResultUI");if(old!=null)Object.DestroyImmediate(old);
            var root=new GameObject("GuardianResultUI",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            root.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;root.GetComponent<Canvas>().sortingOrder=50;
            root.GetComponent<CanvasScaler>().uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;root.GetComponent<CanvasScaler>().referenceResolution=new Vector2(1920f,1080f);
            var data=AssetDatabase.LoadAssetAtPath<BattleUIData>(ResultDataPath);
            if(data==null){data=Object.Instantiate(AssetDatabase.LoadAssetAtPath<BattleUIData>(GuardianBuilder.UIPath));data.victoryTitle="擊敗大樹守衛";data.victorySubtitle="";AssetDatabase.CreateAsset(data,ResultDataPath);}
            BattleUIBuilder.BuildStandalonePrologueResult(root.transform,data,outcome,Object.FindAnyObjectByType<EventSystem>());
        }
        static void EnsureAction()
        {
            const string path="Assets/InputSystem_Actions.inputactions";
            var copy=InputActionAsset.FromJson(AssetDatabase.LoadAssetAtPath<InputActionAsset>(path).ToJson());
            var map=copy.FindActionMap("Player",true);
            if(map.FindAction("DebugGuardianPhase2",false)==null){map.AddAction("DebugGuardianPhase2",InputActionType.Button).AddBinding("<Keyboard>/f5");System.IO.File.WriteAllText(path,copy.ToJson());AssetDatabase.ImportAsset(path);}
            Object.DestroyImmediate(copy);
        }
        public static string Validate()
        {
            var guardian=Object.FindAnyObjectByType<GuardianPhaseController>();var boss=guardian.Boss;
            int missing=UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().Sum(r=>r.GetComponentsInChildren<Transform>(true).Sum(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)));
            if(missing!=0)throw new InvalidOperationException("MissingScript="+missing);
            var moves=boss.GetComponentsInChildren<GuardianMove>(true).Where(m=>m.Data.phase==2).ToArray();
            if(moves.Length!=8 || boss.Moves.Count!=15)throw new InvalidOperationException("招式池數量不符。");
            Require(Object.FindAnyObjectByType<GuardianPhaseTwo>(),"guardian","session","dialogue","cameraController","shot","audioManager","wavePrefab","guardianFace");
            Require(Object.FindAnyObjectByType<PrologueOutcome>(),"guardian","session","bossHealth","definition","sceneFlow","audioManager","options");
            Require(Object.FindAnyObjectByType<GuardianSummonController>(),"guardian","move","data");
            foreach(var m in moves) {Require(m,"data","areaPrefab");if(new SerializedObject(m).FindProperty("obstacles").arraySize==0)throw new InvalidOperationException("缺少障礙引用");}
            Require(boss.GetComponent<GuardianFullPowerVisual>(),"boss","health","barkPrefab");
            var children=guardian.transform.Find("CombatChildren").GetComponentsInChildren<TrainingSapling>(true);
            if(children.Length!=2)throw new InvalidOperationException("召喚池不是兩隻");
            foreach(var c in children){Require(c,"data","attackHitbox","hurtCollider");Require(c.GetComponent<FeedbackEmitter>(),"data","audioManager","impulse");Require(c.GetComponent<TeachingActorFeedback>(),"sapling","sparkPrefab","impactAnchor");if(new SerializedObject(c).FindProperty("appearOnStart").boolValue)throw new InvalidOperationException("樹精不可自行冒出");}
            if(Object.FindAnyObjectByType<BattleManager>()!=null)throw new InvalidOperationException("不能使用一般 BattleManager");
            var result=Object.FindAnyObjectByType<BattleResultUI>();Require(result,"prologue","data","group","hubButton","firstClearGroup");
            if(new SerializedObject(result).FindProperty("battle").objectReferenceValue!=null)throw new InvalidOperationException("結算來源重複");
            var pool=new SerializedObject(Object.FindAnyObjectByType<PlayerSpawner>());
            foreach(var c in children)
            {
                if(!Contains(pool.FindProperty("lockTargets"),c.GetComponent<LockOnTarget>()) || !Contains(pool.FindProperty("executionTargets"),c.GetComponent<ExecutionTarget>()))throw new InvalidOperationException("召喚樹精缺目標綁定");
            }
            foreach(var c in new MonoBehaviour[]{Object.FindAnyObjectByType<GuardianPhaseTwo>(),Object.FindAnyObjectByType<GuardianSummonController>(),Object.FindAnyObjectByType<PrologueOutcome>()})
                if(!Contains(pool.FindProperty("receivers"),c))throw new InvalidOperationException("接收者未Wire");
            ValidateData();ValidatePureRules();GuardianBuilder.ValidateDataAndFeedback();GuardianBuilder.ValidatePresentation();
            return "T29 non-Play PASS: MissingScript=0; 15 moves/P2=8; scene/prefab/UI/player refs; two combat children; 15 SFX; P1 unchanged; pure snapshot/idempotency/self-cost checks. No real save or PlayerPrefs accessed.";
        }
        public static void ValidateData()
        {
            var p=AssetDatabase.LoadAssetAtPath<BossPhaseData>(PhasePath);
            if(p.movePool!=2||p.moveSpeed!=0f||!p.stationary||p.turnSpeedOverride!=180f||p.moveIntervalMin!=0.3f||p.moveIntervalMax!=0.6f||p.chainChance!=0.5f||p.imbalanceMax!=240f||p.stunDuration!=2.5f)throw new InvalidOperationException("二階段數值錯誤");
            var rows=new[]{("VineSweep",38f,0.35f,4f,0.6f,6f,2f,true),("VineSlam",32f,0.3f,5f,0.5f,5f,2f,true),("LeafScatter",25f,0.35f,4f,0.45f,2f,4f,true),("LeafStorm",28f,0.6f,6f,0.6f,1f,4f,true),("RootThrust",42f,0.35f,5f,0.5f,2f,5f,false),("RootRing",35f,0.5f,7f,0.6f,1f,4f,false),("SeedBomb",30f,0.45f,9f,0.6f,1f,3f,true),("CallChildren",25f,0.6f,15f,0.6f,1f,2f,true)};
            foreach(var r in rows){var d=AssetDatabase.LoadAssetAtPath<GuardianMoveData>(MovesPath+r.Item1+".asset");if(d.phase!=2||d.damage!=r.Item2||d.telegraphTime!=r.Item3||d.cooldown!=r.Item4||d.recoveryTime!=r.Item5||d.nearWeight!=r.Item6||d.farWeight!=r.Item7||d.canBlock!=r.Item8||d.canParry!=r.Item8||d.hitWindow!=0.15f)throw new InvalidOperationException("招式數值錯誤："+r.Item1);}
            var angry=AssetDatabase.LoadAssetAtPath<TrainingSaplingData>(PrologueActorsBuilder.AngryDataPath);
            if(angry.maxHealth!=30f||angry.damage!=25f||angry.attackPeriod!=1.5f||angry.lifetime!=15f||angry.initiallyInvulnerable||!angry.canBlock||angry.initialMode!=SaplingMode.Attack||angry.staggerMax!=100f||angry.staggerDecayDelay!=3f||angry.staggerDecayRate!=8f)throw new InvalidOperationException("戰鬥樹精數值錯誤");
            GuardianMoveData D(string name)=>AssetDatabase.LoadAssetAtPath<GuardianMoveData>(MovesPath+name+".asset");
            var sweep=D("VineSweep");var slam=D("VineSlam");var leaf=D("LeafScatter");var storm=D("LeafStorm");var thrust=D("RootThrust");var ring=D("RootRing");
            if(sweep.count!=2||sweep.radius!=9f||sweep.angle!=150f||sweep.secondAngle!=360f||sweep.interval!=0.35f||sweep.visualStyle!=AreaStrikeStyle.Vine||
                slam.count!=4||!slam.trackLast||slam.interval!=0.25f||slam.width!=1.5f||slam.sideOffset!=1.5f||slam.reach!=12f||slam.visualStyle!=AreaStrikeStyle.Vine||
                leaf.count!=9||leaf.waves!=2||leaf.interval!=0.35f||leaf.angleOffset!=5f||leaf.angle!=80f||leaf.speed!=14f||leaf.width!=1f||leaf.reach!=0.6f||leaf.centerHeight!=0.8f||leaf.hitHeight!=1.6f||
                storm.count!=14||storm.waves!=2||storm.nearCount!=2||storm.scatter!=2f||storm.interval!=0.35f||storm.warning!=0.6f||storm.radius!=1.5f||
                thrust.count!=5||thrust.interval!=0.35f||thrust.warning!=0.35f||thrust.radius!=1.5f||
                ring.count!=3||!ring.reverseSecond||ring.interval!=0.4f||ring.innerRadius!=2f||ring.width!=0.6f||ring.speed!=9f)throw new InvalidOperationException("六招強化幾何或時序不符");
            var seed=(SeedBombData)D("SeedBomb");var call=(CallChildrenData)D("CallChildren");
            if(seed.count!=4||seed.flightTime!=0.6f||seed.arcHeight!=6f||seed.landingSpread!=3f||seed.warning!=1f||seed.radius!=2.5f||seed.hitHeight!=2f||seed.visualStyle!=AreaStrikeStyle.Seed||call.maxChildren!=2||call.spawnDistance!=3f)throw new InvalidOperationException("種子或召喚設定不符");
            var battle=AssetDatabase.LoadAssetAtPath<GuardianBattleData>(GuardianBuilder.BattlePath);
            if(battle.selfCostRatio!=0.05f||battle.transitionDialogueTime!=3f||battle.transitionPushRadius!=9f||!battle.startAfterTutorial)throw new InvalidOperationException("流程或自損比例不符");
            var definition=AssetDatabase.LoadAssetAtPath<BossDefinition>(GuardianBuilder.DefinitionPath);if(definition.firstClearRewards.Count!=0||definition.showInPortal)throw new InvalidOperationException("正式獎勵應留空且不可再戰");
        }
        public static void ValidatePureRules()
        {
            float hp=2400f;for(int i=0;i<10;i++)hp=BossHealth.NonlethalSelfCost(hp,0.05f);
            if(Mathf.Abs(hp-2400f*Mathf.Pow(0.95f,10))>0.01f || BossHealth.NonlethalSelfCost(float.Epsilon,0.05f)<=0f || BossHealth.NonlethalSelfCost(0.2f,0.05f)>=0.2f)throw new InvalidOperationException("自損公式或非致死保護錯誤");
            var original=new ProfileData();
            if(!ProfileStore.PreparePrologueCompletion(original,PrologueResult.Victory,"test_guardian",1,new[]{"test_equipment","test_equipment"},new[]{"test_character"},out var next,out bool first)||!first||original.prologueDone||original.skillPoints!=0||original.defeatedBosses.Count!=0||next.skillPoints!=1||next.unlockedEquipment.Count!=1||!next.prologueDone||next.prologueResult!=PrologueResult.Victory)throw new InvalidOperationException("快照或來源隔離錯誤");
            if(ProfileStore.PreparePrologueCompletion(next,PrologueResult.Victory,"test_guardian",1,null,null,out _,out _))throw new InvalidOperationException("結算重複提交");
            var existing=new ProfileData();existing.defeatedBosses.Add("test_guardian");
            if(!ProfileStore.PreparePrologueCompletion(existing,PrologueResult.Victory,"test_guardian",1,null,null,out var again,out bool repeat)||repeat||again.skillPoints!=0)throw new InvalidOperationException("重複首通發獎");
        }
        static bool Contains(SerializedProperty p,Object value){for(int i=0;i<p.arraySize;i++)if(p.GetArrayElementAtIndex(i).objectReferenceValue==value)return true;return false;}
        static void Require(Object o,params string[] names){if(o==null)throw new InvalidOperationException("缺元件");var s=new SerializedObject(o);foreach(var n in names)if(s.FindProperty(n)?.objectReferenceValue==null)throw new InvalidOperationException(o.name+" 缺 "+n);}
        static T Get<T>(string path,Action<T> init)where T:ScriptableObject{var a=AssetDatabase.LoadAssetAtPath<T>(path);if(a!=null)return a;a=ScriptableObject.CreateInstance<T>();init(a);AssetDatabase.CreateAsset(a,path);return a;}
        static TimedFx Fx(string name)=>AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Hazards/"+name+".prefab").GetComponent<TimedFx>();
        static void Set(Object o,string n,Object value){var s=new SerializedObject(o);s.FindProperty(n).objectReferenceValue=value;s.ApplyModifiedPropertiesWithoutUndo();}
        static void SetBool(Object o,string n,bool value){var s=new SerializedObject(o);s.FindProperty(n).boolValue=value;s.ApplyModifiedPropertiesWithoutUndo();}
        static void SetArray<T>(Object o,string n,T[] values)where T:Object{var s=new SerializedObject(o);var p=s.FindProperty(n);p.arraySize=values.Length;for(int i=0;i<values.Length;i++)p.GetArrayElementAtIndex(i).objectReferenceValue=values[i];s.ApplyModifiedPropertiesWithoutUndo();}
    }
}
