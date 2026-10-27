using System.Collections.Generic;
using BossFight.Core;
using BossFight.Hub;
using BossFight.Progression;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace BossFight.EditorTools
{
    /// <summary>
    /// Tools > Hub > Build Hub：照 HubLayoutData 整個重建休息區 Assets/Scenes/Hub.unity（存檔前會先問要不要存目前的場景）。
    /// 1. 資產（沒有才建）：ScriptableObjects/Hub/HubLayout.asset（版面）、HubAtmosphere.asset（天色）、HubUIData.asset（介面文字，HubUIBuilder 建）；
    ///    材質在 Models/Hub/Materials（只在不存在時建立）、網格在 Models/Hub/Meshes（每次覆寫）。
    /// 2. 場景（T20）：地面；沿邊界折線種 treeRows 排樹（第一排在線上，樹幹有碰撞）、第一排後面一圈隱形牆；中心空地與連到各站點的小路；
    ///    四個站點的佔位模型：中心小屋＋村長、鐵匠鋪（棚子、鐵砧、爐火光）、營火（石圈、柴堆、火焰粒子、閃爍的火光、坐的圓木）、傳送門（石拱門＋旋轉的紫色光面）；
    ///    每個站點一個 Interactable（傳送門是 Portal、營火是 Campfire、鐵匠鋪是 Blacksmith〔T22，接上 PlayerSpawner 與 GameOptions〕）、站位點與一台站點鏡頭（優先度 0，HubManager 打開介面時調高）；傍晚天色與薄霧（HubAtmosphere）。
    ///    營火（T21）的坐的圓木橫放在站位點正下方（狗坐在上面），Campfire 接上 PlayerSpawner、GameOptions 與換角色的煙（Prefabs/Hazards/Fx_HitSpark）。
    ///    村長（T24）是 VillageChief（頭上標記的位置 MarkerAnchor，接上 HubStory.asset〔沒有才建〕與 GameDatabase），HubManager 接村長（開場劇情）；傳送門接 GameDatabase（挑戰清單）。
    /// 3. 出生點 Start（營火旁，啟動遊戲時）與 Portal（傳送門前，從 Boss 戰回來時）、PlayerSpawner（預設生成在 Start）與鏡頭（CameraRigBuilder）、
    ///    GameOptions、AudioManager（沒有音樂）、Hub 物件（HubManager、InteractionScanner）、ESC 選單（PauseMenuBuilder 的休息區版：繼續、設定、退出遊戲）、
    ///    互動提示與站點面板（HubUIBuilder）、SceneFlow（PauseMenuBuilder.Wire 最後建）。休息區不建 HUD。
    /// 也把 Build Settings 設成 Hub 0、BossArena 1。佔位模型的尺寸、顏色是純視覺參數；玩法相關的位置與距離都在 HubLayoutData。
    /// </summary>
    public static class HubBuilder
    {
        public const string ScenePath = "Assets/Scenes/Hub.unity";
        const string BattleScenePath = "Assets/Scenes/BossArena.unity";
        const string OptionsDataPath = "Assets/ScriptableObjects/Options/GameOptionsData.asset";
        public const string DataDir = "Assets/ScriptableObjects/Hub";
        const string LayoutPath = DataDir + "/HubLayout.asset";
        const string AtmospherePath = DataDir + "/HubAtmosphere.asset";
        const string StoryPath = DataDir + "/HubStory.asset";
        const string MatDir = "Assets/Models/Hub/Materials";
        const string MeshDir = "Assets/Models/Hub/Meshes";
        const int StationCameraIdlePriority = 0; // 比自由視角（10）與沒在用的鎖定鏡頭（9）低
        const string SwapFxPath = "Assets/Prefabs/Hazards/Fx_HitSpark.prefab"; // 營火換角色時的煙（佔位）
        const float SeatLogBehind = 0.05f; // 坐的圓木在站位點後面多少 (m)：狗坐下時屁股的位置

        static Material groundMat, pathMat, trunkMat, woodMat, roofMat, wallMat, doorMat, stoneMat, metalMat, emberMat, portalMat, runeMat,
                        robeMat, hatMat, skinMat, fireMat;
        static Material[] leafMats;
        static Mesh coneMesh, roofMesh;

        [MenuItem("Tools/Hub/Build Hub")]
        public static void BuildFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Build();
        }

        public static void Build()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(DaoDunDogBuilder.PrefabPath) == null)
            {
                Debug.LogError($"[Hub] 找不到 {DaoDunDogBuilder.PrefabPath}（Tools > Player > Build DaoDun Dog）。");
                return;
            }

            // 先開新場景再載入資產：NewScene 會卸載沒被場景引用的資產，先載入的引用會變成 null
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            DaoDunDogBuilder.EnsureFolder(MatDir);
            DaoDunDogBuilder.EnsureFolder(MeshDir);
            CreateMaterials();
            var layout = GetOrCreate<HubLayoutData>(LayoutPath);
            var atmosphereData = GetOrCreate<HubAtmosphereData>(AtmospherePath);
            if (atmosphereData.skybox == null)
            {
                atmosphereData.skybox = GetSkybox();
                EditorUtility.SetDirty(atmosphereData);
            }
            coneMesh = BuildConeMesh();
            roofMesh = BuildRoofMesh();


            // 天色
            var sun = new GameObject("Directional Light").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.shadows = LightShadows.Soft;
            var atmosphere = sun.gameObject.AddComponent<HubAtmosphere>();
            var atmoSo = new SerializedObject(atmosphere);
            atmoSo.FindProperty("data").objectReferenceValue = atmosphereData;
            atmoSo.FindProperty("sun").objectReferenceValue = sun;
            atmoSo.ApplyModifiedPropertiesWithoutUndo();
            atmosphere.Apply();

            // 地面、小路、樹、隱形牆
            var level = new GameObject("Level").transform;
            BuildGround(level, layout);
            BuildPaths(level, layout);
            BuildTrees(level, layout);
            BuildWalls(level, layout);

            // 站點
            var stations = new GameObject("Stations").transform;
            BuildHut(stations, layout);
            var interactables = new List<Interactable>
            {
                BuildChief(stations, layout),
                BuildBlacksmith(stations, layout, atmosphereData),
                BuildCampfire(stations, layout, atmosphereData),
                BuildPortal(stations, layout),
            };
            interactables.RemoveAll(i => i == null);

            // 出生點
            var flowData = AssetDatabase.LoadAssetAtPath<SceneFlowData>(SceneFlowBuilder.DataPath);
            if (flowData == null) SceneFlowBuilder.Build(); // 建立 SceneFlowData（PauseMenuBuilder.Wire 之後會沿用這個 SceneFlow）
            flowData = AssetDatabase.LoadAssetAtPath<SceneFlowData>(SceneFlowBuilder.DataPath);
            var spawns = new GameObject("Spawns").transform;
            var start = new GameObject(flowData.startArrival).transform;
            start.SetParent(spawns, false);
            start.SetPositionAndRotation(HubLayoutData.Ground(layout.startSpawn), Quaternion.Euler(0f, layout.startFacing, 0f));
            var portalSpawn = new GameObject(flowData.portalArrival).transform;
            portalSpawn.SetParent(spawns, false);
            var portalStation = layout.Get(HubStationKind.Portal);
            if (portalStation != null)
                portalSpawn.SetPositionAndRotation(HubLayoutData.Ground(portalStation.position) + HubLayoutData.Direction(portalStation.facing) * layout.portalSpawnDistance,
                                                   Quaternion.Euler(0f, portalStation.facing, 0f));
            var wake = new GameObject(flowData.wakeArrival).transform;
            wake.SetParent(spawns, false);
            wake.SetPositionAndRotation(HubLayoutData.Ground(layout.wakeSpawn), Quaternion.Euler(0f, layout.wakeFacing, 0f));

            // 設定、音效、玩家、鏡頭
            var options = PauseMenuBuilder.EnsureOptions(AssetDatabase.LoadAssetAtPath<GameOptionsData>(OptionsDataPath), out var pause);
            IntroCutsceneBuilder.BuildAudioManager(null);
            PlayerSpawnerBuilder.Build(start.position, start.rotation, (flowData.startArrival, start), (flowData.portalArrival, portalSpawn), (flowData.wakeArrival, wake));
            var cameraController = CameraRigBuilder.Build(PlayerSpawnerBuilder.Preview().Controller); // 也會接打擊回饋（CombatFeedbackBuilder.Wire）
            WireCampfire(interactables, options);
            WireBlacksmith(interactables, options);
            var chief = WireChiefAndPortal(interactables);

            // 休息區狀態與互動
            var hubGo = new GameObject("Hub");
            var scanner = hubGo.AddComponent<InteractionScanner>();
            var scannerSo = new SerializedObject(scanner);
            IntroCutsceneBuilder.SetArray(scannerSo.FindProperty("interactables"), interactables);
            scannerSo.ApplyModifiedPropertiesWithoutUndo();
            var hub = hubGo.AddComponent<HubManager>();
            var hubSo = new SerializedObject(hub);
            hubSo.FindProperty("layout").objectReferenceValue = layout;
            hubSo.FindProperty("scanner").objectReferenceValue = scanner;
            hubSo.FindProperty("cameraController").objectReferenceValue = cameraController;
            hubSo.FindProperty("pause").objectReferenceValue = pause;
            hubSo.FindProperty("chief").objectReferenceValue = chief;
            hubSo.ApplyModifiedPropertiesWithoutUndo();

            PauseMenuBuilder.Build(); // 休息區版 ESC 選單；Wire 最後建 SceneFlow（接傳送門）並接 spawner
            HubUIBuilder.Build(hub, scanner);
            if (AssetDatabase.LoadAssetAtPath<BossFight.Cutscene.PrologueCutsceneData>(PrologueCutsceneBuilder.DataPath) != null) PrologueCutsceneBuilder.BuildHub();
            SceneFlowBuilder.Build(); // 把最後建立的 HubUI 也納入開機閘門
            PlayerSpawnerBuilder.Wire();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            SetBuildScenes();
            Debug.Log($"[Hub] 休息區已建立：{ScenePath}（{interactables.Count} 個站點）；Build Settings：Hub 0、BossArena 1");
        }

        /// <summary>Build Settings：Hub 0、BossArena 1（SampleScene 拿掉）。</summary>
        public static void SetBuildScenes()
        {
            var scenes = new List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(ScenePath, true),
                new EditorBuildSettingsScene(BattleScenePath, true),
            };
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(PrologueBuilder.ScenePath) != null)
                scenes.Add(new EditorBuildSettingsScene(PrologueBuilder.ScenePath, true));
            foreach (var existing in EditorBuildSettings.scenes)
            {
                if (existing.path == ScenePath || existing.path == BattleScenePath || existing.path == PrologueBuilder.ScenePath ||
                    existing.path == "Assets/Scenes/SampleScene.unity") continue;
                scenes.Add(existing);
            }
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        /// <summary>T25 原地升級目前的 Hub：保留美術與現有出生點，只補 Wake、選單與啟動閘門。</summary>
        [MenuItem("Tools/Prologue/Upgrade Hub Wiring")]
        public static void UpgradePrologueWiring()
        {
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != ScenePath)
                throw new System.InvalidOperationException("請先開啟 Hub 場景。");
            var spawner = PlayerSpawnerBuilder.Find();
            var layout = AssetDatabase.LoadAssetAtPath<HubLayoutData>(LayoutPath);
            var flowData = AssetDatabase.LoadAssetAtPath<SceneFlowData>(SceneFlowBuilder.DataPath);
            if (spawner == null || layout == null || flowData == null)
                throw new System.InvalidOperationException("Hub 缺少 PlayerSpawner、HubLayout 或 SceneFlowData。");
            var so = new SerializedObject(spawner);
            var points = so.FindProperty("spawnPoints");
            Transform parent = null, wake = null;
            for (int i = 0; i < points.arraySize; i++)
            {
                var p = points.GetArrayElementAtIndex(i);
                var point = p.FindPropertyRelative("point").objectReferenceValue as Transform;
                if (point != null) parent = point.parent;
                if (p.FindPropertyRelative("id").stringValue == flowData.wakeArrival) wake = point;
            }
            if (wake == null)
            {
                wake = new GameObject(flowData.wakeArrival).transform;
                wake.SetParent(parent, false);
                int index = points.arraySize++;
                var entry = points.GetArrayElementAtIndex(index);
                entry.FindPropertyRelative("id").stringValue = flowData.wakeArrival;
                entry.FindPropertyRelative("point").objectReferenceValue = wake;
            }
            wake.SetPositionAndRotation(HubLayoutData.Ground(layout.wakeSpawn), Quaternion.Euler(0f, layout.wakeFacing, 0f));
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(layout);
            EditorUtility.SetDirty(flowData);
            PauseMenuBuilder.Build();
            SceneFlowBuilder.Build();
            PlayerSpawnerBuilder.Wire();
            SetBuildScenes();
            EditorSceneManager.MarkSceneDirty(spawner.gameObject.scene);
        }

        // ---------- 地面、小路 ----------

        static void BuildGround(Transform level, HubLayoutData layout)
        {
            Vector2 min = Vector2.positiveInfinity, max = Vector2.negativeInfinity;
            foreach (var p in layout.boundary)
            {
                min = Vector2.Min(min, p);
                max = Vector2.Max(max, p);
            }
            min -= Vector2.one * layout.groundMargin;
            max += Vector2.one * layout.groundMargin;
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Ground";
            ground.transform.SetParent(level, false);
            Vector2 center = (min + max) * 0.5f, size = max - min;
            ground.transform.localPosition = new Vector3(center.x, -0.5f, center.y);
            ground.transform.localScale = new Vector3(size.x, 1f, size.y);
            ground.GetComponent<MeshRenderer>().sharedMaterial = groundMat;
            ground.isStatic = true;
        }

        // 中心空地與連到各站點站位點的小路（貼地、沒有碰撞）
        static void BuildPaths(Transform level, HubLayoutData layout)
        {
            var paths = new GameObject("Paths").transform;
            paths.SetParent(level, false);
            Vector3 center = HubLayoutData.Ground(layout.plazaCenter);
            var plaza = Deco(PrimitiveType.Cylinder, paths, "Plaza", center + Vector3.up * 0.01f,
                             new Vector3(layout.plazaRadius * 2f, 0.005f, layout.plazaRadius * 2f), pathMat);
            plaza.isStatic = true;
            foreach (var s in layout.stations)
            {
                Vector3 to = StandPosition(s);
                Vector3 d = to - center;
                float length = d.magnitude;
                if (length < 0.01f) continue;
                var strip = Deco(PrimitiveType.Cube, paths, $"Path_{s.kind}", (center + to) * 0.5f + Vector3.up * 0.008f,
                                 new Vector3(layout.pathWidth, 0.01f, length), pathMat, Quaternion.LookRotation(d));
                strip.isStatic = true;
            }
        }

        // ---------- 樹與隱形牆 ----------

        static void BuildTrees(Transform level, HubLayoutData layout)
        {
            var trees = new GameObject("Trees").transform;
            trees.SetParent(level, false);
            var boundary = layout.boundary;
            var rng = new System.Random(layout.treeSeed);
            var placed = new List<Vector2>();
            float minGap = layout.treeSpacing * 0.55f;
            int count = 0;
            for (int row = 0; row < layout.treeRows; row++)
            {
                var ring = row == 0 ? boundary : Offset(boundary, row * layout.rowSpacing);
                var normals = EdgeNormals(boundary);
                float t = row % 2 == 1 ? layout.treeSpacing * 0.5f : 0f; // 奇數排錯開半格
                for (int i = 0; i < ring.Length; i++)
                {
                    Vector2 a = ring[i], b = ring[(i + 1) % ring.Length];
                    Vector2 dir = (b - a).normalized;
                    float length = Vector2.Distance(a, b);
                    for (; t < length; t += layout.treeSpacing)
                    {
                        Vector2 p = a + dir * t;
                        // 第一排只往外、沿線偏移，不會長進可走範圍
                        float along = Range(rng, -layout.treeJitter, layout.treeJitter);
                        float outward = row == 0 ? Range(rng, 0f, layout.treeJitter) : Range(rng, -layout.treeJitter, layout.treeJitter);
                        p += dir * along + normals[i] * outward;
                        if (row > 0 && Inside(boundary, p)) continue;
                        if (TooClose(placed, p, minGap)) continue;
                        placed.Add(p);
                        Tree(trees, $"Tree_{count++:000}", p, Range(rng, layout.treeHeight.x, layout.treeHeight.y),
                             (float)rng.NextDouble() * 360f, leafMats[rng.Next(leafMats.Length)], layout.trunkRadius);
                    }
                    t -= length;
                }
            }
        }

        static void Tree(Transform parent, string name, Vector2 p, float height, float yaw, Material leaves, float trunkRadius)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = HubLayoutData.Ground(p);
            root.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            root.isStatic = true;
            var col = root.AddComponent<CapsuleCollider>();
            col.radius = trunkRadius;
            col.height = height;
            col.center = Vector3.up * height * 0.5f;
            float trunkHeight = height * 0.3f;
            Deco(PrimitiveType.Cylinder, root.transform, "Trunk", Vector3.up * trunkHeight * 0.5f,
                 new Vector3(trunkRadius * 1.4f, trunkHeight * 0.5f, trunkRadius * 1.4f), trunkMat).isStatic = true;
            float crownRadius = height * 0.24f;
            MeshDeco(coneMesh, root.transform, "Crown", Vector3.up * height * 0.2f, new Vector3(crownRadius * 2f, height * 0.8f, crownRadius * 2f), leaves).isStatic = true;
        }

        // 第一排樹後面一圈：沿往外 wallOffset 的折線，每段一面 BoxCollider（相鄰兩段在轉角多疊半個牆厚）
        static void BuildWalls(Transform level, HubLayoutData layout)
        {
            var walls = new GameObject("InvisibleWalls").transform;
            walls.SetParent(level, false);
            var ring = Offset(layout.boundary, layout.wallOffset + layout.wallThickness * 0.5f);
            for (int i = 0; i < ring.Length; i++)
            {
                Vector3 a = HubLayoutData.Ground(ring[i]), b = HubLayoutData.Ground(ring[(i + 1) % ring.Length]);
                Vector3 d = b - a;
                var go = new GameObject($"Wall_{i:00}");
                go.transform.SetParent(walls, false);
                go.transform.SetPositionAndRotation((a + b) * 0.5f + Vector3.up * layout.wallHeight * 0.5f, Quaternion.LookRotation(d));
                go.AddComponent<BoxCollider>().size = new Vector3(layout.wallThickness, layout.wallHeight, d.magnitude + layout.wallThickness);
                go.isStatic = true;
            }
        }

        // ---------- 站點 ----------

        static Transform StationRoot(Transform parent, string name, Vector2 position, float facing)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            root.SetPositionAndRotation(HubLayoutData.Ground(position), Quaternion.Euler(0f, facing, 0f)); // 本地 +Z = 正面（玩家那一邊）
            return root;
        }

        static void BuildHut(Transform parent, HubLayoutData layout)
        {
            var hut = StationRoot(parent, "Hut", layout.hutPosition, layout.hutFacing);
            var body = Solid(PrimitiveType.Cube, hut, "Body", new Vector3(0f, 1.5f, 0f), new Vector3(6f, 3f, 5f), wallMat);
            body.isStatic = true;
            MeshDeco(roofMesh, hut, "Roof", new Vector3(0f, 3f, 0f), new Vector3(6.8f, 2.2f, 5.8f), roofMat).isStatic = true;
            Deco(PrimitiveType.Cube, hut, "Door", new Vector3(0f, 1.05f, 2.51f), new Vector3(1.2f, 2.1f, 0.05f), doorMat).isStatic = true;
            Deco(PrimitiveType.Cube, hut, "Window_L", new Vector3(-1.9f, 1.8f, 2.51f), new Vector3(0.9f, 0.8f, 0.05f), emberMat).isStatic = true;
            Deco(PrimitiveType.Cube, hut, "Window_R", new Vector3(1.9f, 1.8f, 2.51f), new Vector3(0.9f, 0.8f, 0.05f), emberMat).isStatic = true;
        }

        static Interactable BuildChief(Transform parent, HubLayoutData layout)
        {
            var s = layout.Get(HubStationKind.Chief);
            if (s == null) return null;
            var root = StationRoot(parent, "Chief", s.position, s.facing);
            Solid(PrimitiveType.Capsule, root, "Body", new Vector3(0f, 0.85f, 0f), new Vector3(0.8f, 0.85f, 0.8f), robeMat);
            Deco(PrimitiveType.Sphere, root, "Head", new Vector3(0f, 1.85f, 0f), Vector3.one * 0.5f, skinMat);
            Deco(PrimitiveType.Sphere, root, "Nose", new Vector3(0f, 1.82f, 0.25f), Vector3.one * 0.12f, skinMat);
            Deco(PrimitiveType.Cylinder, root, "HatBrim", new Vector3(0f, 2.08f, 0f), new Vector3(0.85f, 0.02f, 0.85f), hatMat);
            Deco(PrimitiveType.Cylinder, root, "HatCrown", new Vector3(0f, 2.3f, 0f), new Vector3(0.42f, 0.2f, 0.42f), hatMat);
            var marker = new GameObject("MarkerAnchor").transform; // 頭上「!」「?」的位置
            marker.SetParent(root, false);
            marker.localPosition = new Vector3(0f, 2.95f, 0f);
            var chief = AddStation<VillageChief>(root.gameObject, layout, s);
            DaoDunDogBuilder.SetReference(chief, "markerAnchor", marker);
            return chief;
        }

        static Interactable BuildBlacksmith(Transform parent, HubLayoutData layout, HubAtmosphereData atmosphere)
        {
            var s = layout.Get(HubStationKind.Blacksmith);
            if (s == null) return null;
            var root = StationRoot(parent, "Blacksmith", s.position, s.facing);
            // 鐵砧在棚子前緣；棚子往後（本地 -Z）延伸
            var anvil = Solid(PrimitiveType.Cube, root, "Anvil", new Vector3(0f, 0.3f, 0f), new Vector3(0.45f, 0.6f, 0.35f), metalMat);
            anvil.isStatic = true;
            Deco(PrimitiveType.Cube, root, "AnvilTop", new Vector3(0f, 0.7f, 0f), new Vector3(0.95f, 0.22f, 0.42f), metalMat).isStatic = true;
            Deco(PrimitiveType.Cube, root, "AnvilHorn", new Vector3(0.6f, 0.72f, 0f), new Vector3(0.35f, 0.14f, 0.22f), metalMat).isStatic = true;
            foreach (var x in new[] { -2.3f, 2.3f })
            {
                foreach (var z in new[] { 0.9f, -3.5f })
                    Solid(PrimitiveType.Cylinder, root, $"Post_{x}_{z}", new Vector3(x, 1.45f, z), new Vector3(0.22f, 1.45f, 0.22f), woodMat).isStatic = true;
            }
            Deco(PrimitiveType.Cube, root, "ShedRoof", new Vector3(0f, 3.05f, -1.3f), new Vector3(5.3f, 0.15f, 5.2f), roofMat,
                 Quaternion.Euler(-8f, 0f, 0f)).isStatic = true;
            Solid(PrimitiveType.Cube, root, "Forge", new Vector3(-1.4f, 0.55f, -2.6f), new Vector3(1.4f, 1.1f, 1.2f), stoneMat).isStatic = true;
            Deco(PrimitiveType.Cube, root, "ForgeEmbers", new Vector3(-1.4f, 1.12f, -2.6f), new Vector3(1.1f, 0.06f, 0.9f), emberMat).isStatic = true;
            var forgeLight = new GameObject("ForgeLight").AddComponent<Light>();
            forgeLight.transform.SetParent(root, false);
            forgeLight.transform.localPosition = new Vector3(-1.4f, 1.6f, -2.6f);
            forgeLight.type = LightType.Point;
            forgeLight.color = atmosphere.forgeColor;
            forgeLight.intensity = atmosphere.forgeIntensity;
            forgeLight.range = atmosphere.forgeRange;
            forgeLight.shadows = LightShadows.None;
            return AddStation<Blacksmith>(root.gameObject, layout, s);
        }

        static Interactable BuildCampfire(Transform parent, HubLayoutData layout, HubAtmosphereData atmosphere)
        {
            var s = layout.Get(HubStationKind.Campfire);
            if (s == null) return null;
            var root = StationRoot(parent, "Campfire", s.position, s.facing);
            var blocker = root.gameObject.AddComponent<CapsuleCollider>(); // 走不進火堆
            blocker.radius = 0.85f;
            blocker.height = 2f;
            blocker.center = Vector3.up;
            const int stones = 9;
            for (int i = 0; i < stones; i++)
            {
                float a = i * Mathf.PI * 2f / stones;
                Deco(PrimitiveType.Sphere, root, $"Stone_{i}", new Vector3(Mathf.Sin(a) * 0.75f, 0.08f, Mathf.Cos(a) * 0.75f),
                     new Vector3(0.32f, 0.22f, 0.28f), stoneMat).isStatic = true;
            }
            for (int i = 0; i < 4; i++)
            {
                float yaw = i * 90f + 20f;
                Deco(PrimitiveType.Cylinder, root, $"Firewood_{i}", Quaternion.Euler(0f, yaw, 0f) * new Vector3(0f, 0.3f, 0.22f),
                     new Vector3(0.12f, 0.35f, 0.12f), woodMat, Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(-35f, 0f, 0f)).isStatic = true;
            }
            // 坐的圓木：玩家這一邊（橫放在站位點正下方，T21 坐下用；沒有碰撞，走得上去）、對面、左邊
            Deco(PrimitiveType.Cylinder, root, "SeatLog_Front", new Vector3(0f, 0.22f, s.standDistance + SeatLogBehind), new Vector3(0.44f, 0.8f, 0.44f), woodMat,
                 Quaternion.Euler(0f, 0f, 90f)).isStatic = true;
            Deco(PrimitiveType.Cylinder, root, "SeatLog_Back", new Vector3(0f, 0.22f, -2f), new Vector3(0.44f, 0.8f, 0.44f), woodMat,
                 Quaternion.Euler(0f, 0f, 90f)).isStatic = true;
            Deco(PrimitiveType.Cylinder, root, "SeatLog_Side", new Vector3(-2f, 0.22f, 0.2f), new Vector3(0.44f, 0.8f, 0.44f), woodMat,
                 Quaternion.Euler(90f, 0f, 0f)).isStatic = true;
            BuildFlames(root);
            var lightGo = new GameObject("FireLight");
            lightGo.transform.SetParent(root, false);
            lightGo.transform.localPosition = Vector3.up * 1.1f;
            var fireLight = lightGo.AddComponent<Light>();
            fireLight.type = LightType.Point;
            fireLight.color = atmosphere.fireColor;
            fireLight.intensity = atmosphere.fireIntensity;
            fireLight.range = atmosphere.fireRange;
            fireLight.shadows = LightShadows.None; // 點光源陰影要 6 張 shadow map，會擠爆陰影圖集
            DaoDunDogBuilder.SetReference(lightGo.AddComponent<FireFlicker>(), "data", atmosphere);
            return AddStation<Campfire>(root.gameObject, layout, s);
        }

        // 村長（T24）的劇情與任務清單、傳送門的挑戰清單；回傳村長（HubManager 的開場劇情）
        static VillageChief WireChiefAndPortal(List<Interactable> interactables)
        {
            var database = ProgressionBuilder.LoadDatabase();
            if (database == null) database = ProgressionBuilder.EnsureAssets();
            var chief = interactables.Find(i => i is VillageChief) as VillageChief;
            if (chief != null)
            {
                var so = new SerializedObject(chief);
                so.FindProperty("story").objectReferenceValue = GetOrCreate<HubStoryData>(StoryPath);
                so.FindProperty("database").objectReferenceValue = database;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            var portal = interactables.Find(i => i is Portal) as Portal;
            if (portal != null) DaoDunDogBuilder.SetReference(portal, "database", database);
            return chief;
        }

        // 鐵匠鋪（T22）換裝要的 spawner、Debug「解鎖全部裝備」看的開發者模式（GameOptions）
        static void WireBlacksmith(List<Interactable> interactables, GameOptions options)
        {
            var blacksmith = interactables.Find(i => i is Blacksmith) as Blacksmith;
            if (blacksmith == null) return;
            var so = new SerializedObject(blacksmith);
            so.FindProperty("spawner").objectReferenceValue = Object.FindAnyObjectByType<PlayerSpawner>();
            so.FindProperty("options").objectReferenceValue = options;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // 營火換角色要的 spawner、開發者模式（GameOptions）與煙
        static void WireCampfire(List<Interactable> interactables, GameOptions options)
        {
            var campfire = interactables.Find(i => i is Campfire) as Campfire;
            if (campfire == null) return;
            var so = new SerializedObject(campfire);
            so.FindProperty("spawner").objectReferenceValue = Object.FindAnyObjectByType<PlayerSpawner>();
            so.FindProperty("options").objectReferenceValue = options;
            var fx = AssetDatabase.LoadAssetAtPath<BossFight.Hazards.TimedFx>(SwapFxPath);
            if (fx == null) Debug.LogWarning($"[Hub] 找不到 {SwapFxPath}（Tools > Hazards > Build Hazards），營火換角色時不冒煙。");
            so.FindProperty("swapFx").objectReferenceValue = fx;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void BuildFlames(Transform parent)
        {
            var go = new GameObject("Flames");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.up * 0.15f;
            go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f); // 粒子往上噴
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.3f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.55f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.55f, 0.15f), new Color(1f, 0.85f, 0.35f));
            main.maxParticles = 80;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = ps.emission;
            emission.rateOverTime = 40f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 12f;
            shape.radius = 0.25f;
            var color = ps.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.8f, 0.4f), 0f), new GradientColorKey(new Color(0.9f, 0.25f, 0.05f), 1f) },
                             new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0f, 1f) });
            color.color = gradient;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = fireMat;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        static Interactable BuildPortal(Transform parent, HubLayoutData layout)
        {
            var s = layout.Get(HubStationKind.Portal);
            if (s == null) return null;
            var root = StationRoot(parent, "Portal", s.position, s.facing);
            foreach (var x in new[] { -2f, 2f })
                Solid(PrimitiveType.Cube, root, x < 0f ? "Pillar_L" : "Pillar_R", new Vector3(x, 2.1f, 0f), new Vector3(0.8f, 4.2f, 0.9f), stoneMat).isStatic = true;
            Solid(PrimitiveType.Cube, root, "Lintel", new Vector3(0f, 4.55f, 0f), new Vector3(5.2f, 0.7f, 1.1f), stoneMat).isStatic = true;
            var gate = root.gameObject.AddComponent<BoxCollider>(); // 光面也擋路
            gate.center = new Vector3(0f, 2.1f, 0f);
            gate.size = new Vector3(3.2f, 4.2f, 0.3f);
            // 旋轉的紫色光面：圓盤＋三條亮紋一起轉（Cylinder 轉 90° 後，本地 Y 是門的法線）
            var surface = Deco(PrimitiveType.Cylinder, root, "Surface", new Vector3(0f, 2.15f, 0f), new Vector3(3.1f, 0.02f, 3.1f), portalMat,
                               Quaternion.Euler(90f, 0f, 0f));
            for (int i = 0; i < 3; i++)
            {
                Deco(PrimitiveType.Cube, surface.transform, $"Rune_{i}", new Vector3(0f, 1.2f, 0f), new Vector3(0.9f, 1f, 0.06f), runeMat,
                     Quaternion.Euler(0f, i * 60f, 0f));
            }
            var spin = new SerializedObject(surface.AddComponent<Spin>());
            spin.FindProperty("degreesPerSecond").vector3Value = new Vector3(0f, 45f, 0f);
            spin.ApplyModifiedPropertiesWithoutUndo();
            return AddStation<Portal>(root.gameObject, layout, s);
        }

        // 站點元件、站位點、站點鏡頭
        static T AddStation<T>(GameObject root, HubLayoutData layout, HubLayoutData.Station s) where T : Interactable
        {
            Vector3 stationPos = HubLayoutData.Ground(s.position);
            Vector3 stand = StandPosition(s);
            Vector3 forward = stationPos - stand;
            forward.y = 0f;
            if (forward.sqrMagnitude < 1e-4f) forward = -HubLayoutData.Direction(s.facing);
            var frame = Quaternion.LookRotation(forward.normalized);

            var standPoint = new GameObject("StandPoint").transform;
            standPoint.SetParent(root.transform, false);
            standPoint.SetPositionAndRotation(stand, frame);

            var camGo = new GameObject("StationCamera");
            camGo.transform.SetParent(root.transform, false);
            Vector3 camPos = stand + frame * s.cameraOffset, look = stand + frame * s.cameraLookOffset;
            camGo.transform.SetPositionAndRotation(camPos, Quaternion.LookRotation(look - camPos));
            var cam = camGo.AddComponent<CinemachineCamera>();
            cam.Priority = StationCameraIdlePriority;
            var lens = LensSettings.Default;
            lens.FieldOfView = s.cameraFov;
            lens.NearClipPlane = 0.1f;
            lens.FarClipPlane = 500f;
            cam.Lens = lens;

            var station = root.AddComponent<T>();
            var so = new SerializedObject(station);
            so.FindProperty("layout").objectReferenceValue = layout;
            so.FindProperty("kind").enumValueIndex = (int)s.kind;
            so.FindProperty("standPoint").objectReferenceValue = standPoint;
            so.FindProperty("stationCamera").objectReferenceValue = cam;
            so.ApplyModifiedPropertiesWithoutUndo();
            return station;
        }

        static Vector3 StandPosition(HubLayoutData.Station s) => HubLayoutData.Ground(s.position) + HubLayoutData.Direction(s.facing) * s.standDistance;

        // ---------- 折線 ----------

        // 每段往外的法線（依折線方向判斷外側）
        static Vector2[] EdgeNormals(Vector2[] poly)
        {
            bool ccw = SignedArea(poly) > 0f;
            var normals = new Vector2[poly.Length];
            for (int i = 0; i < poly.Length; i++)
            {
                Vector2 d = (poly[(i + 1) % poly.Length] - poly[i]).normalized;
                normals[i] = ccw ? new Vector2(d.y, -d.x) : new Vector2(-d.y, d.x);
            }
            return normals;
        }

        // 整條折線往外推 distance（頂點沿兩段法線的角平分線，尖角的延伸量有上限）
        static Vector2[] Offset(Vector2[] poly, float distance)
        {
            var normals = EdgeNormals(poly);
            int n = poly.Length;
            var result = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                Vector2 n0 = normals[(i - 1 + n) % n], n1 = normals[i];
                Vector2 m = n0 + n1;
                m = m.sqrMagnitude < 1e-6f ? n1 : m.normalized;
                float cos = Mathf.Max(Vector2.Dot(m, n1), 0.35f);
                result[i] = poly[i] + m * (distance / cos);
            }
            return result;
        }

        static float SignedArea(Vector2[] poly)
        {
            float area = 0f;
            for (int i = 0; i < poly.Length; i++)
            {
                Vector2 a = poly[i], b = poly[(i + 1) % poly.Length];
                area += a.x * b.y - b.x * a.y;
            }
            return area * 0.5f;
        }

        static bool Inside(Vector2[] poly, Vector2 p)
        {
            bool inside = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            {
                Vector2 a = poly[i], b = poly[j];
                if ((a.y > p.y) != (b.y > p.y) && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x) inside = !inside;
            }
            return inside;
        }

        static bool TooClose(List<Vector2> placed, Vector2 p, float gap)
        {
            float sqr = gap * gap;
            foreach (var q in placed)
            {
                if ((q - p).sqrMagnitude < sqr) return true;
            }
            return false;
        }

        static float Range(System.Random rng, float min, float max) => min + (float)rng.NextDouble() * (max - min);

        // ---------- 物件 ----------

        // 沒有碰撞的佔位形體
        static GameObject Deco(PrimitiveType type, Transform parent, string name, Vector3 localPos, Vector3 scale, Material mat, Quaternion? localRot = null)
            => DaoDunDogBuilder.Prim(type, parent, name, localPos, scale, mat, localRot);

        // 保留碰撞的佔位形體（會擋路）
        static GameObject Solid(PrimitiveType type, Transform parent, string name, Vector3 localPos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        static GameObject MeshDeco(Mesh mesh, Transform parent, string name, Vector3 localPos, Vector3 scale, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        // ---------- 網格（每次覆寫） ----------

        // 圓錐：底面圓心在原點、直徑 1、高 1（樹冠）
        static Mesh BuildConeMesh()
        {
            const int sides = 12;
            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var tris = new List<int>();
            const float slope = 0.5f; // 半徑 / 高
            for (int i = 0; i < sides; i++)
            {
                float a0 = i * Mathf.PI * 2f / sides, a1 = (i + 1) * Mathf.PI * 2f / sides, am = (a0 + a1) * 0.5f;
                Vector3 p0 = new Vector3(Mathf.Sin(a0), 0f, Mathf.Cos(a0)) * 0.5f, p1 = new Vector3(Mathf.Sin(a1), 0f, Mathf.Cos(a1)) * 0.5f;
                Vector3 n0 = new Vector3(Mathf.Sin(a0), slope, Mathf.Cos(a0)).normalized, n1 = new Vector3(Mathf.Sin(a1), slope, Mathf.Cos(a1)).normalized;
                Vector3 nm = new Vector3(Mathf.Sin(am), slope, Mathf.Cos(am)).normalized;
                int b = verts.Count;
                verts.AddRange(new[] { p0, p1, Vector3.up });
                normals.AddRange(new[] { n0, n1, nm });
                tris.AddRange(new[] { b, b + 1, b + 2 });
                b = verts.Count; // 底面
                verts.AddRange(new[] { Vector3.zero, p1, p0 });
                normals.AddRange(new[] { Vector3.down, Vector3.down, Vector3.down });
                tris.AddRange(new[] { b, b + 1, b + 2 });
            }
            return SaveMesh("Hub_Cone", verts, normals, tris);
        }

        // 三角屋頂：寬 1（X）、高 1、深 1（Z），屋脊沿 X，底面在 y = 0
        static Mesh BuildRoofMesh()
        {
            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var tris = new List<int>();
            Vector3 fl = new Vector3(-0.5f, 0f, 0.5f), fr = new Vector3(0.5f, 0f, 0.5f), bl = new Vector3(-0.5f, 0f, -0.5f), br = new Vector3(0.5f, 0f, -0.5f);
            Vector3 rl = new Vector3(-0.5f, 1f, 0f), rr = new Vector3(0.5f, 1f, 0f);
            // 頂點照「從外面看逆時針」給，這裡反過來排成 Unity 的正面（從外面看順時針）
            void Quad(Vector3 a, Vector3 d, Vector3 c, Vector3 b)
            {
                Vector3 n = Vector3.Cross(b - a, c - a).normalized;
                int i = verts.Count;
                verts.AddRange(new[] { a, b, c, d });
                normals.AddRange(new[] { n, n, n, n });
                tris.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
            }
            void Tri(Vector3 a, Vector3 c, Vector3 b)
            {
                Vector3 n = Vector3.Cross(b - a, c - a).normalized;
                int i = verts.Count;
                verts.AddRange(new[] { a, b, c });
                normals.AddRange(new[] { n, n, n });
                tris.AddRange(new[] { i, i + 1, i + 2 });
            }
            Quad(fl, rl, rr, fr); // 前坡
            Quad(br, rr, rl, bl); // 後坡
            Tri(bl, rl, fl);      // 左山牆
            Tri(fr, rr, br);      // 右山牆
            Quad(bl, fl, fr, br); // 底
            return SaveMesh("Hub_Roof", verts, normals, tris);
        }

        static Mesh SaveMesh(string name, List<Vector3> verts, List<Vector3> normals, List<int> tris)
        {
            string path = $"{MeshDir}/{name}.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null)
            {
                mesh = new Mesh { name = name };
                AssetDatabase.CreateAsset(mesh, path);
            }
            mesh.Clear();
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        // ---------- 材質（只在不存在時建立） ----------

        static void CreateMaterials()
        {
            groundMat = GetMat("Hub_Ground", new Color(0.32f, 0.42f, 0.24f), 0.1f);
            pathMat = GetMat("Hub_Path", new Color(0.46f, 0.36f, 0.24f), 0.05f);
            trunkMat = GetMat("Hub_Trunk", new Color(0.3f, 0.2f, 0.12f), 0.1f);
            leafMats = new[]
            {
                GetMat("Hub_Leaves0", new Color(0.13f, 0.3f, 0.15f), 0.1f),
                GetMat("Hub_Leaves1", new Color(0.18f, 0.36f, 0.16f), 0.1f),
                GetMat("Hub_Leaves2", new Color(0.1f, 0.25f, 0.17f), 0.1f),
            };
            woodMat = GetMat("Hub_Wood", new Color(0.5f, 0.33f, 0.18f), 0.15f);
            roofMat = GetMat("Hub_Roof", new Color(0.48f, 0.2f, 0.13f), 0.2f);
            wallMat = GetMat("Hub_HutWall", new Color(0.72f, 0.6f, 0.44f), 0.1f);
            doorMat = GetMat("Hub_Door", new Color(0.22f, 0.13f, 0.08f), 0.2f);
            stoneMat = GetMat("Hub_Stone", new Color(0.48f, 0.47f, 0.5f), 0.2f);
            metalMat = GetMat("Hub_Metal", new Color(0.2f, 0.2f, 0.23f), 0.6f, 0.8f);
            emberMat = GetMat("Hub_Ember", new Color(1f, 0.55f, 0.2f), 0.5f, 0f, new Color(1f, 0.45f, 0.12f) * 2f);
            portalMat = GetMat("Hub_Portal", new Color(0.55f, 0.25f, 0.95f), 0.6f, 0f, new Color(0.45f, 0.15f, 0.9f) * 1.5f);
            runeMat = GetMat("Hub_PortalRune", new Color(0.9f, 0.6f, 1f), 0.6f, 0f, new Color(0.85f, 0.45f, 1f) * 3f);
            robeMat = GetMat("Hub_ChiefRobe", new Color(0.33f, 0.42f, 0.62f), 0.1f);
            hatMat = GetMat("Hub_ChiefHat", new Color(0.3f, 0.18f, 0.1f), 0.2f);
            skinMat = GetMat("Hub_ChiefSkin", new Color(0.92f, 0.78f, 0.62f), 0.2f);
            fireMat = GetFireMat();
        }

        static Material GetMat(string name, Color color, float smoothness, float metallic = 0f, Color? emission = null)
        {
            string path = $"{MatDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            mat = new Material(shader) { name = name };
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
            if (emission.HasValue && mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", emission.Value);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        // 火焰粒子：URP Particles/Unlit、加法混合、內建的圓點貼圖
        static Material GetFireMat()
        {
            string path = $"{MatDir}/Hub_Fire.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;
            mat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")) { name = "Hub_Fire" };
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 2f); // Additive
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)BlendMode.One);
            mat.SetFloat("_ZWrite", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)RenderQueue.Transparent;
            var tex = AssetDatabase.GetBuiltinExtraResource<Texture2D>("Default-Particle.psd");
            if (tex != null) mat.SetTexture("_BaseMap", tex);
            mat.SetColor("_BaseColor", Color.white);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        static Material GetSkybox()
        {
            string path = $"{MatDir}/Hub_Sky.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;
            mat = new Material(Shader.Find("Skybox/Procedural")) { name = "Hub_Sky" };
            mat.SetFloat("_SunSize", 0.05f);
            mat.SetFloat("_AtmosphereThickness", 1.6f);
            mat.SetColor("_SkyTint", new Color(0.62f, 0.5f, 0.55f));
            mat.SetColor("_GroundColor", new Color(0.28f, 0.22f, 0.2f));
            mat.SetFloat("_Exposure", 1.1f);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        static T GetOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            DaoDunDogBuilder.EnsureFolder(path.Substring(0, path.LastIndexOf('/')));
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }
    }
}
