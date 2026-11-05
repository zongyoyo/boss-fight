using System.Collections.Generic;
using System.Linq;
using BossFight.Core;
using BossFight.Hub;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace BossFight.EditorTools
{
    /// <summary>
    /// 休息區的佈景（docs/Art/ART_STYLE.md「休息區（Phase 6）」）：照 HubLayoutData 的「佈景」與固定種子，在 Level 底下建 Scenery。
    /// HubBuilder.Build 最後會呼叫 Build；只改佈景時跑 Tools > Hub > Build Scenery（在開著的 Hub 場景原地重建 Level/Scenery 並存檔，不動其他物件）。
    /// 玩法不動，只換外觀：HubBuilder 的佔位物件（地面方塊、小路、樹、小屋、鐵匠鋪、營火、傳送門的柱子與門楣）留著碰撞、只關掉 Renderer；
    /// 村長（角色建模擱置）與傳送門中間的旋轉光面照舊。
    /// 碰撞：玩家走得到的佈景補上簡單的方塊或膠囊（Default 層）——第一排樹的樹幹（貼合模型、不比 trunkRadius 細）、村子小道具、
    /// 鐵匠鋪的背牆、水槽、風箱與鐵砧、營火對面與側邊的座椅圓木；刀盾狗坐的那根圓木、地基、平台、鋪石這類低矮的走得上去；牆外的佈景沒有碰撞。
    /// - 地面：Builder 產生的低面數網格（M_Palette 色格），可走範圍與牆外 GroundFlatMargin 以內是 y = 0 的平地，之後起伏、往外升高；
    ///   中心空地、連到各站位點的小路與站點前的空地直接畫成泥土色格（邊緣用雜訊打散），取代原本的色條。碰撞仍是 HubBuilder 的地面方塊。
    /// - 樹：佔位樹的位置與樹幹碰撞照舊、換成正式模型（離牆內緣 EdgeTreeBand 以內只用樹冠高的闊葉樹與枯樹，鏡頭才不會被針葉樹低枝片包住），
    ///   往外再種 outerTreeRows 排沒有碰撞的裝飾樹。
    /// - 站點：小屋、鐵匠鋪＋鐵砧、營火（火焰片掛 FlameFlicker、另加火星粒子）、座椅圓木（放在佔位圓木的位置與方向）、傳送門。
    /// - 小道具（告示板、木桶、木箱、柴堆、柵欄）照 HubLayoutData.props 擺；牆外的灌木、石頭、枯枝、樹樁、倒木隨機散佈。
    /// - 草叢用 Core/InstancedScatter（GPU Instancing，一區一個元件）：可走範圍是稀疏的矮草，避開小路、空地、站點、出生點；牆外林帶較密。
    /// 缺正式模型時不建佈景、佔位外觀維持顯示。
    /// </summary>
    public static class HubSceneryBuilder
    {
        const string SceneryName = "Scenery";
        const string GroundMeshPath = "Assets/Models/Hub/Meshes/Hub_Ground.asset"; // 每次重建覆寫（保留 GUID）
        public const string SkyMaterialPath = "Assets/Art/Materials/Sky_HubDusk.mat";
        const string SkyShader = "BossFight/Sky Gradient";
        const string FireMaterialPath = HubBuilder.MatDir + "/Hub_Fire.mat";
        // 以下是純編輯器的散佈參數（視覺）
        const float ChunkSize = 12f;
        const float GroundWarp = 8f;        // 平地以外格距放大的快慢（m）
        const float GroundFlatMargin = 2f;  // 隱形牆外多少 m 以內仍然平坦
        const float ReliefScale = 0.07f;    // 地面起伏的雜訊頻率（1/m）
        const float ColorScale = 0.15f;     // 地面色塊的雜訊頻率（1/m）
        const float TrunkClearance = 0.5f;  // 草離樹幹至少這麼遠（m）
        const float EdgeTreeBand = 3f;      // 離牆內緣這麼遠以內的樹只用樹冠高的種類（m）
        const float YardRadius = 1.6f;      // 站位點周圍的泥土空地半徑（m）
        const float CampfireDirt = 2.5f;    // 營火周圍的泥土半徑（m）
        static readonly Vector2 TrunkSample = new Vector2(0.4f, 1.2f); // 量樹幹粗細的高度範圍（m，模型空間）
        const float TrunkSearch = 0.6f;     // 量樹幹時只看離中心軸這麼近的頂點（m，排除針葉樹的低枝片）
        // 站點模型裡碰得到、但 HubBuilder 沒有碰撞的部分（站點本地座標，跟著 Art/Blender/assets/hub 的模型尺寸；改模型時一起改）
        static readonly (string station, Vector3 center, Vector3 size)[] StationBlockers =
        {
            ("Blacksmith", new Vector3(0f, 1.42f, -3.68f), new Vector3(4.9f, 2.85f, 0.2f)),   // 背牆
            ("Blacksmith", new Vector3(1.3f, 0.25f, -2.8f), new Vector3(1.1f, 0.5f, 0.6f)),   // 水槽
            ("Blacksmith", new Vector3(-0.3f, 0.5f, -2.45f), new Vector3(0.55f, 1f, 0.9f)),   // 風箱
        };
        const float AnvilRadius = 0.33f;    // 鐵砧樹墩底座的半徑（m）
        const float AnvilHeight = 0.9f;
        const string FrontSeat = "SeatLog_Front"; // HubBuilder 營火玩家那一邊的圓木（站位點正下方）

        static readonly string[] RequiredModels = { "SM_Hut", "SM_Blacksmith", "SM_Anvil", "SM_Campfire", "SM_SeatLog", "SM_Portal", "SM_Tree_Pine_A" };

        static readonly (string id, float weight)[] RowTreeKinds =
        {
            ("SM_Tree_Pine_A", 0.28f), ("SM_Tree_Pine_B", 0.24f), ("SM_Tree_Pine_C", 0.2f), ("SM_Tree_Pine_D", 0.08f),
            ("SM_Tree_Broad_A", 0.08f), ("SM_Tree_Broad_C", 0.08f), ("SM_Tree_Dead_B", 0.04f),
        };
        // 隱形牆把鏡頭擋在牆內緣，針葉樹的低枝片會把鏡頭包住，所以靠近可走範圍的只用樹冠高的闊葉樹與枯樹（同序章）
        static readonly (string id, float weight)[] EdgeTreeKinds =
            { ("SM_Tree_Broad_A", 0.4f), ("SM_Tree_Broad_C", 0.42f), ("SM_Tree_Broad_B", 0.1f), ("SM_Tree_Dead_A", 0.08f) };
        static readonly (string id, float weight)[] OuterTreeKinds =
        {
            ("SM_Tree_Pine_A", 0.3f), ("SM_Tree_Pine_B", 0.25f), ("SM_Tree_Pine_C", 0.22f), ("SM_Tree_Pine_D", 0.1f),
            ("SM_Tree_Broad_B", 0.08f), ("SM_Tree_Dead_A", 0.05f),
        };
        static readonly (string id, float weight)[] VillageGrass =
            { ("SM_Grass_Clump_B", 0.65f), ("SM_Grass_Clump_A", 0.25f), ("SM_Grass_Clump_D", 0.1f) };
        static readonly (string id, float weight)[] ForestGrass =
            { ("SM_Grass_Clump_A", 0.35f), ("SM_Grass_Clump_C", 0.25f), ("SM_Grass_Clump_B", 0.25f), ("SM_Grass_Clump_D", 0.15f) };

        struct PropKind
        {
            public string[] ids;
            public float density;     // 個 / m²（乘上 propDensity）
            public Vector2 scale;
            public float sink;        // 往地裡埋的比例
            public float clearance;   // 離樹幹與其他道具至少多遠（m）
        }

        static readonly PropKind[] ForestProps =
        {
            new PropKind { ids = new[] { "SM_Log_A", "SM_Log_B" }, density = 0.003f, scale = new Vector2(0.9f, 1.2f), clearance = 1.4f },
            new PropKind { ids = new[] { "SM_Rock_E", "SM_Rock_F" }, density = 0.002f, scale = new Vector2(0.8f, 1.2f), sink = 0.12f, clearance = 1.6f },
            new PropKind { ids = new[] { "SM_Stump_A", "SM_Stump_B" }, density = 0.004f, scale = new Vector2(0.8f, 1.2f), clearance = 1f },
            new PropKind { ids = new[] { "SM_Bush_A", "SM_Bush_B", "SM_Bush_C" }, density = 0.03f, scale = new Vector2(0.8f, 1.3f), clearance = 0.6f },
            new PropKind { ids = new[] { "SM_Rock_A", "SM_Rock_B" }, density = 0.008f, scale = new Vector2(0.7f, 1.3f), sink = 0.12f, clearance = 0.8f },
            new PropKind { ids = new[] { "SM_Rock_C", "SM_Rock_D" }, density = 0.012f, scale = new Vector2(0.8f, 1.5f), sink = 0.1f, clearance = 0.3f },
            new PropKind { ids = new[] { "SM_Twig_A", "SM_Twig_B", "SM_Twig_C" }, density = 0.01f, scale = new Vector2(0.8f, 1.3f), clearance = 0.3f },
        };

        // 站點（HubBuilder 的物件名稱）→ 放在站點原點的模型
        static readonly (string station, string[] models)[] StationModels =
        {
            ("Hut", new[] { "SM_Hut" }),
            ("Blacksmith", new[] { "SM_Blacksmith", "SM_Anvil" }),
            ("Campfire", new[] { "SM_Campfire" }),
            ("Portal", new[] { "SM_Portal" }),
        };

        [MenuItem("Tools/Hub/Build Scenery")]
        public static void BuildFromMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var scene = SceneManager.GetActiveScene();
            if (scene.path != HubBuilder.ScenePath)
            {
                Debug.LogWarning("[Hub] 請先開啟 Hub 場景。");
                return;
            }
            // HubBuilder 建在根層，場景裡可能另外被收進分組物件（例如 HubContent），所以整個場景找
            var all = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).ToList();
            var level = all.FirstOrDefault(t => t.name == "Level" && t.Find("Trees") != null);
            var stations = all.FirstOrDefault(t => t.name == "Stations" && t.Find("Hut") != null);
            if (level == null || stations == null)
            {
                Debug.LogWarning("[Hub] 找不到 Level 或 Stations，先跑 Tools > Hub > Build Hub。");
                return;
            }
            var layout = AssetDatabase.LoadAssetAtPath<HubLayoutData>(HubBuilder.LayoutPath);
            var atmosphere = AssetDatabase.LoadAssetAtPath<HubAtmosphereData>(HubBuilder.AtmospherePath);
            Debug.Log(Build(level, stations, layout, atmosphere));
            ApplyAtmosphere(atmosphere);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        [MenuItem("Tools/Hub/Apply Atmosphere")]
        public static void ApplyAtmosphereFromMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var scene = SceneManager.GetActiveScene();
            if (scene.path != HubBuilder.ScenePath)
            {
                Debug.LogWarning("[Hub] 請先開啟 Hub 場景。");
                return;
            }
            ApplyAtmosphere(AssetDatabase.LoadAssetAtPath<HubAtmosphereData>(HubBuilder.AtmospherePath));
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[Hub] 已套用天色（漸層天空、主光、環境光、霧、基礎後處理）。");
        }

        /// <summary>
        /// 黃昏天色（ART_STYLE「光照與後處理」）：三色漸層天空 Sky_HubDusk（地平線色 = 霧色，顏色以 HubAtmosphereData 為準、每次寫回材質）
        /// 接到 HubAtmosphereData.skybox，加上三個場景共用的基礎後處理 BaseVolume（VP_LowPolyBase），再用 HubAtmosphere 套用主光、環境光與霧。
        /// </summary>
        public static void ApplyAtmosphere(HubAtmosphereData data)
        {
            if (data == null) return;
            var sky = AssetDatabase.LoadAssetAtPath<Material>(SkyMaterialPath);
            if (sky == null)
            {
                sky = new Material(Shader.Find(SkyShader)) { name = "Sky_HubDusk" };
                AssetDatabase.CreateAsset(sky, SkyMaterialPath);
            }
            sky.SetColor("_TopColor", data.skyTopColor);
            sky.SetColor("_HorizonColor", data.fogColor);
            sky.SetColor("_BottomColor", data.skyBottomColor);
            sky.SetColor("_SunGlowColor", data.sunColor);
            EditorUtility.SetDirty(sky);
            if (data.skybox != sky)
            {
                data.skybox = sky;
                EditorUtility.SetDirty(data);
            }
            var volume = SceneManager.GetActiveScene().GetRootGameObjects()
                .SelectMany(go => go.GetComponentsInChildren<Volume>(true)).FirstOrDefault(v => v.name == "BaseVolume");
            if (volume == null) volume = new GameObject("BaseVolume").AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 0f;
            volume.sharedProfile = ArtTools.EnsureBaseVolumeProfile();
            var atmosphere = Object.FindAnyObjectByType<HubAtmosphere>();
            if (atmosphere != null) atmosphere.Apply();
            DynamicGI.UpdateEnvironment();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        /// <summary>在 level 底下重建 Scenery（舊的先刪掉），關掉佔位外觀；回傳摘要。</summary>
        public static string Build(Transform level, Transform stations, HubLayoutData layout, HubAtmosphereData atmosphere)
        {
            var old = level.Find(SceneryName);
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var library = ArtLibrary.Load();
            trunkRadii.Clear(); // 模型可能重新匯入過
            var missing = RequiredModels.Where(id => library == null || library.Get(id) == null).ToArray();
            if (missing.Length > 0)
            {
                SetLegacyVisible(level, stations, true);
                return $"[Hub] 缺正式模型（{string.Join("、", missing)}），佈景不建，維持佔位外觀。";
            }

            var root = new GameObject(SceneryName).transform;
            root.SetParent(level, false);
            var terrain = new Terrain(layout);
            var random = new System.Random(layout.visualSeed);
            var paths = PathSegments(layout);

            BuildGround(root, layout, terrain, paths);
            var trunks = new List<Vector3>();
            int trees = BuildForest(root, level, layout, terrain, library, random, trunks);
            BuildStations(root, stations, atmosphere);
            var keepOut = KeepOut(layout);
            int village = BuildVillageProps(root, layout, terrain, library, keepOut);
            int clumps = BuildGrass(root, layout, terrain, library, random, trunks, keepOut, paths);
            int props = BuildForestProps(root, layout, terrain, library, random, trunks);
            SetLegacyVisible(level, stations, false);
            AssetDatabase.SaveAssets();
            return $"[Hub] 佈景：{trees} 棵樹、{village} 個村子小道具、{clumps} 叢草、{props} 個林地道具。";
        }

        // ---------------------------------------------------------------- 佔位外觀

        /// <summary>HubBuilder 的佔位外觀（地面方塊、小路、樹、小屋、鐵匠鋪、營火、傳送門的石柱與門楣）：只開關 Renderer，碰撞不動。</summary>
        static void SetLegacyVisible(Transform level, Transform stations, bool visible)
        {
            foreach (var name in new[] { "Ground", "Paths", "Trees" })
                SetRenderers(level.Find(name), visible, null);
            foreach (var name in new[] { "Hut", "Blacksmith", "Campfire" })
                SetRenderers(stations.Find(name), visible, null);
            var portal = stations.Find("Portal");
            SetRenderers(portal, visible, portal != null ? portal.Find("Surface") : null); // 旋轉光面照舊
        }

        static void SetRenderers(Transform root, bool visible, Transform keep)
        {
            if (root == null) return;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (keep != null && r.transform.IsChildOf(keep)) continue;
                r.enabled = visible;
            }
        }

        // ---------------------------------------------------------------- 地形

        /// <summary>
        /// 地面的高度：可走範圍與隱形牆外 GroundFlatMargin 以內是 0，之後起伏、往外升高。
        /// Outside 是離邊界折線的距離（外側為正、裡面為負）。
        /// </summary>
        class Terrain
        {
            readonly Vector2[] boundary;
            readonly float flat, relief, rise, riseSpan;
            readonly Vector2 offset;

            public Terrain(HubLayoutData layout)
            {
                boundary = layout.boundary;
                flat = layout.wallOffset + layout.wallThickness + GroundFlatMargin;
                relief = layout.groundRelief;
                rise = layout.groundRise;
                Center = boundary.Aggregate(Vector2.zero, (a, b) => a + b) / boundary.Length;
                FlatRadius = boundary.Max(p => (p - Center).magnitude) + flat + 1f;
                riseSpan = Mathf.Max(layout.groundRadius - FlatRadius - 10f, 1f);
                var r = new System.Random(layout.visualSeed + 1);
                offset = new Vector2((float)r.NextDouble() * 100f, (float)r.NextDouble() * 100f);
            }

            public Vector2 Center { get; }
            /// <summary>以 Center 為圓心、涵蓋所有平地的半徑（地面網格在這個半徑以內格距固定）。</summary>
            public float FlatRadius { get; }
            public float FlatBand => flat;

            public float Outside(Vector2 p)
            {
                float d = float.MaxValue;
                for (int i = 0; i < boundary.Length; i++)
                    d = Mathf.Min(d, SegmentDistance(p, boundary[i], boundary[(i + 1) % boundary.Length]));
                return HubBuilder.Inside(boundary, p) ? -d : d;
            }

            public float Height(Vector2 p)
            {
                float s = Outside(p);
                if (s <= flat) return 0f;
                float bumps = Mathf.SmoothStep(0f, 1f, (s - flat) / 6f) *
                              (Mathf.PerlinNoise(p.x * ReliefScale + offset.x, p.y * ReliefScale + offset.y) - 0.35f) * relief;
                float t = Mathf.Clamp01((s - flat - 10f) / riseSpan);
                return Mathf.Max(bumps, -0.2f) + rise * t * t;
            }

            public Vector3 At(Vector2 p) => new Vector3(p.x, Height(p), p.y);

            public float Noise(Vector2 p, float scale, float seed) =>
                Mathf.PerlinNoise(p.x * scale + offset.x + seed, p.y * scale + offset.y + seed);
        }

        static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            float t = ab.sqrMagnitude < 1e-6f ? 0f : Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return (a + ab * t - p).magnitude;
        }

        static Vector2 Flat(Vector3 v) => new Vector2(v.x, v.z);

        /// <summary>小路：中心空地連到各站位點（跟 HubBuilder 的色條一樣）。</summary>
        static List<(Vector2 a, Vector2 b)> PathSegments(HubLayoutData layout) =>
            layout.stations.Where(s => s != null)
                .Select(s => (layout.plazaCenter, Flat(HubBuilder.StandPosition(s))))
                .Where(seg => (seg.Item2 - seg.Item1).sqrMagnitude > 0.01f).ToList();

        static void BuildGround(Transform root, HubLayoutData layout, Terrain terrain, List<(Vector2 a, Vector2 b)> paths)
        {
            // 三角格子（列與列錯開半格），平地以內格距固定，之外依 GroundWarp 往外拉大：f(d) = r0 + (d - r0)(1 + (d - r0) / L)
            float s = layout.groundCellSize, r0 = terrain.FlatRadius, L = GroundWarp;
            float outer = Mathf.Max(layout.groundRadius, r0 + s);
            float maxD = r0 + (-1f + Mathf.Sqrt(1f + 4f * (outer - r0) / L)) * L * 0.5f;
            float rowH = s * Mathf.Sqrt(3f) * 0.5f;
            int nx = Mathf.CeilToInt(maxD / s) + 1, nz = Mathf.CeilToInt(maxD / rowH) + 1;
            var random = new System.Random(layout.visualSeed + 2);
            var points = new Dictionary<(int, int), Vector3>();
            for (int j = -nz; j <= nz; j++)
            for (int i = -nx; i <= nx; i++)
            {
                var p = new Vector2((i + ((j & 1) != 0 ? 0.5f : 0f)) * s, j * rowH);
                p += new Vector2((float)random.NextDouble() - 0.5f, (float)random.NextDouble() - 0.5f) * s * 0.4f;
                float d = p.magnitude;
                if (d > maxD) continue;
                if (d > r0) p *= (r0 + (d - r0) * (1f + (d - r0) / L)) / d;
                points[(i, j)] = terrain.At(p + terrain.Center);
            }

            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            void Triangle((int, int) a, (int, int) b, (int, int) c)
            {
                if (!points.TryGetValue(a, out var pa) || !points.TryGetValue(b, out var pb) || !points.TryGetValue(c, out var pc)) return;
                var n = Vector3.Cross(pb - pa, pc - pa);
                if (n.y < 0f) { (pb, pc) = (pc, pb); n = -n; }
                n.Normalize();
                var uv = ArtTools.PaletteUV(GroundCell(layout, terrain, paths, (pa + pb + pc) / 3f, n));
                foreach (var v in new[] { pa, pb, pc })
                {
                    triangles.Add(vertices.Count);
                    vertices.Add(v);
                    normals.Add(n);
                    uvs.Add(uv);
                }
            }
            for (int j = -nz; j < nz; j++)
            for (int i = -nx; i < nx; i++)
            {
                if ((j & 1) == 0)
                {
                    Triangle((i, j), (i + 1, j), (i, j + 1));
                    Triangle((i + 1, j), (i + 1, j + 1), (i, j + 1));
                }
                else
                {
                    Triangle((i, j), (i + 1, j), (i + 1, j + 1));
                    Triangle((i, j), (i + 1, j + 1), (i, j + 1));
                }
            }

            var mesh = new Mesh { indexFormat = vertices.Count > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            DaoDunDogBuilder.EnsureFolder(GroundMeshPath.Substring(0, GroundMeshPath.LastIndexOf('/')));
            mesh = PrologueSceneryBuilder.SaveMesh(mesh, GroundMeshPath);

            var ground = new GameObject("Ground", typeof(MeshFilter), typeof(MeshRenderer));
            ground.transform.SetParent(root, false);
            ground.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = ground.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(ArtTools.PaletteMaterial);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            ground.isStatic = true;
        }

        /// <summary>
        /// 地面一個三角面的色格：中心空地、小路、站位點前與營火周圍是泥土（邊緣用雜訊打散），可走範圍是草綠的亮、中、暗色塊，
        /// 牆邊與牆外林地偏暗，陡坡用暗階。
        /// </summary>
        static string GroundCell(HubLayoutData layout, Terrain terrain, List<(Vector2 a, Vector2 b)> paths, Vector3 center, Vector3 normal)
        {
            var p = new Vector2(center.x, center.z);
            float n = terrain.Noise(p, ColorScale, 0f);
            float ragged = 0.8f + 0.45f * terrain.Noise(p, ColorScale * 2.3f, 41f);
            float s = terrain.Outside(p);
            if (s > terrain.FlatBand)
            {
                if (normal.y < 0.92f) return "grass.d";
                return n < 0.35f ? "moss.d" : n < 0.65f ? "grass.d" : "moss.m";
            }
            if (Vector2.Distance(p, layout.plazaCenter) < layout.plazaRadius * ragged * 1.1f) return n < 0.45f ? "earth.m" : "earth.l";
            foreach (var (a, b) in paths)
            {
                if (SegmentDistance(p, a, b) < layout.pathWidth * 0.5f * ragged) return n < 0.35f ? "earth.d" : "earth.m";
            }
            foreach (var st in layout.stations)
            {
                if (st == null) continue;
                if (st.kind == HubStationKind.Campfire && Vector2.Distance(p, st.position) < CampfireDirt * ragged) return "earth.d";
                if (Vector2.Distance(p, Flat(HubBuilder.StandPosition(st))) < YardRadius * ragged) return "earth.m";
            }
            if (s > 0f) return n < 0.5f ? "moss.d" : "grass.d";
            return n > 0.64f ? "grass.l" : n < 0.32f ? "grass.d" : "grass.m";
        }

        // ---------------------------------------------------------------- 樹

        static int BuildForest(Transform root, Transform level, HubLayoutData layout, Terrain terrain, ArtLibrary library,
                               System.Random random, List<Vector3> trunks)
        {
            var forest = new GameObject("Forest").transform;
            forest.SetParent(root, false);
            int count = 0;
            // 佔位樹（有樹幹碰撞）的位置照舊，換外觀
            var legacy = level.Find("Trees");
            if (legacy != null)
            {
                foreach (Transform tree in legacy)
                {
                    var p = Flat(tree.position);
                    var kinds = terrain.Outside(p) < layout.wallOffset + EdgeTreeBand ? EdgeTreeKinds : RowTreeKinds;
                    if (Tree(forest, $"Tree_{count:000}", terrain.At(p), kinds, layout, library, random, true)) count++;
                    trunks.Add(terrain.At(p));
                }
            }
            // 裝飾樹：沿邊界往外推的折線；凹角往外推會打結，所以只收真的離邊界約那麼遠的點
            var placed = trunks.Select(Flat).ToList();
            for (int row = 0; row < layout.outerTreeRows; row++)
            {
                float distance = layout.outerRowStart + row * layout.outerRowSpacing;
                var ring = HubBuilder.Offset(layout.boundary, distance);
                float t = (float)random.NextDouble() * layout.outerTreeSpacing;
                for (int i = 0; i < ring.Length; i++)
                {
                    Vector2 a = ring[i], b = ring[(i + 1) % ring.Length];
                    float length = Vector2.Distance(a, b);
                    var dir = (b - a) / Mathf.Max(length, 1e-4f);
                    var normal = new Vector2(dir.y, -dir.x);
                    for (; t < length; t += layout.outerTreeSpacing)
                    {
                        var p = a + dir * (t + PrologueSceneryBuilder.Jitter(random) * 0.8f) + normal * PrologueSceneryBuilder.Jitter(random) * 0.9f;
                        if (Mathf.Abs(terrain.Outside(p) - distance) > 1.5f) continue;
                        if (placed.Any(q => (q - p).sqrMagnitude < layout.outerTreeSpacing * layout.outerTreeSpacing * 0.36f)) continue;
                        if (Tree(forest, $"Outer_{row}_{count:000}", terrain.At(p), OuterTreeKinds, layout, library, random, false)) count++;
                        placed.Add(p);
                        trunks.Add(terrain.At(p));
                    }
                    t -= length;
                }
            }
            return count;
        }

        /// <summary>collide：佔位樹的位置（玩家碰得到），樹幹加貼合模型的膠囊碰撞（比佔位的 trunkRadius 細就用 trunkRadius）。</summary>
        static bool Tree(Transform root, string name, Vector3 position, (string id, float weight)[] kinds, HubLayoutData layout,
                         ArtLibrary library, System.Random random, bool collide)
        {
            string id = PrologueSceneryBuilder.Pick(kinds, random);
            float scale = Mathf.Lerp(layout.treeScale.x, layout.treeScale.y, (float)random.NextDouble());
            float yaw = (float)random.NextDouble() * 360f;
            var model = library.Get(id);
            if (model == null) return false;
            var holder = PrologueSceneryBuilder.Holder(root, name, position, yaw, scale);
            ArtLibrary.Spawn(id, holder);
            if (collide)
            {
                var capsule = holder.gameObject.AddComponent<CapsuleCollider>();
                capsule.radius = Mathf.Max(layout.trunkRadius / scale, TrunkRadius(model));
                capsule.height = layout.wallHeight / scale;
                capsule.center = Vector3.up * capsule.height * 0.5f;
            }
            PrologueSceneryBuilder.MarkStatic(holder);
            return true;
        }

        static readonly Dictionary<GameObject, float> trunkRadii = new Dictionary<GameObject, float>();

        /// <summary>模型樹幹在離地 TrunkSample 高度的半徑（只看離中心軸 TrunkSearch 以內的頂點，排除低枝片）；量不到時 0。</summary>
        static float TrunkRadius(GameObject model)
        {
            if (trunkRadii.TryGetValue(model, out var cached)) return cached;
            float r = 0f;
            foreach (var filter in model.GetComponentsInChildren<MeshFilter>())
            {
                foreach (var v in filter.sharedMesh.vertices)
                {
                    if (v.y < TrunkSample.x || v.y > TrunkSample.y) continue;
                    float d = new Vector2(v.x, v.z).magnitude;
                    if (d < TrunkSearch) r = Mathf.Max(r, d);
                }
            }
            return trunkRadii[model] = r;
        }

        // ---------------------------------------------------------------- 站點

        static void BuildStations(Transform root, Transform stations, HubAtmosphereData atmosphere)
        {
            var group = new GameObject("Stations").transform;
            group.SetParent(root, false);
            foreach (var (station, models) in StationModels)
            {
                var source = stations.Find(station);
                if (source == null) continue;
                var holder = new GameObject(station).transform;
                holder.SetParent(group, false);
                holder.SetPositionAndRotation(source.position, source.rotation);
                foreach (var id in models) ArtLibrary.Spawn(id, holder);
                foreach (var (name, center, size) in StationBlockers.Where(b => b.station == station))
                {
                    var box = holder.gameObject.AddComponent<BoxCollider>();
                    box.center = center;
                    box.size = size;
                }
                if (station == "Blacksmith")
                {
                    // 佔位的鐵砧碰撞只有 0.45 × 0.35 m，比模型的樹墩細
                    var anvil = holder.gameObject.AddComponent<CapsuleCollider>();
                    anvil.radius = AnvilRadius;
                    anvil.height = AnvilHeight;
                    anvil.center = Vector3.up * AnvilHeight * 0.5f;
                }
                PrologueSceneryBuilder.MarkStatic(holder);
                if (station == "Campfire") DressCampfire(holder, source, atmosphere);
            }
        }

        /// <summary>營火：火焰片掛 FlameFlicker（不能是 Static，否則縮放不會動）、加火星粒子、座椅圓木放在佔位圓木的位置與方向。</summary>
        static void DressCampfire(Transform holder, Transform source, HubAtmosphereData atmosphere)
        {
            var flames = holder.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("Flame_")).OrderBy(t => t.name).ToArray();
            foreach (var f in flames) f.gameObject.isStatic = false;
            if (flames.Length > 0)
            {
                var flicker = flames[0].parent.gameObject.AddComponent<FlameFlicker>();
                var so = new SerializedObject(flicker);
                so.FindProperty("data").objectReferenceValue = atmosphere;
                IntroCutsceneBuilder.SetArray(so.FindProperty("flames"), flames.ToList());
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            BuildSparks(holder);
            foreach (Transform child in source)
            {
                if (!child.name.StartsWith("SeatLog")) continue;
                // 佔位圓木是轉倒的圓柱：本地 Y 是圓木的軸；SM_SeatLog 沿模型 X 長
                var axis = child.up;
                float yaw = Mathf.Atan2(-axis.z, axis.x) * Mathf.Rad2Deg;
                var seat = PrologueSceneryBuilder.Holder(holder, child.name, new Vector3(child.position.x, 0f, child.position.z), yaw, 1f);
                ArtLibrary.Spawn("SM_SeatLog", seat);
                // 刀盾狗坐的那根（站位點就在正上方）照 T21 走得上去，其他兩根擋路
                if (child.name != FrontSeat)
                {
                    var bounds = MeshBounds(seat.gameObject);
                    var capsule = seat.gameObject.AddComponent<CapsuleCollider>();
                    capsule.direction = 0; // 沿圓木（模型 X）
                    capsule.radius = bounds.extents.y;
                    capsule.height = bounds.size.x;
                    capsule.center = bounds.center;
                }
                PrologueSceneryBuilder.MarkStatic(seat);
            }
        }

        // 火星：從火焰往上飄的小亮點（原本的火焰粒子跟佔位外觀一起關掉）
        static void BuildSparks(Transform holder)
        {
            var go = new GameObject("Sparks");
            go.transform.SetParent(holder, false);
            go.transform.localPosition = Vector3.up * 0.5f;
            go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1f, 1.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.08f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.6f, 0.2f), new Color(1f, 0.85f, 0.45f));
            main.maxParticles = 40;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = ps.emission;
            emission.rateOverTime = 12f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 18f;
            shape.radius = 0.2f;
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.35f;
            noise.frequency = 0.8f;
            var color = ps.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.85f, 0.45f), 0f), new GradientColorKey(new Color(1f, 0.35f, 0.08f), 1f) },
                             new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
            color.color = gradient;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(FireMaterialPath);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        // ---------------------------------------------------------------- 小道具與草

        /// <summary>不放草的圓：站點本體、站位點、出生點、擺好的小道具。</summary>
        static List<(Vector2 center, float radius)> KeepOut(HubLayoutData layout)
        {
            var list = new List<(Vector2, float)> { (layout.hutPosition, 4.3f) };
            foreach (var s in layout.stations)
            {
                if (s == null) continue;
                list.Add((Flat(HubBuilder.StandPosition(s)), layout.sceneryClearance));
                switch (s.kind)
                {
                    case HubStationKind.Blacksmith: // 棚子往站點背後延伸
                        list.Add((s.position - (Vector2)Flat(HubLayoutData.Direction(s.facing)) * 1.3f, 3.4f));
                        break;
                    case HubStationKind.Campfire:
                        list.Add((s.position, 2.8f));
                        break;
                    case HubStationKind.Portal:
                        list.Add((s.position, 3.2f));
                        break;
                    default:
                        list.Add((s.position, 1.2f));
                        break;
                }
            }
            list.Add((layout.startSpawn, layout.sceneryClearance));
            list.Add((layout.wakeSpawn, layout.sceneryClearance));
            var portal = layout.Get(HubStationKind.Portal);
            if (portal != null)
                list.Add((portal.position + (Vector2)Flat(HubLayoutData.Direction(portal.facing)) * layout.portalSpawnDistance, layout.sceneryClearance));
            if (layout.props != null)
                list.AddRange(layout.props.Select(p => (p.position, 0.9f * Mathf.Max(p.scale, 0.1f))));
            return list;
        }

        static bool Blocked(Vector2 p, List<(Vector2 center, float radius)> keepOut) =>
            keepOut.Any(k => (p - k.center).sqrMagnitude < k.radius * k.radius);

        static int BuildVillageProps(Transform root, HubLayoutData layout, Terrain terrain, ArtLibrary library, List<(Vector2, float)> keepOut)
        {
            var group = new GameObject("Village").transform;
            group.SetParent(root, false);
            int count = 0;
            if (layout.props == null) return 0;
            foreach (var prop in layout.props)
            {
                if (string.IsNullOrEmpty(prop.id) || library.Get(prop.id) == null) continue;
                var holder = PrologueSceneryBuilder.Holder(group, $"{prop.id}_{count:00}", terrain.At(prop.position), prop.yaw, Mathf.Max(prop.scale, 0.1f));
                ArtLibrary.Spawn(prop.id, holder);
                AddPropCollider(holder.gameObject, prop.id);
                PrologueSceneryBuilder.MarkStatic(holder);
                count++;
            }
            return count;
        }

        /// <summary>
        /// 村子小道具的碰撞（可走範圍裡，玩家碰得到）：圓的（木桶）用直立膠囊，其他用包住整個模型的方塊
        /// （告示板兩根柱子中間也擋，不從板子底下鑽過去）。都在 Default 層，鏡頭也會被擋（同樹幹）。
        /// </summary>
        static void AddPropCollider(GameObject holder, string id)
        {
            var bounds = MeshBounds(holder);
            if (id == "SM_Barrel")
            {
                var capsule = holder.AddComponent<CapsuleCollider>();
                capsule.radius = Mathf.Max(bounds.extents.x, bounds.extents.z);
                capsule.height = bounds.size.y;
                capsule.center = bounds.center;
                return;
            }
            var box = holder.AddComponent<BoxCollider>();
            box.center = bounds.center;
            box.size = bounds.size;
        }

        /// <summary>holder 底下所有網格在 holder 本地座標的包圍盒（模型生在 holder 原點、不旋轉）。</summary>
        static Bounds MeshBounds(GameObject holder)
        {
            var bounds = new Bounds();
            bool first = true;
            var toLocal = holder.transform.worldToLocalMatrix;
            foreach (var filter in holder.GetComponentsInChildren<MeshFilter>())
            {
                var b = filter.sharedMesh.bounds;
                var m = toLocal * filter.transform.localToWorldMatrix;
                for (int i = 0; i < 8; i++)
                {
                    var corner = m.MultiplyPoint3x4(b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)));
                    if (first) bounds = new Bounds(corner, Vector3.zero);
                    else bounds.Encapsulate(corner);
                    first = false;
                }
            }
            return bounds;
        }

        static float OuterEdge(HubLayoutData layout) =>
            layout.outerRowStart + Mathf.Max(layout.outerTreeRows - 1, 0) * layout.outerRowSpacing + 2f;

        static (Vector2 min, Vector2 max) Bounds(HubLayoutData layout, float margin)
        {
            Vector2 min = Vector2.positiveInfinity, max = Vector2.negativeInfinity;
            foreach (var p in layout.boundary)
            {
                min = Vector2.Min(min, p);
                max = Vector2.Max(max, p);
            }
            return (min - Vector2.one * margin, max + Vector2.one * margin);
        }

        static Vector2 RandomIn((Vector2 min, Vector2 max) box, System.Random random) =>
            new Vector2(Mathf.Lerp(box.min.x, box.max.x, (float)random.NextDouble()), Mathf.Lerp(box.min.y, box.max.y, (float)random.NextDouble()));

        static int BuildGrass(Transform root, HubLayoutData layout, Terrain terrain, ArtLibrary library, System.Random random,
                              List<Vector3> trunks, List<(Vector2, float)> keepOut, List<(Vector2 a, Vector2 b)> paths)
        {
            var meshes = new Dictionary<string, Mesh>();
            foreach (var (id, _) in VillageGrass.Concat(ForestGrass))
            {
                var model = library.Get(id);
                var filter = model != null ? model.GetComponentInChildren<MeshFilter>() : null;
                if (filter != null) meshes[id] = filter.sharedMesh;
            }
            if (meshes.Count == 0) return 0;

            var chunks = new Dictionary<Vector2Int, Dictionary<string, List<InstancedScatter.Instance>>>();
            float outer = OuterEdge(layout);
            var box = Bounds(layout, outer);
            float boxArea = (box.max.x - box.min.x) * (box.max.y - box.min.y);
            float wallOuter = layout.wallOffset + layout.wallThickness;
            // 可走範圍（到隱形牆內緣）：稀疏的矮草，避開小路與空地
            int placed = Scatter(-1000f, layout.wallOffset, layout.grassDensity, VillageGrass, true);
            placed += Scatter(wallOuter, outer, layout.forestGrassDensity, ForestGrass, false);

            int Scatter(float inner, float outerDistance, float density, (string id, float weight)[] kinds, bool village)
            {
                if (density <= 0f) return 0;
                // 在外框裡均勻撒點、只收落在這一帶的，期望數量 = density × 這一帶的面積；一處 1–5 叢
                int attempts = Mathf.RoundToInt(boxArea * density / 3f);
                int done = 0;
                for (int k = 0; k < attempts; k++)
                {
                    var center = RandomIn(box, random);
                    int cluster = 1 + random.Next(5);
                    for (int c = 0; c < cluster; c++)
                    {
                        var p = center + new Vector2(PrologueSceneryBuilder.Jitter(random), PrologueSceneryBuilder.Jitter(random)) * 0.5f;
                        float s = terrain.Outside(p);
                        if (s < inner || s > outerDistance) continue;
                        if (Blocked(p, keepOut) || PrologueSceneryBuilder.NearTrunk(p, trunks, TrunkClearance)) continue;
                        if (village && (Vector2.Distance(p, layout.plazaCenter) < layout.plazaRadius + 0.6f ||
                                        paths.Any(seg => SegmentDistance(p, seg.a, seg.b) < layout.pathWidth * 0.5f + 0.5f))) continue;
                        string id = PrologueSceneryBuilder.Pick(kinds, random);
                        if (!meshes.ContainsKey(id)) continue;
                        var instance = new InstancedScatter.Instance
                        {
                            position = terrain.At(p),
                            yaw = (float)random.NextDouble() * 360f,
                            scale = Mathf.Lerp(village ? 0.7f : 0.8f, village ? 1.05f : 1.3f, (float)random.NextDouble()),
                        };
                        var key = new Vector2Int(Mathf.FloorToInt(p.x / ChunkSize), Mathf.FloorToInt(p.y / ChunkSize));
                        if (!chunks.TryGetValue(key, out var byKind)) chunks[key] = byKind = new Dictionary<string, List<InstancedScatter.Instance>>();
                        if (!byKind.TryGetValue(id, out var list)) byKind[id] = list = new List<InstancedScatter.Instance>();
                        list.Add(instance);
                        done++;
                    }
                }
                return done;
            }

            var grass = new GameObject("Grass").transform;
            grass.SetParent(root, false);
            var material = AssetDatabase.LoadAssetAtPath<Material>(ArtTools.FoliageMaterial);
            foreach (var pair in chunks.OrderBy(c => c.Key.x).ThenBy(c => c.Key.y))
            {
                var go = new GameObject($"Grass_{pair.Key.x}_{pair.Key.y}");
                go.transform.SetParent(grass, false);
                go.AddComponent<InstancedScatter>().SetData(material, pair.Value.OrderBy(k => k.Key)
                    .Select(k => new InstancedScatter.Kind { mesh = meshes[k.Key], instances = k.Value.ToArray() }).ToArray());
            }
            return placed;
        }

        static int BuildForestProps(Transform root, HubLayoutData layout, Terrain terrain, ArtLibrary library, System.Random random, List<Vector3> trunks)
        {
            var group = new GameObject("Props").transform;
            group.SetParent(root, false);
            float inner = layout.wallOffset + layout.wallThickness + 0.5f;
            float outer = OuterEdge(layout);
            var box = Bounds(layout, outer);
            float boxArea = (box.max.x - box.min.x) * (box.max.y - box.min.y);
            var taken = new List<Vector3>(trunks);
            int placed = 0;
            foreach (var kind in ForestProps)
            {
                int attempts = Mathf.RoundToInt(boxArea * kind.density * layout.propDensity);
                for (int k = 0; k < attempts; k++)
                {
                    var p = RandomIn(box, random);
                    float s = terrain.Outside(p);
                    if (s < inner || s > outer || PrologueSceneryBuilder.NearTrunk(p, taken, kind.clearance)) continue;
                    string id = kind.ids[random.Next(kind.ids.Length)];
                    var model = library.Get(id);
                    if (model == null) continue;
                    float scale = Mathf.Lerp(kind.scale.x, kind.scale.y, (float)random.NextDouble());
                    var position = terrain.At(p);
                    position.y -= model.GetComponentInChildren<MeshFilter>().sharedMesh.bounds.size.y * scale * kind.sink;
                    var holder = PrologueSceneryBuilder.Holder(group, $"{id}_{placed:000}", position, (float)random.NextDouble() * 360f, scale);
                    ArtLibrary.Spawn(id, holder);
                    PrologueSceneryBuilder.MarkStatic(holder);
                    taken.Add(position);
                    placed++;
                }
            }
            return placed;
        }
    }
}
