using System.Collections.Generic;
using BossFight.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace BossFight.EditorTools
{
    [CustomEditor(typeof(ArenaGenerator))]
    public class ArenaGeneratorEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var g = (ArenaGenerator)target;
            EditorGUILayout.Space();
            if (g.data == null)
            {
                EditorGUILayout.HelpBox("請指定 ArenaData。", MessageType.Warning);
                return;
            }

            var d = g.data;
            EditorGUILayout.HelpBox(
                $"競技區半徑 {d.radius:0.#} m（直徑 {d.radius * 2f:0.#} m），外牆高 {d.wallHeight:0.#} m\n" +
                $"含觀眾席外徑 {d.RimOuterRadius * 2f:0.#} m，最外圈牆頂高 {d.RimTopHeight:0.#} m",
                MessageType.Info);

            if (GUILayout.Button("重建競技場", GUILayout.Height(32)))
                ArenaBuilder.Build(g);
        }
    }

    public static class ArenaBuilder
    {
        const string ModelDir = "Assets/Models/Arena";
        const string MatDir = ModelDir + "/Materials";
        const string MeshDir = ModelDir + "/Meshes";
        const string DefaultDataPath = "Assets/ScriptableObjects/Arena/ArenaData.asset";
        const string GeneratedName = "Generated";

        const StaticEditorFlags StaticFlags =
            StaticEditorFlags.ContributeGI |
            StaticEditorFlags.OccluderStatic |
            StaticEditorFlags.OccludeeStatic |
            StaticEditorFlags.BatchingStatic |
            StaticEditorFlags.ReflectionProbeStatic;

        [MenuItem("Tools/Arena/Build Arena")]
        public static void BuildFromMenu()
        {
            var g = Object.FindAnyObjectByType<ArenaGenerator>();
            if (g == null)
            {
                var go = new GameObject("Arena");
                Undo.RegisterCreatedObjectUndo(go, "Create Arena");
                g = go.AddComponent<ArenaGenerator>();
            }
            if (g.data == null)
            {
                Undo.RecordObject(g, "Assign ArenaData");
                g.data = AssetDatabase.LoadAssetAtPath<ArenaData>(DefaultDataPath);
            }
            Build(g);
            Selection.activeGameObject = g.gameObject;
        }

        public static void Build(ArenaGenerator g)
        {
            var d = g.data;
            if (d == null)
            {
                Debug.LogError($"[Arena] ArenaGenerator 沒有指定 ArenaData（預設在 {DefaultDataPath}）。", g);
                return;
            }

            int wallLayer = d.WallLayerIndex;
            if (wallLayer < 0)
                Debug.LogWarning($"[Arena] 找不到 Layer「{d.wallLayer}」，外牆會留在 Default。請到 Project Settings > Tags and Layers 新增。");

            EnsureFolder(MatDir);
            EnsureFolder(MeshDir);

            var sand = GetMat("Arena_Sand", new Color(0.78f, 0.68f, 0.50f));
            var stone = GetMat("Arena_Stone", new Color(0.58f, 0.55f, 0.50f));
            var stoneDark = GetMat("Arena_StoneDark", new Color(0.42f, 0.40f, 0.37f));
            var gateMat = GetMat("Arena_Gate", new Color(0.24f, 0.19f, 0.15f));
            var groundMat = GetMat("Arena_Ground", new Color(0.36f, 0.38f, 0.30f));
            var refMat = GetMat("Arena_ScaleRef", new Color(0.85f, 0.25f, 0.20f));

            Undo.SetCurrentGroupName("Rebuild Arena");
            int undoGroup = Undo.GetCurrentGroup();

            var old = g.transform.Find(GeneratedName);
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

            var root = new GameObject(GeneratedName);
            Undo.RegisterCreatedObjectUndo(root, "Rebuild Arena");
            root.transform.SetParent(g.transform, false);

            float R = d.radius;
            float wallOut = d.StandsInnerRadius;
            float h = d.wallHeight;
            int seg = d.segments;
            float gateW = Mathf.Min(d.gateWidth, R);
            bool hasStands = d.standTiers > 0;

            // 1. 沙地（延伸到圍牆底下）
            var mb = new MeshBuilder();
            mb.Disc(wallOut, 0f, seg);
            AddMeshObject(root, "Floor", mb, "Arena_Floor", sand);

            // 2. 競技區圍牆，入口處留缺口；Boss 撞牆判定靠它的 Layer
            mb = new MeshBuilder();
            foreach (var arc in WallArcs(d, R, gateW))
            {
                int s = Mathf.Max(1, Mathf.CeilToInt(seg * (arc.y - arc.x) / 360f));
                mb.TubeArc(R, wallOut, 0f, 0f, h, arc.x, arc.y, s,
                    inner: true, outer: !hasStands, top: true, caps: true);
            }
            var podiumWall = AddMeshObject(root, "PodiumWall", mb, "Arena_PodiumWall", stone);
            SetLayerRecursive(podiumWall, wallLayer);

            // 3. 觀眾席（階梯），從圍牆頂往外往上
            if (hasStands)
            {
                mb = new MeshBuilder();
                float prevTop = h;
                for (int i = 0; i < d.standTiers; i++)
                {
                    float rIn = wallOut + i * d.tierDepth;
                    float top = h + (i + 1) * d.tierRise;
                    mb.TubeArc(rIn, rIn + d.tierDepth, prevTop, 0f, top, 0f, 360f, seg,
                        inner: true, outer: false, top: true, caps: false);
                    prevTop = top;
                }
                AddMeshObject(root, "Stands", mb, "Arena_Stands", stoneDark);
            }

            // 4. 最外圈外牆
            mb = new MeshBuilder();
            mb.TubeArc(d.StandsOuterRadius, d.RimOuterRadius, d.StandsTopHeight, 0f, d.RimTopHeight, 0f, 360f, seg,
                inner: true, outer: true, top: true, caps: false);
            AddMeshObject(root, "RimWall", mb, "Arena_RimWall", stone);

            // 5. 外圍地面
            if (d.outerGroundMargin > 0f)
            {
                mb = new MeshBuilder();
                mb.RingTop(d.RimOuterRadius, d.RimOuterRadius + d.outerGroundMargin, 0f, seg);
                AddMeshObject(root, "OuterGround", mb, "Arena_OuterGround", groundMat);
            }

            // 6. 入口（門柱 + 關閉的門），跟圍牆同一個 Layer
            var gatesRoot = NewChild(root, "Gates");
            if (d.gates != null)
            {
                foreach (var gate in d.gates)
                {
                    var gateGo = BuildGate(gatesRoot, gate, R, d.wallThickness, h, gateW, stone, gateMat);
                    SetLayerRecursive(gateGo, wallLayer);
                }
            }

            // 7. 出生點：玩家在 PlayerGate 內側，Boss 在場中央面向玩家
            float playerAngle = 180f;
            if (d.TryGetGate("PlayerGate", out var pg)) playerAngle = pg.angle;
            else if (d.gates != null && d.gates.Length > 0) playerAngle = d.gates[0].angle;

            var spawns = NewChild(root, "Spawns");
            var playerDir = ArenaData.Dir(playerAngle);

            var ps = NewChild(spawns, "PlayerSpawn");
            ps.transform.localPosition = playerDir * Mathf.Clamp(d.playerSpawnDistance, 0f, R);
            ps.transform.localRotation = Quaternion.LookRotation(-playerDir);
            SetIcon(ps, "sv_label_3");

            var bs = NewChild(spawns, "BossSpawn");
            bs.transform.localPosition = Vector3.zero;
            bs.transform.localRotation = Quaternion.LookRotation(playerDir);
            SetIcon(bs, "sv_label_6");

            // 8. 比例參考：2 m 高膠囊，站在玩家出生點旁邊（不跟角色重疊），只在編輯器裡存在
            var refCap = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            refCap.name = "ScaleRef_2m";
            refCap.tag = "EditorOnly";
            Object.DestroyImmediate(refCap.GetComponent<Collider>());
            refCap.transform.SetParent(spawns.transform, false);
            Vector3 side = Vector3.Cross(Vector3.up, -playerDir);
            refCap.transform.localPosition = ps.transform.localPosition + side * 1.5f + Vector3.up;
            refCap.GetComponent<MeshRenderer>().sharedMaterial = refMat;

            Undo.CollapseUndoOperations(undoGroup);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(g.gameObject.scene);

            Debug.Log($"[Arena] 重建完成：競技區半徑 {R:0.#} m，外牆高 {h:0.#} m，" +
                      $"含觀眾席外徑 {d.RimOuterRadius * 2f:0.#} m");
        }

        // ---------- 組件 ----------

        static GameObject BuildGate(GameObject parent, ArenaData.Gate gate, float R, float thick, float h, float w,
                                    Material frameMat, Material doorMat)
        {
            var dir = ArenaData.Dir(gate.angle);
            var go = NewChild(parent, string.IsNullOrEmpty(gate.name) ? "Gate" : gate.name);
            // 放在圍牆內側線上，+Z 朝向場中央
            go.transform.localPosition = dir * R;
            go.transform.localRotation = Quaternion.LookRotation(-dir);

            float doorDepth = Mathf.Max(0.1f, thick - 0.3f);
            Cube(go, "Door", new Vector3(0f, h * 0.5f, -(0.3f + thick) * 0.5f),
                 new Vector3(w, h, doorDepth), doorMat);

            float pillarH = h + 1.2f;
            Vector3 pillarScale = new Vector3(1.2f, pillarH, thick + 0.8f);
            float px = w * 0.5f + 0.6f;
            Cube(go, "Pillar_L", new Vector3(-px, pillarH * 0.5f, -thick * 0.5f), pillarScale, frameMat);
            Cube(go, "Pillar_R", new Vector3(px, pillarH * 0.5f, -thick * 0.5f), pillarScale, frameMat);
            return go;
        }

        static List<Vector2> WallArcs(ArenaData d, float R, float gateW)
        {
            var gaps = new List<Vector2>();
            float half = Mathf.Asin(Mathf.Clamp01(gateW * 0.5f / R)) * Mathf.Rad2Deg;
            if (d.gates != null)
            {
                foreach (var gate in d.gates)
                {
                    float a = Mathf.Repeat(gate.angle, 360f);
                    gaps.Add(new Vector2(a - half, a + half));
                }
            }

            var arcs = new List<Vector2>();
            if (gaps.Count == 0)
            {
                arcs.Add(new Vector2(0f, 360f));
                return arcs;
            }

            gaps.Sort((p, q) => p.x.CompareTo(q.x));
            for (int i = 0; i < gaps.Count; i++)
            {
                float start = gaps[i].y;
                float end = i + 1 < gaps.Count ? gaps[i + 1].x : gaps[0].x + 360f;
                if (end - start > 0.01f) arcs.Add(new Vector2(start, end));
            }
            return arcs;
        }

        // ---------- 工具 ----------

        static GameObject AddMeshObject(GameObject parent, string name, MeshBuilder mb, string meshAsset, Material mat)
        {
            var mesh = GetMesh(meshAsset);
            mb.WriteTo(mesh);
            EditorUtility.SetDirty(mesh);

            var go = NewChild(parent, name);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
            GameObjectUtility.SetStaticEditorFlags(go, StaticFlags);
            return go;
        }

        static GameObject Cube(GameObject parent, string name, Vector3 localPos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            GameObjectUtility.SetStaticEditorFlags(go, StaticFlags);
            return go;
        }

        static void SetLayerRecursive(GameObject go, int layer)
        {
            if (layer < 0) return;
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
                t.gameObject.layer = layer;
        }

        static GameObject NewChild(GameObject parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            return go;
        }

        static void SetIcon(GameObject go, string iconName)
        {
            var tex = EditorGUIUtility.IconContent(iconName).image as Texture2D;
            if (tex != null) EditorGUIUtility.SetIconForObject(go, tex);
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            string parent = path.Substring(0, slash);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(slash + 1));
        }

        static Mesh GetMesh(string name)
        {
            string path = $"{MeshDir}/{name}.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null)
            {
                mesh = new Mesh { name = name };
                AssetDatabase.CreateAsset(mesh, path);
            }
            return mesh;
        }

        /// 材質只在不存在時建立，之後你在 Inspector 改顏色不會被重建覆蓋。
        static Material GetMat(string name, Color color)
        {
            string path = $"{MatDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            mat = new Material(shader) { name = name };
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.1f);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        // ---------- 網格產生 ----------

        class MeshBuilder
        {
            const float Tile = 4f; // 每 4 m 重複一次貼圖

            readonly List<Vector3> verts = new List<Vector3>();
            readonly List<Vector3> normals = new List<Vector3>();
            readonly List<Vector2> uvs = new List<Vector2>();
            readonly List<int> tris = new List<int>();

            static Vector3 P(float r, float a, float y) => ArenaData.Dir(a) * r + Vector3.up * y;

            static Vector3 Tangent(float a)
            {
                float rad = a * Mathf.Deg2Rad;
                return new Vector3(Mathf.Cos(rad), 0f, -Mathf.Sin(rad));
            }

            static Vector2 FlatUV(Vector3 p) => new Vector2(p.x, p.z) / Tile;
            static Vector2 WallUV(float r, float a, float y) => new Vector2(a * Mathf.Deg2Rad * r, y) / Tile;

            // a,b,c,d 以「從正面看順時針」排列
            void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d,
                      Vector3 na, Vector3 nb, Vector3 nc, Vector3 nd,
                      Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud)
            {
                int i = verts.Count;
                verts.Add(a); verts.Add(b); verts.Add(c); verts.Add(d);
                normals.Add(na); normals.Add(nb); normals.Add(nc); normals.Add(nd);
                uvs.Add(ua); uvs.Add(ub); uvs.Add(uc); uvs.Add(ud);
                tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
                tris.Add(i); tris.Add(i + 2); tris.Add(i + 3);
            }

            public void Disc(float radius, float y, int segs)
            {
                int c = verts.Count;
                verts.Add(new Vector3(0f, y, 0f));
                normals.Add(Vector3.up);
                uvs.Add(Vector2.zero);
                for (int k = 0; k <= segs; k++)
                {
                    var p = P(radius, 360f * k / segs, y);
                    verts.Add(p);
                    normals.Add(Vector3.up);
                    uvs.Add(FlatUV(p));
                }
                for (int k = 0; k < segs; k++)
                {
                    tris.Add(c);
                    tris.Add(c + 1 + k);
                    tris.Add(c + 2 + k);
                }
            }

            public void RingTop(float rIn, float rOut, float y, int segs)
            {
                for (int k = 0; k < segs; k++)
                {
                    float t0 = 360f * k / segs, t1 = 360f * (k + 1) / segs;
                    Vector3 a = P(rIn, t0, y), b = P(rOut, t0, y), c = P(rOut, t1, y), d = P(rIn, t1, y);
                    Quad(a, b, c, d, Vector3.up, Vector3.up, Vector3.up, Vector3.up,
                         FlatUV(a), FlatUV(b), FlatUV(c), FlatUV(d));
                }
            }

            /// 一段環形厚牆。內側面從 innerY0 到 yTop，外側面從 outerY0 到 yTop。
            public void TubeArc(float rIn, float rOut, float innerY0, float outerY0, float yTop,
                                float a0, float a1, int segs,
                                bool inner, bool outer, bool top, bool caps)
            {
                for (int k = 0; k < segs; k++)
                {
                    float t0 = Mathf.Lerp(a0, a1, (float)k / segs);
                    float t1 = Mathf.Lerp(a0, a1, (float)(k + 1) / segs);

                    if (inner && yTop > innerY0)
                    {
                        Vector3 n0 = -ArenaData.Dir(t0), n1 = -ArenaData.Dir(t1);
                        Quad(P(rIn, t0, innerY0), P(rIn, t0, yTop), P(rIn, t1, yTop), P(rIn, t1, innerY0),
                             n0, n0, n1, n1,
                             WallUV(rIn, t0, innerY0), WallUV(rIn, t0, yTop), WallUV(rIn, t1, yTop), WallUV(rIn, t1, innerY0));
                    }

                    if (outer && yTop > outerY0)
                    {
                        Vector3 n0 = ArenaData.Dir(t0), n1 = ArenaData.Dir(t1);
                        Quad(P(rOut, t1, outerY0), P(rOut, t1, yTop), P(rOut, t0, yTop), P(rOut, t0, outerY0),
                             n1, n1, n0, n0,
                             WallUV(rOut, t1, outerY0), WallUV(rOut, t1, yTop), WallUV(rOut, t0, yTop), WallUV(rOut, t0, outerY0));
                    }

                    if (top)
                    {
                        Vector3 a = P(rIn, t0, yTop), b = P(rOut, t0, yTop), c = P(rOut, t1, yTop), d = P(rIn, t1, yTop);
                        Quad(a, b, c, d, Vector3.up, Vector3.up, Vector3.up, Vector3.up,
                             FlatUV(a), FlatUV(b), FlatUV(c), FlatUV(d));
                    }
                }

                if (caps)
                {
                    float y0 = Mathf.Min(innerY0, outerY0);
                    float depth = (rOut - rIn) / Tile;

                    Vector3 ns = -Tangent(a0);
                    Quad(P(rOut, a0, y0), P(rOut, a0, yTop), P(rIn, a0, yTop), P(rIn, a0, y0),
                         ns, ns, ns, ns,
                         new Vector2(0f, y0 / Tile), new Vector2(0f, yTop / Tile),
                         new Vector2(depth, yTop / Tile), new Vector2(depth, y0 / Tile));

                    Vector3 ne = Tangent(a1);
                    Quad(P(rIn, a1, y0), P(rIn, a1, yTop), P(rOut, a1, yTop), P(rOut, a1, y0),
                         ne, ne, ne, ne,
                         new Vector2(0f, y0 / Tile), new Vector2(0f, yTop / Tile),
                         new Vector2(depth, yTop / Tile), new Vector2(depth, y0 / Tile));
                }
            }

            public void WriteTo(Mesh m)
            {
                m.Clear();
                m.indexFormat = verts.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
                m.SetVertices(verts);
                m.SetNormals(normals);
                m.SetUVs(0, uvs);
                m.SetTriangles(tris, 0);
                m.RecalculateBounds();
                m.RecalculateTangents();
            }
        }
    }
}
