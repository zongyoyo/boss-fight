using System.Linq;
using BossFight.Progression;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BossFight.EditorTools
{
    /// <summary>
    /// Tools > Art > Place Style Slice：美術 Phase 1 的風格切片（docs/Art/ART_STYLE.md 第 10 節）。
    /// 在開著的序章場景東北角，把扇形範圍裡的佔位樹關掉、原位換成正式模型（針葉 7 成、闊葉 3 成，碰撞照佔位樹幹），
    /// 再散佈草叢、石頭與樹樁。重跑會先拆掉舊的切片、打開佔位樹；重跑 Build Prologue 會清掉切片，再按一次即可。
    /// 暫時工具：Phase 2 把植被散佈併進 PrologueBuilder 後刪除。
    /// </summary>
    public static class StyleSliceBuilder
    {
        const string RootName = "StyleSlice";
        // 切片的扇形（度，0 = 北、順時針）與草叢散佈範圍（m）；純編輯器工具的版面參數
        const float SectorFrom = 5f, SectorTo = 75f;
        const float GrassInner = 11.5f, GrassOuter = 22f;
        const int GrassClusters = 45;
        const int ClusterMax = 4;
        const float ClusterSpread = 0.45f;
        const float PineShare = 0.7f;
        const float PineHeight = 8f;
        // 佔位樹的高度範圍對到模型的縮放：佔位樹高 / 這個值
        const float TreeScaleDivisor = 8f;

        static readonly (string id, float angle, float radius, float scale, float yaw)[] Props =
        {
            ("SM_Rock_A", 22f, 15.3f, 1.0f, 30f),
            ("SM_Rock_A", 56f, 16.6f, 1.5f, 200f),
            ("SM_Rock_A", 47f, 13.6f, 0.55f, 110f),
            ("SM_Stump_A", 33f, 14.5f, 1.0f, 70f),
        };

        [MenuItem("Tools/Art/Place Style Slice")]
        public static void PlaceFromMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            Debug.Log(Place());
        }

        public static string Place()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path != PrologueBuilder.ScenePath) return "[Art] 請先開啟 Prologue 場景。";
            var data = AssetDatabase.LoadAssetAtPath<PrologueLayoutData>(PrologueBuilder.LayoutPath);
            var forest = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Level")?.transform.Find("Forest");
            if (data == null || forest == null) return "[Art] 找不到 PrologueLayout 或 Level/Forest，先跑 Tools > Prologue > Build Prologue。";
            foreach (var id in new[] { "SM_Tree_Pine_A", "SM_Tree_Broad_A", "SM_Grass_Clump_A", "SM_Rock_A", "SM_Stump_A" })
                if (!ArtLibrary.Has(id)) return $"[Art] ArtLibrary 沒有 {id}：先在 Blender 跑 Art/Blender/assets/env 的腳本。";

            foreach (var old in scene.GetRootGameObjects().Where(g => g.name == RootName)) Object.DestroyImmediate(old);
            foreach (Transform tree in forest) tree.gameObject.SetActive(true);
            var root = new GameObject(RootName).transform;
            var trees = new GameObject("Trees").transform;
            trees.SetParent(root, false);
            var random = new System.Random(data.visualSeed);

            int replaced = 0;
            foreach (Transform tree in forest)
            {
                if (!InSector(tree.position)) continue;
                var trunk = tree.Find("Trunk");
                float height = trunk != null ? trunk.localScale.y * 2f : PineHeight;
                bool pine = random.NextDouble() < PineShare;
                var holder = Holder(trees, tree.name, tree.position, (float)random.NextDouble() * 360f, height / TreeScaleDivisor);
                ArtLibrary.Spawn(pine ? "SM_Tree_Pine_A" : "SM_Tree_Broad_A", holder);
                // 樹幹碰撞照佔位樹（半徑 trunkRadius），高度只要擋得住玩家
                var capsule = holder.gameObject.AddComponent<CapsuleCollider>();
                capsule.radius = data.trunkRadius / holder.localScale.x;
                capsule.height = data.wallHeight / holder.localScale.x;
                capsule.center = Vector3.up * capsule.height * 0.5f;
                tree.gameObject.SetActive(false);
                replaced++;
            }

            var grass = new GameObject("Grass").transform;
            grass.SetParent(root, false);
            int clumps = 0;
            for (int i = 0; i < GrassClusters; i++)
            {
                float angle = Mathf.Lerp(SectorFrom, SectorTo, (float)random.NextDouble());
                // 越靠近空地邊緣越密（鏡頭前景），往林子裡變疏；一處 1–4 叢擠在一起才像一片草
                float radius = Mathf.Lerp(GrassInner, GrassOuter, Mathf.Pow((float)random.NextDouble(), 1.6f));
                var center = Polar(angle, radius);
                int count = 1 + random.Next(ClusterMax);
                for (int k = 0; k < count; k++, clumps++)
                {
                    var offset = new Vector3((float)random.NextDouble() * 2f - 1f, 0f, (float)random.NextDouble() * 2f - 1f) * ClusterSpread;
                    var holder = Holder(grass, $"Grass_{clumps:000}", center + offset, (float)random.NextDouble() * 360f,
                                        Mathf.Lerp(0.8f, 1.35f, (float)random.NextDouble()));
                    ArtLibrary.Spawn("SM_Grass_Clump_A", holder);
                }
            }

            var props = new GameObject("Props").transform;
            props.SetParent(root, false);
            foreach (var p in Props)
            {
                var holder = Holder(props, p.id, Polar(p.angle, p.radius), p.yaw, p.scale);
                var model = ArtLibrary.Spawn(p.id, holder);
                // 石頭與樹樁在空地邊緣，玩家走得到：用模型的包圍盒擋住
                var bounds = model.GetComponentInChildren<MeshFilter>().sharedMesh.bounds;
                var box = holder.gameObject.AddComponent<BoxCollider>();
                box.center = bounds.center;
                box.size = bounds.size;
            }

            foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.isStatic = true;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return $"[Art] 風格切片：換掉 {replaced} 棵佔位樹，放了 {clumps} 叢草、{Props.Length} 個石頭與樹樁。";
        }

        static bool InSector(Vector3 p)
        {
            float angle = Mathf.Repeat(Mathf.Atan2(p.x, p.z) * Mathf.Rad2Deg, 360f);
            return angle >= SectorFrom && angle <= SectorTo;
        }

        static Vector3 Polar(float angle, float radius)
        {
            float a = angle * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * radius;
        }

        static Transform Holder(Transform parent, string name, Vector3 position, float yaw, float scale)
        {
            var holder = new GameObject(name).transform;
            holder.SetParent(parent, false);
            holder.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            holder.localScale = Vector3.one * scale;
            return holder;
        }
    }
}
