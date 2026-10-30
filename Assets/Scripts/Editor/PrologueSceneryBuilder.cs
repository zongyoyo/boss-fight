using System;
using System.Collections.Generic;
using System.Linq;
using BossFight.Core;
using BossFight.Progression;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace BossFight.EditorTools
{
    /// <summary>
    /// 序章的佈景（docs/Art/ART_STYLE.md「序章」，美術 Phase 2）：地面、森林、草叢、牆外道具與遠山，全部照 PrologueLayoutData 與固定種子。
    /// PrologueBuilder.Build 會呼叫 Build；只改佈景時跑 Tools > Prologue > Build Scenery（在開著的序章場景原地重建 Level 底下這幾組物件，不動其他東西）。
    /// - 地面：Builder 產生的低面數網格（M_Palette 色格），牆內完全平坦（y = 0），牆外起伏、往遠山腳下升高；MeshCollider 就是這張網格。
    /// - 森林：牆內外的前 treeRows 排位置照舊（樹幹碰撞照舊），換成正式樹模型；牆外再種 outerTreeRows 排沒有碰撞的裝飾樹。
    /// - 草叢：空地中央 grassInnerRadius 內不放；同一區（ChunkSize 見方）的草由一個 Core/InstancedScatter 用 GPU Instancing 畫，
    ///   場景只存每一叢的位置、朝向與縮放（合併成網格的話文字資產有 20 多 MB）；不投影。
    /// - 道具（灌木、石頭、枯枝、樹樁、倒木）只放在牆外，沒有碰撞；空地邊緣只有碎石。
    /// 沒有正式模型時：樹退回佔位幾何、遠山退回圓錐，草與道具不放。
    /// </summary>
    public static class PrologueSceneryBuilder
    {
        const string SceneryDir = PrologueBuilder.ModelDir + "/Scenery";
        const string GroundMeshPath = SceneryDir + "/Ground.asset";  // 每次重建覆寫（保留 GUID）
        // 以下是純編輯器的散佈參數（視覺）：模型種類與權重、道具密度、合併草叢的區塊大小
        const float ChunkSize = 12f;
        const float GroundWarp = 8f;       // 牆外地面格距放大的快慢（m）：越小放大越快
        const float GroundFlatMargin = 2f; // 牆外多少 m 以內仍然平坦
        const float ReliefScale = 0.06f;   // 地面起伏的雜訊頻率（1/m）
        const float ColorScale = 0.12f;    // 地面色塊的雜訊頻率（1/m）
        const float TrunkClearance = 0.5f; // 草離樹幹至少這麼遠（m）
        const float EdgeTreeBand = 3f;     // 牆內緣往外這麼遠以內的樹只用樹冠高的種類（m）

        static readonly (string id, float weight)[] TreeKinds =
        {
            ("SM_Tree_Pine_A", 0.22f), ("SM_Tree_Pine_B", 0.18f), ("SM_Tree_Pine_C", 0.15f), ("SM_Tree_Pine_D", 0.05f),
            ("SM_Tree_Broad_A", 0.13f), ("SM_Tree_Broad_B", 0.1f), ("SM_Tree_Broad_C", 0.12f),
            ("SM_Tree_Dead_A", 0.03f), ("SM_Tree_Dead_B", 0.02f),
        };
        // 靠近空地的樹：隱形牆把鏡頭擋在牆內緣，針葉樹的低枝片會把鏡頭包住，所以只用樹冠在 3.5 m 以上的闊葉樹與枯樹
        static readonly (string id, float weight)[] EdgeTreeKinds =
            { ("SM_Tree_Broad_A", 0.4f), ("SM_Tree_Broad_C", 0.45f), ("SM_Tree_Dead_A", 0.15f) };
        static readonly (string id, float weight)[] EdgeGrass =
            { ("SM_Grass_Clump_B", 0.6f), ("SM_Grass_Clump_A", 0.3f), ("SM_Grass_Clump_D", 0.1f) };
        static readonly (string id, float weight)[] ForestGrass =
            { ("SM_Grass_Clump_A", 0.35f), ("SM_Grass_Clump_C", 0.25f), ("SM_Grass_Clump_B", 0.25f), ("SM_Grass_Clump_D", 0.15f) };

        struct PropKind
        {
            public string[] ids;
            public float density;     // 個 / m²（乘上 propDensity）
            public Vector2 scale;
            public float sink;        // 往地裡埋的比例（石頭在斜坡上不浮空）
            public float clearance;   // 離樹幹至少多遠（m）
        }

        static readonly PropKind[] ForestProps =
        {
            new PropKind { ids = new[] { "SM_Bush_A", "SM_Bush_B", "SM_Bush_C" }, density = 0.025f, scale = new Vector2(0.8f, 1.3f), clearance = 0.6f },
            new PropKind { ids = new[] { "SM_Rock_A", "SM_Rock_B" }, density = 0.008f, scale = new Vector2(0.7f, 1.3f), sink = 0.12f, clearance = 0.8f },
            new PropKind { ids = new[] { "SM_Rock_E", "SM_Rock_F" }, density = 0.0025f, scale = new Vector2(0.8f, 1.2f), sink = 0.12f, clearance = 1.6f },
            new PropKind { ids = new[] { "SM_Rock_C", "SM_Rock_D" }, density = 0.012f, scale = new Vector2(0.8f, 1.5f), sink = 0.1f, clearance = 0.3f },
            new PropKind { ids = new[] { "SM_Twig_A", "SM_Twig_B", "SM_Twig_C" }, density = 0.012f, scale = new Vector2(0.8f, 1.3f), clearance = 0.3f },
            new PropKind { ids = new[] { "SM_Stump_A", "SM_Stump_B" }, density = 0.003f, scale = new Vector2(0.8f, 1.2f), clearance = 1f },
            new PropKind { ids = new[] { "SM_Log_A", "SM_Log_B" }, density = 0.003f, scale = new Vector2(0.9f, 1.2f), clearance = 1.4f },
        };
        static readonly PropKind[] EdgeProps =
        {
            new PropKind { ids = new[] { "SM_Rock_C", "SM_Rock_D" }, density = 0.01f, scale = new Vector2(0.7f, 1.2f), sink = 0.1f, clearance = 0.3f },
        };
        static readonly string[] MountainKinds = { "SM_Mountain_A", "SM_Mountain_B", "SM_Mountain_C" };

        /// <summary>佔位幾何的材質（沒有正式模型時用）；由 PrologueBuilder 準備。</summary>
        public struct Fallback
        {
            public Material grass, bark, mountain;
            public Material[] leaves;
        }

        [MenuItem("Tools/Prologue/Build Scenery")]
        public static void BuildFromMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var scene = SceneManager.GetActiveScene();
            if (scene.path != PrologueBuilder.ScenePath)
            {
                Debug.LogWarning("[Prologue] 請先開啟 Prologue 場景。");
                return;
            }
            var level = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Level");
            if (level == null)
            {
                Debug.LogWarning("[Prologue] 找不到 Level，先跑 Tools > Prologue > Build Prologue。");
                return;
            }
            var data = AssetDatabase.LoadAssetAtPath<PrologueLayoutData>(PrologueBuilder.LayoutPath);
            Debug.Log(Build(level.transform, data, PrologueBuilder.FallbackMaterials()));
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        /// <summary>在 level 底下重建 Ground、Forest、Vegetation、Mountains（同名的舊物件先刪掉）；回傳摘要。</summary>
        public static string Build(Transform level, PrologueLayoutData data, Fallback fallback)
        {
            foreach (var name in new[] { "Ground", "Forest", "Vegetation", "Mountains" })
            {
                var old = level.Find(name);
                if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
            }
            DaoDunDogBuilder.EnsureFolder(SceneryDir);
            var library = ArtLibrary.Load();
            var terrain = new Terrain(data);
            var random = new System.Random(data.visualSeed);

            BuildGround(level, data, terrain, fallback);
            var trunks = new List<Vector3>();
            int trees = BuildForest(level, data, terrain, library, fallback, random, trunks);
            var vegetation = new GameObject("Vegetation").transform;
            vegetation.SetParent(level, false);
            var keepOut = KeepOut(data);
            int clumps = BuildGrass(vegetation, data, terrain, library, random, trunks, keepOut);
            int props = BuildProps(vegetation, data, terrain, library, random, trunks, keepOut);
            BuildMountains(level, data, terrain, library, fallback, random);
            AssetDatabase.SaveAssets();
            return $"[Prologue] 佈景：{trees} 棵樹、{clumps} 叢草、{props} 個道具、{data.mountainCount} 座遠山。";
        }

        // ---------------------------------------------------------------- 地形

        /// <summary>地面的高度：牆內與牆外 GroundFlatMargin 以內是 0，之後起伏、往外升高。樹、草、道具、遠山都放在這個高度。</summary>
        class Terrain
        {
            readonly float flat, outer, relief, rise;
            readonly Vector2 offset;

            public Terrain(PrologueLayoutData data)
            {
                flat = WallOuterRadius(data) + GroundFlatMargin;
                outer = data.groundRadius;
                relief = data.groundRelief;
                rise = data.groundRise;
                var r = new System.Random(data.visualSeed + 1);
                offset = new Vector2((float)r.NextDouble() * 100f, (float)r.NextDouble() * 100f);
            }

            public float FlatRadius => flat;

            public float Height(float x, float z)
            {
                float r = Mathf.Sqrt(x * x + z * z);
                if (r <= flat) return 0f;
                float bumps = Mathf.SmoothStep(0f, 1f, (r - flat) / 8f) *
                              (Mathf.PerlinNoise(x * ReliefScale + offset.x, z * ReliefScale + offset.y) - 0.35f) * relief;
                float t = Mathf.Clamp01((r - flat - 15f) / Mathf.Max(outer - flat - 15f, 1f));
                return Mathf.Max(bumps, -0.2f) + rise * t * t;
            }

            public Vector3 At(Vector2 p) => new Vector3(p.x, Height(p.x, p.y), p.y);

            public float Color(Vector2 p, float scale, float seed) =>
                Mathf.PerlinNoise(p.x * scale + offset.x + seed, p.y * scale + offset.y + seed);
        }

        static float WallOuterRadius(PrologueLayoutData data) => data.clearingRadius + data.wallOffset + data.wallThickness;
        static float WallInnerRadius(PrologueLayoutData data) => data.clearingRadius + data.wallOffset;

        static void BuildGround(Transform level, PrologueLayoutData data, Terrain terrain, Fallback fallback)
        {
            // 三角格子（列與列錯開半格），牆內格距固定，牆外依 GroundWarp 往外拉大：f(d) = r0 + (d - r0)(1 + (d - r0) / L)
            float s = data.groundCellSize, r0 = terrain.FlatRadius, L = GroundWarp;
            float outer = Mathf.Max(data.groundRadius, r0 + s);
            float maxD = r0 + (-1f + Mathf.Sqrt(1f + 4f * (outer - r0) / L)) * L * 0.5f;
            float rowH = s * Mathf.Sqrt(3f) * 0.5f;
            int nx = Mathf.CeilToInt(maxD / s) + 1, nz = Mathf.CeilToInt(maxD / rowH) + 1;
            var random = new System.Random(data.visualSeed + 2);
            var points = new Dictionary<(int, int), Vector3>();
            for (int j = -nz; j <= nz; j++)
            for (int i = -nx; i <= nx; i++)
            {
                var p = new Vector2((i + ((j & 1) != 0 ? 0.5f : 0f)) * s, j * rowH);
                p += new Vector2((float)random.NextDouble() - 0.5f, (float)random.NextDouble() - 0.5f) * s * 0.4f;
                float d = p.magnitude;
                if (d > maxD) continue;
                if (d > r0) p *= (r0 + (d - r0) * (1f + (d - r0) / L)) / d;
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
                var uv = ArtTools.PaletteUV(GroundCell(data, terrain, (pa + pb + pc) / 3f, n));
                // Unity 的面法線是 Cross(b − a, c − a)，朝上的面才是正面
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
            mesh = SaveMesh(mesh, GroundMeshPath);

            var ground = new GameObject("Ground", typeof(MeshFilter), typeof(MeshRenderer));
            ground.transform.SetParent(level, false);
            ground.GetComponent<MeshFilter>().sharedMesh = mesh;
            var palette = AssetDatabase.LoadAssetAtPath<Material>(ArtTools.PaletteMaterial);
            ground.GetComponent<MeshRenderer>().sharedMaterial = palette != null ? palette : fallback.grass;
            ground.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            // 牆內的地面就是 y = 0 的平面，碰撞直接用這張網格
            ground.AddComponent<MeshCollider>().sharedMesh = mesh;
            ground.isStatic = true;
        }

        /// <summary>地面一個三角面的色格：菊花周圍是泥土，空地是草綠的亮、中、暗色塊，牆外林地偏暗，陡坡用暗階。</summary>
        static string GroundCell(PrologueLayoutData data, Terrain terrain, Vector3 center, Vector3 normal)
        {
            var p = new Vector2(center.x, center.z);
            float r = p.magnitude;
            float n = terrain.Color(p, ColorScale, 0f);
            float n2 = terrain.Color(p, ColorScale * 1.7f, 37f);
            if (Vector2.Distance(p, data.mentorPosition) < data.earthRadius * (0.75f + 0.5f * n2)) return "earth.m";
            if (normal.y < 0.92f) return "grass.d";
            if (r < data.clearingRadius) return n > 0.62f ? "grass.l" : n < 0.3f ? "grass.d" : "grass.m";
            if (r < terrain.FlatRadius + 20f)
                return n < 0.3f ? "moss.d" : n < 0.6f ? "grass.d" : "grass.m";
            return n < 0.4f ? "moss.m" : "grass.m";
        }

        // ---------------------------------------------------------------- 森林

        static int BuildForest(Transform level, PrologueLayoutData data, Terrain terrain, ArtLibrary library, Fallback fallback,
                               System.Random random, List<Vector3> trunks)
        {
            var root = new GameObject("Forest").transform;
            root.SetParent(level, false);
            int count = 0;
            // 前 treeRows 排：位置照佔位版，樹幹有碰撞（第一排在牆內，玩家碰得到）
            float radius = data.clearingRadius + data.trunkRadius * 2f;
            for (int row = 0; row < data.treeRows; row++, radius += data.rowSpacing)
            {
                int n = Mathf.CeilToInt(Mathf.PI * 2f * radius / data.treeSpacing);
                for (int i = 0; i < n; i++, count++)
                {
                    float angle = (i + 0.5f + row * 0.5f) / n * Mathf.PI * 2f;
                    var position = terrain.At(new Vector2(Mathf.Sin(angle), Mathf.Cos(angle)) * radius);
                    var kinds = radius < WallInnerRadius(data) + EdgeTreeBand ? EdgeTreeKinds : TreeKinds;
                    Tree(root, $"Tree_{row}_{i:00}", position, kinds, data, library, fallback, random, true);
                    trunks.Add(position);
                }
            }
            // 牆外的裝飾樹：沒有碰撞，位置隨機偏移
            radius += data.outerRowSpacing - data.rowSpacing;
            for (int row = 0; row < data.outerTreeRows; row++, radius += data.outerRowSpacing)
            {
                int n = Mathf.CeilToInt(Mathf.PI * 2f * radius / data.outerTreeSpacing);
                float phase = (float)random.NextDouble();
                for (int i = 0; i < n; i++, count++)
                {
                    float angle = (i + phase) / n * Mathf.PI * 2f + Jitter(random) * data.outerTreeJitter / radius;
                    float r = radius + Jitter(random) * data.outerTreeJitter;
                    var position = terrain.At(new Vector2(Mathf.Sin(angle), Mathf.Cos(angle)) * r);
                    Tree(root, $"Outer_{row}_{i:00}", position, TreeKinds, data, library, fallback, random, false);
                    trunks.Add(position);
                }
            }
            return count;
        }

        static void Tree(Transform root, string name, Vector3 position, (string id, float weight)[] kinds, PrologueLayoutData data,
                         ArtLibrary library, Fallback fallback, System.Random random, bool collide)
        {
            string id = Pick(kinds, random);
            float scale = Mathf.Lerp(data.treeScale.x, data.treeScale.y, (float)random.NextDouble());
            float yaw = (float)random.NextDouble() * 360f;
            if (library == null || library.Get(id) == null)
            {
                // 佔位：樹幹圓柱＋樹冠球（Phase 2 以前的樣子）
                float height = Mathf.Lerp(data.treeHeight.x, data.treeHeight.y, (float)random.NextDouble());
                var tree = new GameObject(name).transform;
                tree.SetParent(root, false);
                tree.position = position;
                Primitive("Trunk", tree, PrimitiveType.Cylinder, Vector3.up * height * 0.5f,
                          new Vector3(data.trunkRadius * 2f, height * 0.5f, data.trunkRadius * 2f), fallback.bark, collide);
                Primitive("Crown", tree, PrimitiveType.Sphere, Vector3.up * height * 0.8f,
                          new Vector3(3.8f, height * 0.65f, 3.8f), fallback.leaves[random.Next(fallback.leaves.Length)], false);
                return;
            }
            var holder = Holder(root, name, position, yaw, scale);
            ArtLibrary.Spawn(id, holder);
            if (collide)
            {
                // 樹幹碰撞照佔位樹（半徑 trunkRadius），高度只要擋得住玩家
                var capsule = holder.gameObject.AddComponent<CapsuleCollider>();
                capsule.radius = data.trunkRadius / scale;
                capsule.height = data.wallHeight / scale;
                capsule.center = Vector3.up * capsule.height * 0.5f;
            }
            MarkStatic(holder);
        }

        // ---------------------------------------------------------------- 草叢

        /// <summary>出生點與教學角色周圍（不放草與道具的圓）。</summary>
        static List<Vector2> KeepOut(PrologueLayoutData data) =>
            new List<Vector2> { data.playerSpawn, data.mentorPosition, data.saplingPosition, data.guardianPosition };

        static int BuildGrass(Transform parent, PrologueLayoutData data, Terrain terrain, ArtLibrary library, System.Random random,
                              List<Vector3> trunks, List<Vector2> keepOut)
        {
            var meshes = new Dictionary<string, Mesh>();
            foreach (var (id, _) in EdgeGrass.Concat(ForestGrass))
            {
                var model = library != null ? library.Get(id) : null;
                var filter = model != null ? model.GetComponentInChildren<MeshFilter>() : null;
                if (filter != null) meshes[id] = filter.sharedMesh;
            }
            if (meshes.Count == 0) return 0;

            // 區塊 → 種類 → 這一區的草叢
            var chunks = new Dictionary<Vector2Int, Dictionary<string, List<InstancedScatter.Instance>>>();
            int placed = 0;
            float wallInner = WallInnerRadius(data);
            float outer = ForestOuterRadius(data);
            placed += Scatter(data.grassInnerRadius, wallInner, data.edgeGrassDensity, EdgeGrass);
            placed += Scatter(wallInner, outer, data.forestGrassDensity, ForestGrass);

            int Scatter(float inner, float outerRadius, float density, (string id, float weight)[] kinds)
            {
                if (outerRadius <= inner || density <= 0f) return 0;
                int target = Mathf.RoundToInt(Mathf.PI * (outerRadius * outerRadius - inner * inner) * density);
                int done = 0;
                for (int guard = 0; done < target && guard < target * 4; guard++)
                {
                    // 一處 1–5 叢擠在一起才像一片草；中心在圓環上均勻分布
                    var center = RingPoint(random, inner, outerRadius);
                    int cluster = 1 + random.Next(5);
                    for (int k = 0; k < cluster && done < target; k++)
                    {
                        var p = center + new Vector2(Jitter(random), Jitter(random)) * 0.5f;
                        float r = p.magnitude;
                        if (r < inner || r > outerRadius || Near(p, keepOut, data.actorClearance) || NearTrunk(p, trunks, TrunkClearance)) continue;
                        string id = Pick(kinds, random);
                        if (!meshes.ContainsKey(id)) continue;
                        var instance = new InstancedScatter.Instance
                        {
                            position = terrain.At(p),
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
                return done;
            }

            var root = new GameObject("Grass").transform;
            root.SetParent(parent, false);
            var material = AssetDatabase.LoadAssetAtPath<Material>(ArtTools.FoliageMaterial);
            foreach (var pair in chunks.OrderBy(c => c.Key.x).ThenBy(c => c.Key.y))
            {
                var go = new GameObject($"Grass_{pair.Key.x}_{pair.Key.y}");
                go.transform.SetParent(root, false);
                go.AddComponent<InstancedScatter>().SetData(material, pair.Value.OrderBy(k => k.Key)
                    .Select(k => new InstancedScatter.Kind { mesh = meshes[k.Key], instances = k.Value.ToArray() }).ToArray());
            }
            return placed;
        }

        /// <summary>林帶（草與道具）往外放到最外排裝飾樹後面一點。</summary>
        static float ForestOuterRadius(PrologueLayoutData data) =>
            data.clearingRadius + data.trunkRadius * 2f + (data.treeRows - 1) * data.rowSpacing + data.outerTreeRows * data.outerRowSpacing + 2f;

        // ---------------------------------------------------------------- 道具

        static int BuildProps(Transform parent, PrologueLayoutData data, Terrain terrain, ArtLibrary library, System.Random random,
                              List<Vector3> trunks, List<Vector2> keepOut)
        {
            var root = new GameObject("Props").transform;
            root.SetParent(parent, false);
            int placed = 0;
            float wallInner = WallInnerRadius(data);
            float outer = ForestOuterRadius(data);
            var taken = new List<Vector3>(trunks);
            foreach (var kind in EdgeProps) placed += Place(kind, data.grassInnerRadius, wallInner);
            // 牆外：大的先放，小的再填空隙
            foreach (var kind in ForestProps.OrderByDescending(k => k.clearance)) placed += Place(kind, WallOuterRadius(data), outer);
            return placed;

            int Place(PropKind kind, float inner, float outerRadius)
            {
                int target = Mathf.RoundToInt(Mathf.PI * (outerRadius * outerRadius - inner * inner) * kind.density * data.propDensity);
                int done = 0;
                for (int guard = 0; done < target && guard < target * 6; guard++)
                {
                    var p = RingPoint(random, inner, outerRadius);
                    if (Near(p, keepOut, data.actorClearance) || NearTrunk(p, taken, kind.clearance)) continue;
                    string id = kind.ids[random.Next(kind.ids.Length)];
                    if (library == null || library.Get(id) == null) continue;
                    float scale = Mathf.Lerp(kind.scale.x, kind.scale.y, (float)random.NextDouble());
                    var position = terrain.At(p);
                    var bounds = library.Get(id).GetComponentInChildren<MeshFilter>().sharedMesh.bounds;
                    position.y -= bounds.size.y * scale * kind.sink;
                    var holder = Holder(root, $"{id}_{done:000}", position, (float)random.NextDouble() * 360f, scale);
                    ArtLibrary.Spawn(id, holder);
                    MarkStatic(holder);
                    taken.Add(position);
                    done++;
                }
                return done;
            }
        }

        // ---------------------------------------------------------------- 遠山

        static void BuildMountains(Transform level, PrologueLayoutData data, Terrain terrain, ArtLibrary library, Fallback fallback,
                                   System.Random random)
        {
            var root = new GameObject("Mountains").transform;
            root.SetParent(level, false);
            for (int i = 0; i < data.mountainCount; i++)
            {
                float a = (i + Jitter(random) * 0.3f) * Mathf.PI * 2f / data.mountainCount;
                float r = data.mountainRadius * (1f + Jitter(random) * 0.12f);
                var p = new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * r;
                string id = MountainKinds[random.Next(MountainKinds.Length)];
                if (library == null || library.Get(id) == null)
                {
                    var cone = new GameObject($"Mountain_{i:00}", typeof(MeshFilter), typeof(MeshRenderer));
                    cone.transform.SetParent(root, false);
                    cone.transform.position = new Vector3(p.x, -1f, p.y);
                    cone.transform.localScale = new Vector3(17f, 14f + (float)random.NextDouble() * 14f, 17f);
                    cone.GetComponent<MeshFilter>().sharedMesh = PrologueBuilder.MountainCone();
                    cone.GetComponent<MeshRenderer>().sharedMaterial = fallback.mountain;
                    cone.isStatic = true;
                    continue;
                }
                // 模型的 X 是山脈的走向：轉成沿著圓周（正面朝場地中心）
                var holder = Holder(root, $"Mountain_{i:00}", terrain.At(p) + Vector3.down * 1.5f,
                                    Mathf.Atan2(-p.x, -p.y) * Mathf.Rad2Deg, 1f);
                float scale = Mathf.Lerp(0.8f, 1.15f, (float)random.NextDouble());
                holder.localScale = new Vector3(scale, scale * Mathf.Lerp(0.85f, 1.3f, (float)random.NextDouble()), scale);
                ArtLibrary.Spawn(id, holder);
                MarkStatic(holder);
            }
        }

        // ---------------------------------------------------------------- 共用

        static Mesh SaveMesh(Mesh mesh, string path)
        {
            mesh.name = System.IO.Path.GetFileNameWithoutExtension(path); // 主物件名稱要和檔名相同
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(mesh, path);
                return mesh;
            }
            // 保留 GUID：把新資料複製進舊資產
            existing.Clear();
            EditorUtility.CopySerialized(mesh, existing);
            UnityEngine.Object.DestroyImmediate(mesh);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        static string Pick((string id, float weight)[] kinds, System.Random random)
        {
            float total = kinds.Sum(k => k.weight), x = (float)random.NextDouble() * total;
            foreach (var (id, weight) in kinds)
            {
                if (x < weight) return id;
                x -= weight;
            }
            return kinds[kinds.Length - 1].id;
        }

        static float Jitter(System.Random random) => (float)random.NextDouble() * 2f - 1f;

        static Vector2 RingPoint(System.Random random, float inner, float outer)
        {
            float r = Mathf.Sqrt(Mathf.Lerp(inner * inner, outer * outer, (float)random.NextDouble()));
            float a = (float)random.NextDouble() * Mathf.PI * 2f;
            return new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * r;
        }

        static bool Near(Vector2 p, List<Vector2> points, float radius) => points.Any(q => (p - q).sqrMagnitude < radius * radius);

        static bool NearTrunk(Vector2 p, List<Vector3> trunks, float radius)
        {
            foreach (var t in trunks)
                if ((p.x - t.x) * (p.x - t.x) + (p.y - t.z) * (p.y - t.z) < radius * radius) return true;
            return false;
        }

        static Transform Holder(Transform parent, string name, Vector3 position, float yaw, float scale)
        {
            var holder = new GameObject(name).transform;
            holder.SetParent(parent, false);
            holder.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            holder.localScale = Vector3.one * scale;
            return holder;
        }

        static void MarkStatic(Transform root)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.isStatic = true;
        }

        static void Primitive(string name, Transform parent, PrimitiveType type, Vector3 localPosition, Vector3 scale, Material material, bool collide)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = material;
            if (!collide) UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            go.isStatic = true;
        }
    }
}
