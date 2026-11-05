using System.Collections.Generic;
using System.Linq;
using BossFight.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace BossFight.EditorTools
{
    /// <summary>
    /// 競技場的暗林佈景（docs/Art/ART_STYLE.md「競技場：風雨交加的黑暗森林」，美術 Phase 7），參數在 ArenaData 的「佈景」。
    /// ArenaBuilder.Build 在 forestScenery 開著時最後呼叫 Build；只改佈景時跑 Tools > Arena > Build Scenery（原地重建 Arena 底下的 Scenery，不動 Generated）。
    /// 玩法完全不變：外牆、門、門柱與地板的碰撞還是 ArenaBuilder 建的那些（只把它們的 MeshRenderer 關掉），佈景本身沒有任何碰撞，
    /// 所以 Boss 撞牆、鏡頭的 Deoccluder、出生點都照舊。由內到外：
    /// - 地面：Builder 產生的低面數網格（M_Palette 色格），牆內是 y = 0 的泥地，牆外起伏、往外升高。
    /// - 地板小件：魔法陣外圈一圈半埋的石板、水窪、從牆邊往內爬的貼地樹根（都只高出地面約 3 cm）。
    /// - 撞牆圈：巨根、倒木、岩塊模組沿外牆內緣排一圈（內面對齊半徑 radius），入口兩側留給入口模型。
    /// - 入口：每個入口放一個 SM_Gate_Thorns（兩棵巨樹夾一道荊棘），位置與朝向跟門的物件一樣。
    /// - 近林（nearForest）：扭曲巨樹、枯樹與少數針葉樹；遠林（farForest）：針葉樹剪影。
    /// - 牆外的草叢（Core/InstancedScatter）與道具（灌木、石頭、枯枝、樹樁、倒木）。
    /// 沒有正式模型時不建佈景，原本的看台版外觀維持可見。
    /// </summary>
    public static class ArenaSceneryBuilder
    {
        public const string RootName = "Scenery";
        const string GroundMeshPath = "Assets/Models/Arena/Meshes/Arena_ForestGround.asset"; // 每次重建覆寫（保留 GUID）
        const string GeneratedName = "Generated";
        // 以下是純編輯器的散佈參數（視覺）
        const float FlatMargin = 2f;         // 牆外多少 m 以內仍然平坦
        const float GroundWarp = 10f;        // 牆外地面格距放大的快慢（m）：越小放大越快
        const float ReliefScale = 0.07f;     // 地面起伏的雜訊頻率（1/m）
        const float ColorScale = 0.15f;      // 地面色塊的雜訊頻率（1/m）
        const float GateRimClearance = 5f;   // 入口中心兩側這麼遠（沿圓周，m）以內不放撞牆圈模組（入口模型的巨樹與板根在這裡）
        const int GateGroveTrees = 4;        // 每個入口後方補種的樹數
        const float GateGroveGap = 1f;       // 入口後方的樹離外牆外緣至少這麼遠（m）
        const float GateGroveSpacing = 0.75f; // 入口後方的樹比近林種得密（間距乘這個）
        const float ChunkSize = 12f;         // 草叢一個 InstancedScatter 管的區塊大小（m）
        const float TrunkClearance = 0.6f;   // 草離樹幹至少這麼遠（m）
        const float SpawnClearance = 1.6f;   // 水窪離玩家出生點至少這麼遠（m）
        const float IntroViewStep = 5f;      // 檢查開場第 1 鏡視線時，鏡頭路徑每幾度取一次
        const float IntroCrownRadius = 2.5f; // 樹冠離視線這麼近（m）就算擋住
        const float IntroCameraClearance = 4f; // 樹離開場第 1 鏡的路徑至少這麼遠（m），鏡頭才不會在樹冠裡

        static readonly string[] RimKinds =
            { "SM_Rim_Root_A", "SM_Rim_Root_B", "SM_Rim_Log_A", "SM_Rim_Log_B", "SM_Rim_Rock_A", "SM_Rim_Rock_B" };
        const string GateModel = "SM_Gate_Thorns";
        static readonly (string id, float weight)[] NearTrees =
        {
            ("SM_Tree_Gnarled_A", 0.26f), ("SM_Tree_Gnarled_B", 0.22f), ("SM_Tree_Gnarled_C", 0.2f),
            ("SM_Tree_Dead_A", 0.1f), ("SM_Tree_Dead_B", 0.07f), ("SM_Tree_Pine_C", 0.08f), ("SM_Tree_Pine_D", 0.07f),
        };
        static readonly (string id, float weight)[] FarTrees =
        {
            ("SM_Tree_Pine_A", 0.3f), ("SM_Tree_Pine_B", 0.25f), ("SM_Tree_Pine_C", 0.18f), ("SM_Tree_Pine_D", 0.1f),
            ("SM_Tree_Gnarled_C", 0.12f), ("SM_Tree_Dead_A", 0.05f),
        };
        static readonly (string id, float weight)[] Grass =
            { ("SM_Grass_Clump_C", 0.35f), ("SM_Grass_Clump_B", 0.4f), ("SM_Grass_Clump_A", 0.25f) };
        static readonly string[] Slabs = { "SM_Arena_Slab_A", "SM_Arena_Slab_B", "SM_Arena_Slab_C" };
        static readonly string[] FloorRoots = { "SM_Arena_RootFlat_A", "SM_Arena_RootFlat_B" };
        static readonly string[] Puddles = { "SM_Arena_Puddle_A", "SM_Arena_Puddle_B" };

        struct PropKind
        {
            public string[] ids;
            public float density;     // 個 / m²（乘上 propDensity）
            public Vector2 scale;
            public float sink;        // 往地裡埋的比例（石頭在斜坡上不浮空）
            public float clearance;   // 離樹幹與其他道具至少多遠（m）
        }

        static readonly PropKind[] Props =
        {
            new PropKind { ids = new[] { "SM_Log_A", "SM_Log_B" }, density = 0.004f, scale = new Vector2(1.1f, 1.6f), clearance = 1.6f },
            new PropKind { ids = new[] { "SM_Rock_E", "SM_Rock_F" }, density = 0.003f, scale = new Vector2(0.9f, 1.4f), sink = 0.12f, clearance = 1.6f },
            new PropKind { ids = new[] { "SM_Stump_A", "SM_Stump_B" }, density = 0.004f, scale = new Vector2(0.9f, 1.3f), clearance = 1f },
            new PropKind { ids = new[] { "SM_Bush_A", "SM_Bush_B", "SM_Bush_C" }, density = 0.02f, scale = new Vector2(0.8f, 1.3f), clearance = 0.6f },
            new PropKind { ids = new[] { "SM_Rock_A", "SM_Rock_B" }, density = 0.008f, scale = new Vector2(0.7f, 1.3f), sink = 0.12f, clearance = 0.8f },
            new PropKind { ids = new[] { "SM_Twig_A", "SM_Twig_B", "SM_Twig_C" }, density = 0.016f, scale = new Vector2(0.8f, 1.4f), clearance = 0.3f },
            new PropKind { ids = new[] { "SM_Rock_C", "SM_Rock_D" }, density = 0.01f, scale = new Vector2(0.8f, 1.5f), sink = 0.1f, clearance = 0.3f },
        };

        [MenuItem("Tools/Arena/Build Scenery")]
        public static void BuildFromMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var g = Object.FindAnyObjectByType<ArenaGenerator>();
            if (g == null || g.data == null)
            {
                Debug.LogWarning("[Arena] 找不到 ArenaGenerator（或沒有 ArenaData），先開 BossArena 場景、跑 Tools > Arena > Build Arena。");
                return;
            }
            Debug.Log(Build(g));
            EditorSceneManager.MarkSceneDirty(g.gameObject.scene);
            EditorSceneManager.SaveScene(g.gameObject.scene);
        }

        /// <summary>在 ArenaGenerator 底下重建 Scenery（舊的先刪掉），關掉 Generated 裡外牆、門、地板的外觀；回傳摘要。</summary>
        public static string Build(ArenaGenerator g)
        {
            var d = g.data;
            var old = g.transform.Find(RootName);
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var generated = g.transform.Find(GeneratedName);

            var library = ArtLibrary.Load();
            var missing = RimKinds.Append(GateModel).Concat(NearTrees.Select(t => t.id)).Concat(Slabs)
                .Where(id => library == null || library.Get(id) == null).ToList();
            if (!d.forestScenery || missing.Count > 0)
            {
                SetLegacyVisible(generated, true);
                return d.forestScenery
                    ? $"[Arena] 缺少正式模型（{string.Join("、", missing)}），暗林佈景沒有建立，維持原本的外觀。"
                    : "[Arena] ArenaData.forestScenery 關著，維持原本的外觀。";
            }

            var root = new GameObject(RootName).transform;
            root.SetParent(g.transform, false);
            var center = g.transform.position;
            var terrain = new Terrain(d);
            var random = new System.Random(d.visualSeed);
            var taken = new List<Vector3>(); // 樹幹與大道具的位置（世界座標），草與小道具避開

            BuildGround(root, d, terrain, center);
            int rim = BuildRim(root, d, random, center);
            BuildGates(root, d, center);
            int floor = BuildFloor(root, d, random, center);
            int trees = BuildForest(root, d, terrain, library, random, center, taken);
            int clumps = BuildGrass(root, d, terrain, library, random, center, taken);
            int props = BuildProps(root, d, terrain, library, random, center, taken);
            SetLegacyVisible(generated, false);
            AssetDatabase.SaveAssets();
            return $"[Arena] 暗林佈景：撞牆圈 {rim} 個模組、地板小件 {floor} 個、{trees} 棵樹、{clumps} 叢草、{props} 個道具。";
        }

        /// <summary>Generated 裡看台版的外觀：visible = false 時關掉地板、外牆、門與門柱的 MeshRenderer（碰撞留著），刪掉看台、最外圈外牆與外圍地面。</summary>
        static void SetLegacyVisible(Transform generated, bool visible)
        {
            if (generated == null) return;
            foreach (var name in new[] { "Floor", "PodiumWall" })
            {
                var t = generated.Find(name);
                if (t != null && t.TryGetComponent<MeshRenderer>(out var mr)) mr.enabled = visible;
            }
            var gates = generated.Find("Gates");
            if (gates != null)
                foreach (var mr in gates.GetComponentsInChildren<MeshRenderer>(true)) mr.enabled = visible;
            if (visible) return;
            foreach (var name in new[] { "Stands", "RimWall", "OuterGround" })
            {
                var t = generated.Find(name);
                if (t != null) Object.DestroyImmediate(t.gameObject);
            }
        }

        // ---------------------------------------------------------------- 地形

        /// <summary>地面的高度（以場地中心為原點）：牆內與牆外 FlatMargin 以內是 0，之後起伏、往外升高。</summary>
        class Terrain
        {
            readonly float flat, outer, relief, rise;
            readonly Vector2 offset;

            public Terrain(ArenaData d)
            {
                flat = d.radius + d.wallThickness + FlatMargin;
                outer = d.groundRadius;
                relief = d.groundRelief;
                rise = d.groundRise;
                var r = new System.Random(d.visualSeed + 1);
                offset = new Vector2((float)r.NextDouble() * 100f, (float)r.NextDouble() * 100f);
            }

            public float FlatRadius => flat;

            public float Height(float x, float z)
            {
                float r = Mathf.Sqrt(x * x + z * z);
                if (r <= flat) return 0f;
                float bumps = Mathf.SmoothStep(0f, 1f, (r - flat) / 6f) *
                              (Mathf.PerlinNoise(x * ReliefScale + offset.x, z * ReliefScale + offset.y) - 0.35f) * relief;
                float t = Mathf.Clamp01((r - flat - 12f) / Mathf.Max(outer - flat - 12f, 1f));
                return Mathf.Max(bumps, -0.2f) + rise * t * t;
            }

            public Vector3 At(Vector2 p) => new Vector3(p.x, Height(p.x, p.y), p.y);

            public float Noise(Vector2 p, float scale, float seed) =>
                Mathf.PerlinNoise(p.x * scale + offset.x + seed, p.y * scale + offset.y + seed);
        }

        static void BuildGround(Transform root, ArenaData d, Terrain terrain, Vector3 center)
        {
            // 三角格子（列與列錯開半格），平坦區格距固定，牆外依 GroundWarp 往外拉大（同序章的地面）
            float s = d.groundCellSize, r0 = terrain.FlatRadius, L = GroundWarp;
            float outer = Mathf.Max(d.groundRadius, r0 + s);
            float maxD = r0 + (-1f + Mathf.Sqrt(1f + 4f * (outer - r0) / L)) * L * 0.5f;
            float rowH = s * Mathf.Sqrt(3f) * 0.5f;
            int nx = Mathf.CeilToInt(maxD / s) + 1, nz = Mathf.CeilToInt(maxD / rowH) + 1;
            var random = new System.Random(d.visualSeed + 2);
            var points = new Dictionary<(int, int), Vector3>();
            for (int j = -nz; j <= nz; j++)
            for (int i = -nx; i <= nx; i++)
            {
                var p = new Vector2((i + ((j & 1) != 0 ? 0.5f : 0f)) * s, j * rowH);
                p += new Vector2((float)random.NextDouble() - 0.5f, (float)random.NextDouble() - 0.5f) * s * 0.4f;
                float dist = p.magnitude;
                if (dist > maxD) continue;
                if (dist > r0) p *= (r0 + (dist - r0) * (1f + (dist - r0) / L)) / dist;
                points[(i, j)] = terrain.At(p);
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
                var uv = ArtTools.PaletteUV(GroundCell(d, terrain, (pa + pb + pc) / 3f, n));
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
            ground.transform.position = center;
            ground.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = ground.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(ArtTools.PaletteMaterial);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            ground.isStatic = true; // 地板碰撞仍是 Generated/Floor 的平面，這張網格只負責外觀
        }

        /// <summary>地面一個三角面的色格：場地是深褐泥地（暗泥、土色斑），牆腳長苔，牆外林地是暗泥與苔蘚，陡坡用暗階。</summary>
        static string GroundCell(ArenaData d, Terrain terrain, Vector3 c, Vector3 normal)
        {
            var p = new Vector2(c.x, c.z);
            float r = p.magnitude;
            float n = terrain.Noise(p, ColorScale, 0f);
            float n2 = terrain.Noise(p, ColorScale * 1.9f, 37f);
            if (r < d.radius)
            {
                if (r > d.radius - 1.4f && n2 > 0.5f) return "moss.d";
                return n < 0.3f ? "mud.d" : n > 0.68f ? "earth.d" : "mud.m";
            }
            if (normal.y < 0.92f) return "mud.d";
            return n < 0.35f ? "moss.d" : n2 > 0.62f ? "bark_dark.m" : "mud.d";
        }

        // ---------------------------------------------------------------- 撞牆圈與入口

        static int BuildRim(Transform root, ArenaData d, System.Random random, Vector3 center)
        {
            var parent = new GameObject("RimWall").transform;
            parent.SetParent(root, false);
            float R = d.radius;
            float step = d.rimSpacing / R * Mathf.Rad2Deg;
            int n = Mathf.CeilToInt(360f / step);
            step = 360f / n;
            float gateHalf = Mathf.Asin(Mathf.Clamp01(GateRimClearance / R)) * Mathf.Rad2Deg;
            int count = 0;
            string previous = null;
            for (int i = 0; i < n; i++)
            {
                float angle = i * step + PrologueSceneryBuilder.Jitter(random) * step * 0.15f;
                if (d.gates != null && d.gates.Any(gate => Mathf.Abs(Mathf.DeltaAngle(angle, gate.angle)) < gateHalf)) continue;
                string id;
                do id = RimKinds[random.Next(RimKinds.Length)]; while (id == previous);
                previous = id;
                // 內面對齊半徑 R（模型的內面在 z = 0、往外長），稍微往外退一點，轉一點角度也不會伸進場地
                var position = center + ArenaData.Dir(angle) * (R + 0.15f + (float)random.NextDouble() * 0.25f);
                float yaw = angle + 180f + PrologueSceneryBuilder.Jitter(random) * 4f;
                float scale = Mathf.Lerp(d.rimScale.x, d.rimScale.y, (float)random.NextDouble());
                var holder = PrologueSceneryBuilder.Holder(parent, $"{id}_{count:00}", position, yaw, scale);
                ArtLibrary.Spawn(id, holder);
                PrologueSceneryBuilder.MarkStatic(holder);
                count++;
            }
            return count;
        }

        static void BuildGates(Transform root, ArenaData d, Vector3 center)
        {
            if (d.gates == null) return;
            var parent = new GameObject("Gates").transform;
            parent.SetParent(root, false);
            // 跟 ArenaBuilder.BuildGate 的物件同一個位置與朝向：牆內緣線上、+Z 朝場地中央
            foreach (var gate in d.gates)
            {
                var holder = PrologueSceneryBuilder.Holder(parent, string.IsNullOrEmpty(gate.name) ? "Gate" : gate.name,
                    center + ArenaData.Dir(gate.angle) * d.radius, gate.angle + 180f, 1f);
                ArtLibrary.Spawn(GateModel, holder);
                PrologueSceneryBuilder.MarkStatic(holder);
            }
        }

        // ---------------------------------------------------------------- 地板小件

        static int BuildFloor(Transform root, ArenaData d, System.Random random, Vector3 center)
        {
            var parent = new GameObject("Floor").transform;
            parent.SetParent(root, false);
            float R = d.radius;
            int count = 0;

            // 魔法陣外圈的半埋石板：長邊沿圓周，偶爾缺一塊（古老、殘缺）
            for (int i = 0; i < d.slabCount; i++)
            {
                if (random.NextDouble() < 0.12) continue;
                float angle = (i + PrologueSceneryBuilder.Jitter(random) * 0.15f) * 360f / d.slabCount;
                float r = d.slabRingRadius + PrologueSceneryBuilder.Jitter(random) * 0.2f;
                Flat(parent, Slabs[random.Next(Slabs.Length)], center + ArenaData.Dir(angle) * r,
                     angle + PrologueSceneryBuilder.Jitter(random) * 12f, Mathf.Lerp(0.9f, 1.15f, (float)random.NextDouble()), count++);
            }

            // 貼地樹根：從牆腳往場地中央爬（模型沿 +X 長）
            float gateHalf = Mathf.Asin(Mathf.Clamp01(d.gateWidth / R)) * Mathf.Rad2Deg;
            for (int i = 0, guard = 0; i < d.floorRootCount && guard < d.floorRootCount * 10; guard++)
            {
                float angle = (float)random.NextDouble() * 360f;
                if (d.gates != null && d.gates.Any(gate => Mathf.Abs(Mathf.DeltaAngle(angle, gate.angle)) < gateHalf)) continue;
                Flat(parent, FloorRoots[random.Next(FloorRoots.Length)], center + ArenaData.Dir(angle) * (R - 0.1f),
                     angle + 90f + PrologueSceneryBuilder.Jitter(random) * 18f, Mathf.Lerp(0.8f, 1.25f, (float)random.NextDouble()), count++);
                i++;
            }

            // 水窪：避開石板圈、玩家出生點與彼此
            var spawn = Vector2.zero;
            if (d.TryGetGate("PlayerGate", out var playerGate))
            {
                var s = ArenaData.Dir(playerGate.angle) * d.playerSpawnDistance;
                spawn = new Vector2(s.x, s.z);
            }
            var puddles = new List<Vector2>();
            for (int guard = 0; puddles.Count < d.puddleCount && guard < d.puddleCount * 30; guard++)
            {
                var p = PrologueSceneryBuilder.RingPoint(random, 2.5f, R - 2f);
                if (Mathf.Abs(p.magnitude - d.slabRingRadius) < 1.2f || (p - spawn).magnitude < SpawnClearance ||
                    puddles.Any(q => (p - q).magnitude < 3f)) continue;
                puddles.Add(p);
                Flat(parent, Puddles[random.Next(Puddles.Length)], center + new Vector3(p.x, 0f, p.y),
                     (float)random.NextDouble() * 360f, Mathf.Lerp(0.8f, 1.4f, (float)random.NextDouble()), count++);
            }
            return count;
        }

        /// <summary>貼地的小件：不投影（只高出地面幾公分，投影只會是一條雜訊）。</summary>
        static void Flat(Transform parent, string id, Vector3 position, float yaw, float scale, int index)
        {
            var holder = PrologueSceneryBuilder.Holder(parent, $"{id}_{index:00}", position, yaw, scale);
            var go = ArtLibrary.Spawn(id, holder);
            if (go == null) { Object.DestroyImmediate(holder.gameObject); return; }
            foreach (var mr in go.GetComponentsInChildren<MeshRenderer>()) mr.shadowCastingMode = ShadowCastingMode.Off;
            PrologueSceneryBuilder.MarkStatic(holder);
        }

        // ---------------------------------------------------------------- 樹林

        static int BuildForest(Transform root, ArenaData d, Terrain terrain, ArtLibrary library, System.Random random, Vector3 center,
                               List<Vector3> taken)
        {
            var parent = new GameObject("Forest").transform;
            parent.SetParent(root, false);
            var placed = new List<Vector2>();
            int grove = Grove();
            int near = Ring("Near", d.nearForest, d.nearTreeSpacing, NearTrees);
            int far = Ring("Far", d.farForest, d.farTreeSpacing, FarTrees);
            return grove + near + far;

            // 入口兩側沒有撞牆圈模組（留給入口模型），近林又從 nearForest.x 才開始，入口後面會空出一塊：
            // 每個入口後方先種幾棵，從牆外 GateGroveGap 起到近林內緣
            int Grove()
            {
                if (d.gates == null) return 0;
                float half = Mathf.Asin(Mathf.Clamp01(GateRimClearance / d.radius)) * Mathf.Rad2Deg;
                float inner = d.radius + d.wallThickness + GateGroveGap;
                int done = 0;
                foreach (var gate in d.gates)
                    for (int k = 0, guard = 0; k < GateGroveTrees && guard < GateGroveTrees * 30; guard++)
                    {
                        var dir = ArenaData.Dir(gate.angle + PrologueSceneryBuilder.Jitter(random) * half);
                        var p = new Vector2(dir.x, dir.z) * Mathf.Lerp(inner, d.nearForest.x + 1f, (float)random.NextDouble());
                        if (TryPlace("Gate", done, p, d.nearTreeSpacing * GateGroveSpacing, NearTrees)) { done++; k++; }
                    }
                return done;
            }

            // 圓環裡隨機撒點、彼此至少相隔 spacing（拒絕取樣）
            int Ring(string label, Vector2 range, float spacing, (string id, float weight)[] kinds)
            {
                float area = Mathf.PI * (range.y * range.y - range.x * range.x);
                int target = Mathf.RoundToInt(area / (spacing * spacing * 1.25f));
                int done = 0;
                for (int guard = 0; done < target && guard < target * 30; guard++)
                    if (TryPlace(label, done, PrologueSceneryBuilder.RingPoint(random, range.x, range.y), spacing, kinds)) done++;
                return done;
            }

            // 一棵樹：離已種的樹至少 spacing、不擋開場第 1 鏡
            bool TryPlace(string label, int index, Vector2 p, float spacing, (string id, float weight)[] kinds)
            {
                if (placed.Any(q => (p - q).sqrMagnitude < spacing * spacing)) return false;
                string id = PrologueSceneryBuilder.Pick(kinds, random);
                var model = library.Get(id);
                if (model == null) return false;
                float scale = Mathf.Lerp(d.treeScale.x, d.treeScale.y, (float)random.NextDouble());
                float height = model.GetComponentInChildren<MeshFilter>().sharedMesh.bounds.max.y * scale;
                if (BlocksIntroView(d, p, height)) return false;
                placed.Add(p);
                var position = center + terrain.At(p);
                var holder = PrologueSceneryBuilder.Holder(parent, $"{label}_{index:000}", position, (float)random.NextDouble() * 360f, scale);
                ArtLibrary.Spawn(id, holder);
                PrologueSceneryBuilder.MarkStatic(holder);
                taken.Add(position);
                return true;
            }
        }

        /// <summary>
        /// 開場第 1 鏡在場外 introCameraRadius、高 introCameraHeight，從 introCameraArc.x 繞到 .y，看著入口內側的狗（地面）。
        /// 這棵樹（以場地中心為原點的 xz 位置 p、樹高 height）離鏡頭的路徑太近（鏡頭會在樹冠裡），
        /// 或擋住 introSightArc 那段任一刻的視線時回傳 true。
        /// </summary>
        static bool BlocksIntroView(ArenaData d, Vector2 p, float height)
        {
            if (!d.TryGetGate("PlayerGate", out var gate)) return false;
            var g = ArenaData.Dir(gate.angle) * (d.radius - 1f);
            var target = new Vector2(g.x, g.z);
            for (float a = d.introCameraArc.x; a <= d.introCameraArc.y + 0.01f; a += IntroViewStep)
            {
                var c = ArenaData.Dir(a) * d.introCameraRadius;
                var camera = new Vector2(c.x, c.z);
                if ((p - camera).magnitude < IntroCameraClearance) return true;
                if (a < d.introSightArc.x || a > d.introSightArc.y) continue;
                var line = target - camera;
                float t = Mathf.Clamp01(Vector2.Dot(p - camera, line) / line.sqrMagnitude);
                if ((camera + line * t - p).magnitude > IntroCrownRadius) continue;
                if (height > Mathf.Lerp(d.introCameraHeight, 0f, t)) return true;
            }
            return false;
        }

        // ---------------------------------------------------------------- 草叢與道具（牆外）

        static float OuterBandStart(ArenaData d) => d.radius + d.wallThickness + 0.5f;

        static int BuildGrass(Transform root, ArenaData d, Terrain terrain, ArtLibrary library, System.Random random, Vector3 center,
                              List<Vector3> taken)
        {
            var meshes = new Dictionary<string, Mesh>();
            foreach (var (id, _) in Grass)
            {
                var model = library.Get(id);
                var filter = model != null ? model.GetComponentInChildren<MeshFilter>() : null;
                if (filter != null) meshes[id] = filter.sharedMesh;
            }
            if (meshes.Count == 0 || d.forestGrassDensity <= 0f) return 0;

            float inner = OuterBandStart(d), outer = d.farForest.y;
            int target = Mathf.RoundToInt(Mathf.PI * (outer * outer - inner * inner) * d.forestGrassDensity);
            var chunks = new Dictionary<Vector2Int, Dictionary<string, List<InstancedScatter.Instance>>>();
            int done = 0;
            for (int guard = 0; done < target && guard < target * 4; guard++)
            {
                // 一處 1–5 叢擠在一起才像一片草
                var c = PrologueSceneryBuilder.RingPoint(random, inner, outer);
                int cluster = 1 + random.Next(5);
                for (int k = 0; k < cluster && done < target; k++)
                {
                    var p = c + new Vector2(PrologueSceneryBuilder.Jitter(random), PrologueSceneryBuilder.Jitter(random)) * 0.5f;
                    if (p.magnitude < inner || PrologueSceneryBuilder.NearTrunk(p + new Vector2(center.x, center.z), taken, TrunkClearance)) continue;
                    string id = PrologueSceneryBuilder.Pick(Grass, random);
                    if (!meshes.ContainsKey(id)) continue;
                    var instance = new InstancedScatter.Instance
                    {
                        position = center + terrain.At(p),
                        yaw = (float)random.NextDouble() * 360f,
                        scale = Mathf.Lerp(0.8f, 1.3f, (float)random.NextDouble()),
                    };
                    var key = new Vector2Int(Mathf.FloorToInt(p.x / ChunkSize), Mathf.FloorToInt(p.y / ChunkSize));
                    if (!chunks.TryGetValue(key, out var byKind)) chunks[key] = byKind = new Dictionary<string, List<InstancedScatter.Instance>>();
                    if (!byKind.TryGetValue(id, out var list)) byKind[id] = list = new List<InstancedScatter.Instance>();
                    list.Add(instance);
                    done++;
                }
            }

            var parent = new GameObject("Grass").transform;
            parent.SetParent(root, false);
            var material = AssetDatabase.LoadAssetAtPath<Material>(ArtTools.FoliageMaterial);
            foreach (var pair in chunks.OrderBy(c => c.Key.x).ThenBy(c => c.Key.y))
            {
                var go = new GameObject($"Grass_{pair.Key.x}_{pair.Key.y}");
                go.transform.SetParent(parent, false);
                go.AddComponent<InstancedScatter>().SetData(material, pair.Value.OrderBy(k => k.Key)
                    .Select(k => new InstancedScatter.Kind { mesh = meshes[k.Key], instances = k.Value.ToArray() }).ToArray());
            }
            return done;
        }

        static int BuildProps(Transform root, ArenaData d, Terrain terrain, ArtLibrary library, System.Random random, Vector3 center,
                              List<Vector3> taken)
        {
            var parent = new GameObject("Props").transform;
            parent.SetParent(root, false);
            float inner = OuterBandStart(d) + 1f, outer = d.farForest.y;
            int placed = 0;
            foreach (var kind in Props)
            {
                int target = Mathf.RoundToInt(Mathf.PI * (outer * outer - inner * inner) * kind.density * d.propDensity);
                for (int done = 0, guard = 0; done < target && guard < target * 6; guard++)
                {
                    var p = PrologueSceneryBuilder.RingPoint(random, inner, outer);
                    if (PrologueSceneryBuilder.NearTrunk(p + new Vector2(center.x, center.z), taken, kind.clearance)) continue;
                    string id = kind.ids[random.Next(kind.ids.Length)];
                    var model = library.Get(id);
                    if (model == null) continue;
                    float scale = Mathf.Lerp(kind.scale.x, kind.scale.y, (float)random.NextDouble());
                    var position = center + terrain.At(p);
                    position.y -= model.GetComponentInChildren<MeshFilter>().sharedMesh.bounds.size.y * scale * kind.sink;
                    var holder = PrologueSceneryBuilder.Holder(parent, $"{id}_{placed:000}", position, (float)random.NextDouble() * 360f, scale);
                    ArtLibrary.Spawn(id, holder);
                    PrologueSceneryBuilder.MarkStatic(holder);
                    taken.Add(position);
                    placed++;
                    done++;
                }
            }
            return placed;
        }
    }
}
