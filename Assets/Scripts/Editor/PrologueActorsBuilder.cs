using System;
using System.Linq;
using BossFight.Battle;
using BossFight.Boss;
using BossFight.Core;
using BossFight.Hazards;
using BossFight.DebugTools;
using BossFight.Progression;
using BossFight.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace BossFight.EditorTools
{
    /// <summary>只建立 T26 角色與接線，不重建 T25 的地形、鏡頭、出生或選單。</summary>
    public static class PrologueActorsBuilder
    {
        public const string DaisyDataPath = "Assets/ScriptableObjects/Prologue/DaisyMentor.asset";
        public const string SaplingDataPath = "Assets/ScriptableObjects/Prologue/TrainingSapling.asset";
        public const string AngryDataPath = "Assets/ScriptableObjects/Prologue/AngrySapling.asset";
        public const string DaisyPrefabPath = "Assets/Prefabs/Prologue/DaisyMentor.prefab";
        public const string SaplingPrefabPath = "Assets/Prefabs/Prologue/TrainingSapling.prefab";
        const string LeafPrefabPath = "Assets/Prefabs/Prologue/Fx_SaplingLeaves.prefab";
        const string ModelDir = "Assets/Models/Prologue/Actors";

        [MenuItem("Tools/Prologue/Build Teaching Actors (T26)")]
        public static void BuildFromMenu()
        {
            Build();
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
        }

        /// <summary>MCP 可在非 Play 呼叫的引用驗證；不讀寫 ProfileStore 或 PlayerPrefs。</summary>
        public static string Validate()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != PrologueBuilder.ScenePath) throw new InvalidOperationException("驗證目標必須是 Prologue。");
            var all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
            foreach (var t in all)
                if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) > 0)
                    throw new InvalidOperationException($"Missing Script: {t.name}");
            var mentor = Object.FindAnyObjectByType<DaisyMentor>(FindObjectsInactive.Include);
            var sapling = Object.FindAnyObjectByType<TrainingSapling>(FindObjectsInactive.Include);
            var binding = Object.FindAnyObjectByType<PrologueActorBinding>(FindObjectsInactive.Include);
            var hud = Object.FindAnyObjectByType<TrainingSaplingHudUI>(FindObjectsInactive.Include);
            Require(mentor, "data", "whipHitbox", "hurtCollider");
            Require(sapling, "data", "attackHitbox", "hurtCollider");
            Require(mentor.GetComponent<DaisyMentorVisual>(), "mentor", "model", "flower", "petalHand", "vine");
            Require(sapling.GetComponent<TrainingSaplingVisual>(), "sapling", "model", "branch", "stars", "leavesPrefab", "telegraphPrefab");
            Require(sapling.GetComponent<BossHitFeedback>(), "sapling", "sparkPrefab");
            Require(binding, "mentor", "sapling", "session");
            Require(hud, "sapling", "viewCamera", "anchor", "panel", "health", "stagger", "title");
            Require(Object.FindAnyObjectByType<ExecutionMarkerUI>(FindObjectsInactive.Include), "data", "target", "aimPoint", "viewCamera", "marker");
            var spawner = Object.FindAnyObjectByType<PlayerSpawner>();
            var receivers = new SerializedObject(spawner).FindProperty("receivers");
            foreach (var expected in new MonoBehaviour[] { binding, Object.FindAnyObjectByType<PlayerHudUI>(FindObjectsInactive.Include) })
            {
                bool found = false;
                for (int i = 0; i < receivers.arraySize; i++) found |= receivers.GetArrayElementAtIndex(i).objectReferenceValue == expected;
                if (!found || expected == null) throw new InvalidOperationException("PlayerSpawner 缺少 T26 接收者。");
            }
            var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions");
            foreach (string name in new[] { "DebugSaplingMode", "DebugMentorWhip", "DebugMentorSlap", "DebugSaplingInvulnerability" })
                if (actions.FindAction("Player/" + name, false) == null) throw new InvalidOperationException("缺少開發動作 " + name);
            var hit = new DamageInfo(40f, false, false, 0f, ignoreInvulnerability: true, ignoreDefense: true, pushDistance: 1f, pushDuration: 0.25f)
                .WithSource(Vector3.one, null).WithBlockStaminaMultiplier(1f);
            if (!hit.ignoreInvulnerability || !hit.ignoreDefense || hit.pushDistance != 1f || hit.pushDuration != 0.25f)
                throw new InvalidOperationException("DamageInfo 複製遺失劇情攻擊資料。");
            if (new DamageInfo(1f, true, true, 0f).ignoreInvulnerability || new DamageInfo(1f, true, true, 0f).pushDistance != 0f)
                throw new InvalidOperationException("一般攻擊的預設行為被改動。");
            var training = AssetDatabase.LoadAssetAtPath<TrainingSaplingData>(SaplingDataPath);
            var white = new SaplingTelegraph(0, training, SaplingMode.Attack);
            var red = new SaplingTelegraph(0, training, SaplingMode.DodgeAttack);
            if (!white.canBlock || !white.canParry || red.canBlock || red.canParry || (int)SaplingMode.Target != 2)
                throw new InvalidOperationException("白色格擋／紅色閃避模式或舊資產編號不符。");
            return $"T26 PASS: {scene.path}; Missing Script 0; actor/UI/spawner refs OK; Debug Actions 4; DamageInfo copy/defaults OK; nodes {all.Length}";
        }

        static void Require(Object target, params string[] fields)
        {
            if (target == null) throw new InvalidOperationException("缺少 T26 元件。");
            var so = new SerializedObject(target);
            foreach (string field in fields)
                if (so.FindProperty(field)?.objectReferenceValue == null) throw new InvalidOperationException($"{target.name}: 缺少 {field}");
        }

        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("請先停止 Play。");
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var anchors = GameObject.Find("ActorAnchors");
            if (anchors == null || (scene.path != "" && scene.path != PrologueBuilder.ScenePath))
                throw new InvalidOperationException("請在已有 ActorAnchors 的 Prologue 場景執行。");
            foreach (string path in new[] { ModelDir, "Assets/Prefabs/Prologue", "Assets/ScriptableObjects/Prologue" })
                DaoDunDogBuilder.EnsureFolder(path);
            var daisyData = Data<DaisyMentorData>(DaisyDataPath);
            var saplingData = Data<TrainingSaplingData>(SaplingDataPath);
            var angry = AssetDatabase.LoadAssetAtPath<TrainingSaplingData>(AngryDataPath);
            if (angry == null)
            {
                angry = ScriptableObject.CreateInstance<TrainingSaplingData>();
                angry.displayName = "憤怒的小樹精";
                angry.damage = 25f; angry.attackPeriod = 1.5f; angry.lifetime = 15f;
                angry.initialMode = SaplingMode.Attack;
                angry.initiallyInvulnerable = false; angry.initiallyAccumulateStagger = true;
                AssetDatabase.CreateAsset(angry, AngryDataPath);
            }
            var leaves = BuildLeaves();
            var daisyPrefab = BuildDaisy(daisyData);
            var saplingPrefab = BuildSapling(saplingData, leaves);

            foreach (string name in new[] { "PrologueActors", "PrologueActorsUI", "PrologueActorDebug" })
            {
                var previous = GameObject.Find(name);
                if (previous != null) Undo.DestroyObjectImmediate(previous);
            }
            var root = new GameObject("PrologueActors");
            var mentor = ((GameObject)PrefabUtility.InstantiatePrefab(daisyPrefab, root.transform)).GetComponent<DaisyMentor>();
            var sapling = ((GameObject)PrefabUtility.InstantiatePrefab(saplingPrefab, root.transform)).GetComponent<TrainingSapling>();
            mentor.transform.SetPositionAndRotation(anchors.transform.Find("Daisy").position, Quaternion.identity);
            sapling.transform.SetPositionAndRotation(anchors.transform.Find("Sapling").position, Quaternion.Euler(0f, 180f, 0f));
            var binding = root.AddComponent<PrologueActorBinding>();
            Set(binding, "mentor", mentor); Set(binding, "sapling", sapling);
            Set(binding, "saplingFeedback", sapling.GetComponent<BossHitFeedback>());
            Set(binding, "session", Object.FindAnyObjectByType<PrologueSession>());
            BuildUI(sapling);
            BuildDebug(binding, mentor, sapling);
            PlayerSpawnerBuilder.Wire();
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[T26] 菊花、小樹精、SO、HUD 與開發按鍵已接線；F8 模式／F9 抽打／F10 巴掌／F11 無敵。");
            // T27 已存在時一併重接，避免重建角色後教學仍指向已銷毀的實例。
            if (scene.path == PrologueBuilder.ScenePath && GameObject.Find("Tutorial") != null) TutorialBuilder.Build();
        }

        static GameObject BuildDaisy(DaisyMentorData data)
        {
            var root = new GameObject("DaisyMentor");
            var actor = root.AddComponent<DaisyMentor>();
            var visual = root.AddComponent<DaisyMentorVisual>();
            var model = Node("Model", root.transform, Vector3.zero);
            model.localScale = Vector3.one * 2f;
            var green = Mat("DaisyStem", new Color(0.2f, 0.55f, 0.15f));
            var white = Mat("DaisyPetal", new Color(1f, 0.98f, 0.9f));
            var yellow = Mat("DaisyFace", new Color(1f, 0.8f, 0.12f));
            Prim("Stem", model, PrimitiveType.Cylinder, new Vector3(0f, 0.2f, 0f), new Vector3(0.04f, 0.2f, 0.04f), green);
            var flower = Node("Flower", model, new Vector3(0f, 0.46f, 0f));
            for (int i = 0; i < 10; i++)
            {
                float a = i * Mathf.PI * 2f / 10f;
                var p = Prim($"Petal_{i}", flower, PrimitiveType.Sphere, new Vector3(Mathf.Sin(a) * 0.13f, Mathf.Cos(a) * 0.13f, 0f), new Vector3(0.08f, 0.15f, 0.04f), white);
                p.transform.localRotation = Quaternion.Euler(0f, 0f, -a * Mathf.Rad2Deg);
            }
            Prim("Face", flower, PrimitiveType.Sphere, Vector3.zero, new Vector3(0.22f, 0.22f, 0.08f), yellow);
            var faceMat = SmileMaterial();
            var smile = Prim("Smile", flower, PrimitiveType.Quad, new Vector3(0f, 0f, 0.043f), new Vector3(0.19f, 0.19f, 1f), faceMat);
            smile.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            var hand = Node("PetalHand", flower, new Vector3(0.18f, -0.08f, 0f));
            Prim("Hand", hand, PrimitiveType.Sphere, Vector3.zero, new Vector3(0.13f, 0.07f, 0.035f), white);
            var hitbox = Node("WhipHitbox", root.transform, Vector3.zero).gameObject.AddComponent<Hitbox>();
            SetHitbox(hitbox, root.transform);
            var hurt = Hurt(root.transform, actor, new Vector3(0f, 0.3f, 0f), new Vector3(0.45f, 0.6f, 0.25f));
            var vine = Node("Vine", root.transform, Vector3.zero).gameObject.AddComponent<LineRenderer>();
            vine.sharedMaterial = green; vine.widthMultiplier = 0.055f; vine.useWorldSpace = true; vine.enabled = false;
            Set(actor, "data", data); Set(actor, "whipHitbox", hitbox); Set(actor, "hurtCollider", hurt);
            Set(visual, "mentor", actor); Set(visual, "model", model); Set(visual, "flower", flower); Set(visual, "petalHand", hand); Set(visual, "vine", vine);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, DaisyPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        static GameObject BuildSapling(TrainingSaplingData data, ParticleSystem leaves)
        {
            var root = new GameObject("TrainingSapling");
            var actor = root.AddComponent<TrainingSapling>();
            var visual = root.AddComponent<TrainingSaplingVisual>();
            var model = Node("Model", root.transform, Vector3.zero);
            model.localScale = Vector3.one * 2f;
            var bark = Mat("SaplingBark", new Color(0.44f, 0.27f, 0.12f));
            var leaf = Mat("SaplingLeaf", new Color(0.3f, 0.72f, 0.17f));
            var gold = Mat("SaplingGold", new Color(1f, 0.8f, 0.1f));
            Prim("Body", model, PrimitiveType.Capsule, new Vector3(0f, 0.43f, 0f), new Vector3(0.34f, 0.3f, 0.3f), bark);
            foreach (float side in new[] { -1f, 1f })
            {
                Prim("Foot", model, PrimitiveType.Sphere, new Vector3(side * 0.13f, 0.09f, 0.04f), new Vector3(0.14f, 0.18f, 0.15f), bark);
                Prim("Leaf", model, PrimitiveType.Sphere, new Vector3(side * 0.12f, 0.79f, 0f), new Vector3(0.25f, 0.19f, 0.1f), leaf);
                Prim("Eye", model, PrimitiveType.Sphere, new Vector3(side * 0.07f, 0.52f, 0.14f), new Vector3(0.04f, 0.06f, 0.025f), Mat("SaplingEye", new Color(0.04f, 0.035f, 0.02f)));
            }
            var branch = Node("Branch", model, new Vector3(0.12f, 0.46f, 0f));
            var arm = Prim("Arm", branch, PrimitiveType.Capsule, new Vector3(0.18f, 0f, 0f), new Vector3(0.09f, 0.25f, 0.09f), bark);
            arm.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            var stars = Node("Stars", model, new Vector3(0f, 1.05f, 0f));
            var starMesh = StarMesh();
            for (int i = 0; i < 5; i++)
            {
                float a = i * Mathf.PI * 2f / 5f;
                var star = new GameObject($"Star_{i}", typeof(MeshFilter), typeof(MeshRenderer));
                star.transform.SetParent(stars, false);
                star.transform.localPosition = new Vector3(Mathf.Sin(a) * 0.27f, 0f, Mathf.Cos(a) * 0.27f);
                star.transform.localScale = Vector3.one * 0.12f;
                star.GetComponent<MeshFilter>().sharedMesh = starMesh;
                star.GetComponent<MeshRenderer>().sharedMaterial = gold;
            }
            stars.gameObject.SetActive(false);
            var aim = Node("AimPoint", root.transform, new Vector3(0f, 0.9f, 0f));
            Node("HudAnchor", root.transform, new Vector3(0f, 2.5f, 0f));
            Set(root.GetComponent<LockOnTarget>(), "aimPoint", aim);
            var hitbox = Node("BranchHitbox", root.transform, Vector3.zero).gameObject.AddComponent<Hitbox>();
            SetHitbox(hitbox, root.transform);
            var hurt = Hurt(root.transform, actor, new Vector3(0f, 0.45f, 0f), new Vector3(0.45f, 0.9f, 0.45f));
            Set(actor, "data", data); Set(actor, "attackHitbox", hitbox); Set(actor, "hurtCollider", hurt);
            Set(visual, "sapling", actor); Set(visual, "model", model); Set(visual, "branch", branch); Set(visual, "stars", stars); Set(visual, "leavesPrefab", leaves);
            Set(visual, "telegraphPrefab", AssetDatabase.LoadAssetAtPath<GroundTelegraph>("Assets/Prefabs/Hazards/GroundTelegraph.prefab"));
            SetArray(visual, "renderers", model.GetComponentsInChildren<Renderer>(true));
            WireSaplingFeedback(actor);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, SaplingPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        /// <summary>直接重用 OIIA 的火花 prefab、HDR 顏色與大小；可單獨補接既有樹精 prefab。</summary>
        public static void WireSaplingFeedback(TrainingSapling actor)
        {
            var oiia = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/OiiaCat.prefab");
            var source = oiia != null ? oiia.GetComponent<BossHitFeedback>() : null;
            if (source == null) throw new InvalidOperationException("缺少 OIIA 的 BossHitFeedback，無法接用既有特效。");
            var feedback = DaoDunDogBuilder.GetOrAdd<BossHitFeedback>(actor.gameObject);
            EditorUtility.CopySerialized(source, feedback);
            Set(feedback, "boss", null);
            Set(feedback, "sapling", actor);
        }

        static ParticleSystem BuildLeaves()
        {
            var root = new GameObject("Fx_SaplingLeaves");
            var particles = root.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.duration = 1.2f; main.loop = false; main.startLifetime = 1.1f; main.startSpeed = 1.6f;
            main.startSize = 0.12f; main.gravityModifier = 0.35f; main.startColor = new Color(0.4f, 0.7f, 0.15f);
            main.stopAction = ParticleSystemStopAction.Destroy;
            var emission = particles.emission; emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)35) });
            var shape = particles.shape; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = 0.2f;
            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = Mat("FallingLeaf", new Color(0.4f, 0.7f, 0.15f));
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, LeafPrefabPath).GetComponent<ParticleSystem>();
            Object.DestroyImmediate(root);
            return prefab;
        }

        static void BuildUI(TrainingSapling sapling)
        {
            var data = AssetDatabase.LoadAssetAtPath<BattleUIData>("Assets/ScriptableObjects/UI/BattleUIData.asset");
            var root = new GameObject("PrologueActorsUI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            root.GetComponent<Canvas>().sortingOrder = 5;
            var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f); scaler.matchWidthOrHeight = 0.5f;
            var area = root.transform as RectTransform;
            BattleUIBuilder.BuildStandalonePlayerHud(area, data);
            // PauseMenuBuilder.Wire 會連帶重建 SceneFlow 黑幕；這裡只補玩家 HUD 的按鍵提示引用。
            var playerHud = root.GetComponentInChildren<PlayerHudUI>(true);
            Set(playerHud, "inputNames", AssetDatabase.LoadAssetAtPath<InputNameData>("Assets/ScriptableObjects/UI/InputNameData.asset"));
            Set(playerHud, "hintDevice", Object.FindAnyObjectByType<InputHintDevice>());
            var panel = Rect("SaplingHud", area, new Vector2(240f, 54f));
            var title = Label("Name", panel, new Vector2(0f, 15f), new Vector2(240f, 24f));
            var health = Bar("Health", panel, data, new Vector2(0f, -5f), data.playerHealthColor);
            var stagger = Bar("Stagger", panel, data, new Vector2(0f, -21f), data.staggerColor);
            var hud = root.AddComponent<TrainingSaplingHudUI>();
            Set(hud, "sapling", sapling); Set(hud, "viewCamera", UnityEngine.Camera.main);
            Set(hud, "anchor", sapling.transform.Find("HudAnchor")); Set(hud, "panel", panel);
            Set(hud, "health", health); Set(hud, "stagger", stagger); Set(hud, "title", title);
            var marker = Rect("ExecutionMarker", area, new Vector2(52f, 52f));
            var ring = marker.gameObject.AddComponent<Image>(); ring.color = data.executionMarkerColor; ring.raycastTarget = false;
            ring.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Models/UI/Ui_Ring.png");
            var dot = Rect("Dot", marker, new Vector2(8f, 8f)).gameObject.AddComponent<Image>();
            dot.color = data.executionMarkerColor; dot.raycastTarget = false;
            var executionUI = root.AddComponent<ExecutionMarkerUI>();
            Set(executionUI, "data", data); Set(executionUI, "target", sapling.GetComponent<ExecutionTarget>());
            Set(executionUI, "aimPoint", sapling.transform.Find("AimPoint")); Set(executionUI, "viewCamera", UnityEngine.Camera.main);
            Set(executionUI, "marker", marker); SetArray(executionUI, "tinted", new Image[] { ring, dot });
        }

        static void BuildDebug(PrologueActorBinding binding, DaisyMentor mentor, TrainingSapling sapling)
        {
            var root = new GameObject("PrologueActorDebug");
            var debug = root.AddComponent<PrologueActorDebugTools>();
            debug.enabled = false;
            Set(debug, "binding", binding); Set(debug, "mentor", mentor); Set(debug, "sapling", sapling);
            var gate = root.AddComponent<DeveloperMode>();
            Set(gate, "options", Object.FindAnyObjectByType<GameOptions>());
            Set(gate, "pause", Object.FindAnyObjectByType<GamePause>());
            SetArray(gate, "tools", new Behaviour[] { debug });
        }

        static T Data<T>(string path) where T : ScriptableObject
        {
            var data = AssetDatabase.LoadAssetAtPath<T>(path);
            if (data != null) return data;
            data = ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(data, path); return data;
        }
        static Material Mat(string name, Color color)
        {
            var path = $"{ModelDir}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            material.SetColor("_BaseColor", color); material.SetFloat("_Smoothness", 0f);
            material.SetFloat("_Cull", 0f); material.EnableKeyword("_EMISSION");
            AssetDatabase.CreateAsset(material, path); return material;
        }
        static Material SmileMaterial()
        {
            var material = Mat("DaisySmile", Color.white);
            var path = ModelDir + "/DaisySmile.asset";
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null)
            {
                const int size = 128;
                texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "DaisySmile" };
                var pixels = new Color[size * size];
                for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size, v = (y + 0.5f) / size;
                    bool eye = (new Vector2(u - 0.3f, v - 0.64f).magnitude < 0.035f || new Vector2(u - 0.7f, v - 0.64f).magnitude < 0.035f);
                    float curve = 0.24f + (u - 0.5f) * (u - 0.5f) * 1.4f;
                    bool smile = u > 0.2f && u < 0.8f && Mathf.Abs(v - curve) < 0.018f;
                    pixels[y * size + x] = eye || smile ? Color.black : new Color(1f, 0.8f, 0.12f);
                }
                texture.SetPixels(pixels); texture.Apply(); AssetDatabase.CreateAsset(texture, path);
            }
            if (material.GetTexture("_BaseMap") == null) { material.SetTexture("_BaseMap", texture); EditorUtility.SetDirty(material); }
            return material;
        }
        static Mesh StarMesh()
        {
            string path = ModelDir + "/SaplingStar.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh != null) return mesh;
            var vertices = new Vector3[11]; var triangles = new int[30];
            for (int i = 0; i < 10; i++)
            {
                float a = i * Mathf.PI / 5f; float r = i % 2 == 0 ? 1f : 0.4f;
                vertices[i + 1] = new Vector3(Mathf.Sin(a) * r, Mathf.Cos(a) * r, 0f);
                triangles[i * 3] = 0; triangles[i * 3 + 1] = i + 1; triangles[i * 3 + 2] = (i + 1) % 10 + 1;
            }
            mesh = new Mesh { name = "SaplingStar", vertices = vertices, triangles = triangles };
            mesh.RecalculateNormals(); AssetDatabase.CreateAsset(mesh, path); return mesh;
        }
        static Transform Node(string name, Transform parent, Vector3 position)
        {
            var node = new GameObject(name).transform; node.SetParent(parent, false); node.localPosition = position; return node;
        }
        static GameObject Prim(string name, Transform parent, PrimitiveType type, Vector3 position, Vector3 scale, Material material) =>
            DaoDunDogBuilder.Prim(type, parent, name, position, scale, material);
        static BoxCollider Hurt(Transform parent, MonoBehaviour owner, Vector3 center, Vector3 size)
        {
            var go = Node("Hurtbox", parent, Vector3.zero).gameObject; go.layer = LayerMask.NameToLayer("Hurtbox");
            var collider = go.AddComponent<BoxCollider>(); collider.isTrigger = true; collider.center = center; collider.size = size;
            Set(go.AddComponent<Hurtbox>(), "owner", owner); return collider;
        }
        static void SetHitbox(Hitbox hitbox, Transform root)
        {
            Set(hitbox, "attacker", root);
            var so = new SerializedObject(hitbox); so.FindProperty("targetMask").intValue = LayerMask.GetMask("Hurtbox"); so.ApplyModifiedPropertiesWithoutUndo();
        }
        static void Set(Object target, string field, Object value) => DaoDunDogBuilder.SetReference(target, field, value);
        static void SetArray(Object target, string field, Object[] values)
        {
            var so = new SerializedObject(target); var array = so.FindProperty(field); array.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        static RectTransform Rect(string name, Transform parent, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).transform as RectTransform;
            rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f); rect.sizeDelta = size; return rect;
        }
        static Text Label(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var rect = Rect(name, parent, size); rect.anchoredPosition = position;
            var text = rect.gameObject.AddComponent<Text>(); text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 20; text.alignment = TextAnchor.MiddleCenter; text.raycastTarget = false; return text;
        }
        static ResourceBar Bar(string name, Transform parent, BattleUIData data, Vector2 position, Color color)
        {
            var rect = Rect(name, parent, new Vector2(230f, 12f)); rect.anchoredPosition = position;
            var back = rect.gameObject.AddComponent<Image>(); back.color = new Color(0f, 0f, 0f, 0.75f); back.raycastTarget = false;
            var fill = Rect("Fill", rect, Vector2.zero); fill.anchorMin = Vector2.zero; fill.anchorMax = Vector2.one;
            var image = fill.gameObject.AddComponent<Image>(); image.color = color; image.raycastTarget = false;
            var bar = rect.gameObject.AddComponent<ResourceBar>(); Set(bar, "data", data); Set(bar, "fill", fill); return bar;
        }
    }
}
