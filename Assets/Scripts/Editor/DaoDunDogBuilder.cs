using System.Collections.Generic;
using BossFight.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BossFight.EditorTools
{
    /// <summary>
    /// 用基本形體拼出刀盾狗（照公仔造型）：蛋形身體、頭身一體，上面橘、肚子和下巴奶油色；
    /// 長狗吻 + 黑鼻子 + 壞笑、半瞇杏仁眼、貼在頭側的小圓耳、短手、青蛙腳。
    /// 右手短刀，左手金邊黑底的鳶形盾（中間金色太陽紋）。
    /// 存成 Assets/Prefabs/DaoDunDog.prefab（掛好 PlayerController、PlayerStamina 與 PlayerStats），放到場景的 PlayerSpawn，
    /// 再用 CameraRigBuilder 建立 Cinemachine 鏡頭。
    /// 選單 Tools > Player > Build DaoDun Dog；重建時沿用場景中舊角色的位置。
    /// 尺寸以 +Z 為前方、腳底 y = 0，總高約 1.4 m。
    /// </summary>
    public static class DaoDunDogBuilder
    {
        const string ModelDir = "Assets/Models/DaoDunDog";
        const string MatDir = ModelDir + "/Materials";
        const string MeshDir = ModelDir + "/Meshes";
        const string PrefabDir = "Assets/Prefabs";
        const string PrefabPath = PrefabDir + "/DaoDunDog.prefab";
        const string StatsPath = "Assets/ScriptableObjects/Player/PlayerStats.asset";

        class Mats
        {
            public Material fur, cream, dark, legs, earInner, steel, gold, shieldFace;
        }

        class Meshes
        {
            public Mesh sphere, knife, shieldPlate, shieldFace;
        }

        // 鳶形盾輪廓（逆時針）：平頂微拱、兩側往下收成尖底
        static readonly Vector2[] ShieldOutline =
        {
            new Vector2(0f, -0.44f),
            new Vector2(0.1f, -0.38f),
            new Vector2(0.19f, -0.27f),
            new Vector2(0.25f, -0.12f),
            new Vector2(0.28f, 0.05f),
            new Vector2(0.28f, 0.33f),
            new Vector2(0.14f, 0.355f),
            new Vector2(0f, 0.36f),
            new Vector2(-0.14f, 0.355f),
            new Vector2(-0.28f, 0.33f),
            new Vector2(-0.28f, 0.05f),
            new Vector2(-0.25f, -0.12f),
            new Vector2(-0.19f, -0.27f),
            new Vector2(-0.1f, -0.38f),
        };

        [MenuItem("Tools/Player/Build DaoDun Dog")]
        public static void BuildFromMenu()
        {
            Selection.activeGameObject = Build();
        }

        public static GameObject Build()
        {
            EnsureFolder(MatDir);
            EnsureFolder(MeshDir);
            EnsureFolder(PrefabDir);

            var fur = new Color(0.98f, 0.6f, 0.3f);
            var m = new Mats
            {
                fur = GetMat("DaoDunDog_Fur", fur, 0f, 0.35f),
                cream = GetMat("DaoDunDog_Cream", new Color(1f, 0.94f, 0.84f), 0f, 0.3f),
                dark = GetMat("DaoDunDog_Dark", new Color(0.04f, 0.04f, 0.04f), 0f, 0.75f),
                legs = GetMat("DaoDunDog_Legs", fur, 0f, 0.35f),
                earInner = GetMat("DaoDunDog_EarInner", new Color(0.86f, 0.48f, 0.32f), 0f, 0.3f),
                steel = GetMat("DaoDunDog_Steel", new Color(0.78f, 0.8f, 0.84f), 1f, 0.7f),
                gold = GetMat("DaoDunDog_Gold", new Color(1f, 0.78f, 0.28f), 1f, 0.75f),
                shieldFace = GetMat("DaoDunDog_ShieldFace", new Color(0.06f, 0.06f, 0.07f), 0f, 0.7f),
            };
            var meshes = BuildMeshes();

            Undo.SetCurrentGroupName("Build DaoDun Dog");
            int undoGroup = Undo.GetCurrentGroup();

            // 位置：沿用場景中舊的角色，否則放在 PlayerSpawn
            Vector3 pos = Vector3.zero;
            Quaternion rot = Quaternion.identity;
            var old = Object.FindAnyObjectByType<PlayerController>();
            if (old != null)
            {
                pos = old.transform.position;
                rot = old.transform.rotation;
                Undo.DestroyObjectImmediate(old.gameObject);
            }
            else
            {
                var spawn = GameObject.Find("PlayerSpawn");
                if (spawn != null)
                {
                    pos = spawn.transform.position;
                    rot = Quaternion.Euler(0f, spawn.transform.eulerAngles.y, 0f);
                }
            }

            var root = BuildModel(m, meshes);
            PrefabUtility.SaveAsPrefabAssetAndConnect(root, PrefabPath, InteractionMode.AutomatedAction);
            Undo.RegisterCreatedObjectUndo(root, "Build DaoDun Dog");
            root.transform.SetPositionAndRotation(pos, rot);

            CameraRigBuilder.Build(root.GetComponent<PlayerController>());

            Undo.CollapseUndoOperations(undoGroup);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(root.scene);

            Debug.Log($"[Player] 刀盾狗已建立：{PrefabPath}，位置 {pos}");
            return root;
        }

        // ---------- 模型 ----------

        static GameObject BuildModel(Mats m, Meshes meshes)
        {
            var root = new GameObject("DaoDunDog");
            root.tag = "Player";

            var cc = root.AddComponent<CharacterController>();
            cc.center = new Vector3(0f, 0.7f, 0f);
            cc.height = 1.4f;
            cc.radius = 0.5f;
            cc.stepOffset = 0.3f;
            cc.skinWidth = 0.04f;
            var pc = root.AddComponent<PlayerController>(); // RequireComponent 會一起加上 PlayerStamina
            var stats = AssetDatabase.LoadAssetAtPath<PlayerStats>(StatsPath);
            if (stats == null) Debug.LogWarning($"[Player] 找不到 {StatsPath}，PlayerController 與 PlayerStamina 的 stats 要手動指定。");
            SetReference(pc, "stats", stats);
            SetReference(root.GetComponent<PlayerStamina>(), "stats", stats);
            var anim = root.AddComponent<DaoDunDogAnimator>();
            anim.controller = pc;

            var model = Node(root.transform, "Model", Vector3.zero);
            anim.model = model;

            // 身體：以髖部為軸，起伏/前傾/扭腰都轉這裡；腳不在底下，才不會跟著離地
            // 蛋形橘色身體 + 往前下方偏的奶油色肚子，正面下半就是奶油色
            var body = Node(model, "Body", new Vector3(0f, 0.25f, 0f));
            Ball(body, "Torso", new Vector3(0f, 0.5f, 0f), new Vector3(1f, 1.15f, 0.95f), m.fur, meshes);
            Ball(body, "Belly", new Vector3(0f, 0.45f, 0.1f), new Vector3(0.88f, 1.05f, 0.8f), m.cream, meshes);

            var head = BuildHead(body, m, meshes);

            // 右手往前舉著短刀；左手垂在身側拿盾
            var armR = BuildArm(body, "Arm_R", 1f, m);
            armR.localRotation = Quaternion.Euler(-50f, 0f, 12f);
            var knife = BuildKnife(armR, m, meshes.knife);

            var armL = BuildArm(body, "Arm_L", -1f, m);
            armL.localRotation = Quaternion.Euler(-15f, 0f, -15f);
            var shieldGrip = BuildShield(armL, m, meshes);
            // 盾面朝左前方，朝向以身體空間決定
            shieldGrip.localRotation = Quaternion.Inverse(armL.localRotation) *
                                       Quaternion.LookRotation(new Vector3(-0.6f, 0f, 0.8f));

            var legR = BuildLeg(model, "Leg_R", 1f, m);
            var legL = BuildLeg(model, "Leg_L", -1f, m);

            anim.body = body;
            anim.head = head;
            anim.armR = armR;
            anim.sword = knife;
            anim.armL = armL;
            anim.shieldGrip = shieldGrip;
            anim.legR = legR;
            anim.legL = legL;
            return root;
        }

        static Transform BuildHead(Transform body, Mats m, Meshes meshes)
        {
            // 頭頂和身體連在一起，只是微微鼓起；臉部零件都掛在這裡才能點頭、轉頭
            var head = Node(body, "Head", new Vector3(0f, 0.78f, 0f));

            var crownC = new Vector3(0f, 0.06f, 0.03f); // 往前偏，只有臉那側鼓出身體
            var crownR = new Vector3(0.4f, 0.23f, 0.38f);
            Ball(head, "Crown", crownC, crownR * 2f, m.fur, meshes);
            Ball(head, "Cheeks", new Vector3(0f, -0.07f, 0.27f), new Vector3(0.5f, 0.3f, 0.38f), m.cream, meshes);

            // 長而尖的狗吻：下半奶油色，上面一條橘色鼻樑
            var snoutC = new Vector3(0f, 0f, 0.52f);
            var snoutR = new Vector3(0.12f, 0.1f, 0.25f);
            Ball(head, "Snout", snoutC, snoutR * 2f, m.cream, meshes);
            Ball(head, "SnoutBridge", new Vector3(0f, 0.045f, 0.44f), new Vector3(0.18f, 0.12f, 0.48f), m.fur, meshes);
            Prim(PrimitiveType.Sphere, head, "Nose", new Vector3(0f, 0.02f, 0.76f), new Vector3(0.12f, 0.095f, 0.09f), m.dark);

            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? 1f : -1f;
                string sfx = i == 0 ? "_R" : "_L";

                // 嘴線沿著狗吻下緣往後，右嘴角往上翹（壞笑）
                var m0 = OnEllipsoid(snoutC, snoutR, side, -0.055f, 0.71f, 0.004f);
                var m1 = OnEllipsoid(snoutC, snoutR, side, -0.055f, 0.59f, 0.004f);
                var m2 = OnEllipsoid(snoutC, snoutR, side, -0.045f, 0.49f, 0.004f);
                var corner = i == 0 ? new Vector3(0.14f, -0.005f, 0.44f) : new Vector3(0.13f, -0.035f, 0.43f);
                Segment(head, "Mouth" + sfx + "_0", m0, m1, 0.014f, m.dark);
                Segment(head, "Mouth" + sfx + "_1", m1, m2, 0.014f, m.dark);
                Segment(head, "Mouth" + sfx + "_2", m2, corner, 0.014f, m.dark);

                // 鬍鬚點
                Prim(PrimitiveType.Sphere, head, "Whisker" + sfx + "_0", OnEllipsoid(snoutC, snoutR, side, -0.02f, 0.6f, 0.003f), Vector3.one * 0.018f, m.dark);
                Prim(PrimitiveType.Sphere, head, "Whisker" + sfx + "_1", OnEllipsoid(snoutC, snoutR, side, -0.03f, 0.65f, 0.003f), Vector3.one * 0.018f, m.dark);
                Prim(PrimitiveType.Sphere, head, "Whisker" + sfx + "_2", OnEllipsoid(snoutC, snoutR, side, -0.028f, 0.55f, 0.003f), Vector3.one * 0.018f, m.dark);

                // 半瞇的杏仁眼：眼尾微微下垂（往上翹會變生氣臉），上緣蓋一片毛色眼皮
                var eyePos = OnEllipsoid(crownC, crownR, side, 0.15f, 0.33f, 0f);
                var eyeRot = Quaternion.LookRotation(EllipsoidNormal(crownC, crownR, eyePos), Vector3.up) *
                             Quaternion.Euler(0f, 0f, -8f * side);
                Prim(PrimitiveType.Sphere, head, "Eye" + sfx, eyePos, new Vector3(0.105f, 0.066f, 0.05f), m.dark, eyeRot);
                Prim(PrimitiveType.Sphere, head, "Lid" + sfx, eyePos + eyeRot * new Vector3(0f, 0.034f, 0.008f),
                     new Vector3(0.12f, 0.045f, 0.055f), m.fur, eyeRot);

                // 小圓耳貼在頭側偏後，往後倒
                var earPos = OnEllipsoid(crownC, crownR, side, 0.18f, -0.1f, 0f);
                var earN = EllipsoidNormal(crownC, crownR, earPos);
                var earRot = Quaternion.LookRotation(earN, new Vector3(0f, 0.6f, -0.8f));
                Prim(PrimitiveType.Sphere, head, "Ear" + sfx, earPos + earN * 0.01f, new Vector3(0.12f, 0.17f, 0.045f), m.fur, earRot);
                Prim(PrimitiveType.Sphere, head, "EarInner" + sfx, earPos + earN * 0.022f, new Vector3(0.075f, 0.115f, 0.03f), m.earInner, earRot);
            }
            return head;
        }

        static Transform BuildArm(Transform body, string name, float side, Mats m)
        {
            var arm = Node(body, name, new Vector3(0.4f * side, 0.42f, 0.1f));
            Limb(arm, "UpperArm", Vector3.zero, new Vector3(0.1f * side, -0.2f, 0f), 0.08f, m.fur);
            Prim(PrimitiveType.Sphere, arm, "Paw", PawPos(side), Vector3.one * 0.13f, m.fur);
            return arm;
        }

        static Vector3 PawPos(float side) => new Vector3(0.11f * side, -0.23f, 0.02f);

        // 短刀：+Y 沿刀身，+Z 是刀刃那側；黑色握柄
        static Transform BuildKnife(Transform armR, Mats m, Mesh blade)
        {
            var knife = Node(armR, "Knife", PawPos(1f));
            knife.localRotation = Quaternion.Euler(85f, 15f, 0f);
            knife.localScale = Vector3.one * 1.3f;

            Prim(PrimitiveType.Capsule, knife, "Handle", new Vector3(0f, -0.01f, 0f), new Vector3(0.055f, 0.09f, 0.055f), m.dark);
            var b = new GameObject("Blade");
            b.transform.SetParent(knife, false);
            b.transform.localPosition = new Vector3(0f, 0.075f, 0f);
            b.AddComponent<MeshFilter>().sharedMesh = blade;
            b.AddComponent<MeshRenderer>().sharedMaterial = m.steel;
            return knife;
        }

        // 鳶形盾：握把在左手掌，+Z 是盾面朝外；金邊、黑底、中間金色太陽紋與一圈鉚釘
        static Transform BuildShield(Transform armL, Mats m, Meshes meshes)
        {
            var grip = Node(armL, "ShieldGrip", PawPos(-1f));
            var shield = Node(grip, "Shield", new Vector3(0f, 0.06f, 0.18f));

            MeshObject(shield, "Plate", meshes.shieldPlate, m.gold);
            MeshObject(shield, "Face", meshes.shieldFace, m.shieldFace);

            var faceOut = Quaternion.Euler(90f, 0f, 0f); // 圓柱的 Y 軸轉到 +Z
            var sun = new Vector3(0f, 0.02f, 0f);
            Prim(PrimitiveType.Cylinder, shield, "Sun", sun + new Vector3(0f, 0f, 0.04f), new Vector3(0.13f, 0.008f, 0.13f), m.gold, faceOut);
            Prim(PrimitiveType.Sphere, shield, "SunBoss", sun + new Vector3(0f, 0f, 0.05f), new Vector3(0.06f, 0.06f, 0.03f), m.gold);
            for (int i = 0; i < 4; i++)
            {
                Prim(PrimitiveType.Cube, shield, "Ray_" + i, sun + new Vector3(0f, 0f, 0.038f), new Vector3(0.022f, 0.26f, 0.01f), m.gold,
                     Quaternion.Euler(0f, 0f, i * 45f));
            }

            int[] rivets = { 0, 2, 4, 5, 7, 9, 10, 12 };
            for (int i = 0; i < rivets.Length; i++)
            {
                Vector2 p = ShieldOutline[rivets[i]] * 0.92f;
                Prim(PrimitiveType.Sphere, shield, "Rivet_" + i, new Vector3(p.x, p.y, 0.024f), Vector3.one * 0.03f, m.gold);
            }
            return grip;
        }

        // 青蛙腳：短短的大腿藏在身體下，扁平腳掌 + 四根往外張的長腳趾，趾尖圓圓的
        static Transform BuildLeg(Transform model, string name, float side, Mats m)
        {
            var leg = Node(model, name, new Vector3(0.22f * side, 0.25f, 0.1f));
            Limb(leg, "Thigh", Vector3.zero, new Vector3(0.07f * side, -0.16f, 0.05f), 0.1f, m.legs);

            var palm = new Vector3(0.1f * side, -0.22f, 0.14f);
            Prim(PrimitiveType.Sphere, leg, "Foot", palm, new Vector3(0.18f, 0.05f, 0.17f), m.legs);
            float[] angles = { -32f, -10f, 12f, 34f };
            for (int i = 0; i < angles.Length; i++)
            {
                var dir = Quaternion.Euler(0f, angles[i] * side, 0f) * Vector3.forward;
                Prim(PrimitiveType.Sphere, leg, "Toe_" + i, palm + dir * 0.11f, new Vector3(0.05f, 0.04f, 0.17f), m.legs,
                     Quaternion.LookRotation(dir));
                Prim(PrimitiveType.Sphere, leg, "ToeTip_" + i, palm + dir * 0.19f, Vector3.one * 0.055f, m.legs);
            }
            return leg;
        }

        // ---------- 工具 ----------

        /// 指定私有 [SerializeField] 欄位。
        static void SetReference(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static Transform Node(Transform parent, string name, Vector3 localPos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            return go.transform;
        }

        static GameObject Prim(PrimitiveType type, Transform parent, string name, Vector3 localPos, Vector3 scale,
                               Material mat, Quaternion? localRot = null)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>()); // 碰撞只用 CharacterController
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot ?? Quaternion.identity;
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        /// 大塊的圓形部位用細分較高的球，交界線和輪廓比較平滑。
        static GameObject Ball(Transform parent, string name, Vector3 localPos, Vector3 scale, Material mat, Meshes meshes)
        {
            var go = MeshObject(parent, name, meshes.sphere, mat);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            return go;
        }

        static GameObject MeshObject(Transform parent, string name, Mesh mesh, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        /// 從 a 到 b 的膠囊，兩端圓頭剛好包住關節。
        static GameObject Limb(Transform parent, string name, Vector3 a, Vector3 b, float radius, Material mat)
        {
            Vector3 d = b - a;
            return Prim(PrimitiveType.Capsule, parent, name, (a + b) * 0.5f,
                        new Vector3(radius * 2f, (d.magnitude + radius * 2f) * 0.5f, radius * 2f), mat,
                        Quaternion.FromToRotation(Vector3.up, d));
        }

        /// 從 a 到 b 的細長方塊，用來畫嘴線。
        static GameObject Segment(Transform parent, string name, Vector3 a, Vector3 b, float thickness, Material mat)
        {
            Vector3 d = b - a;
            return Prim(PrimitiveType.Cube, parent, name, (a + b) * 0.5f,
                        new Vector3(thickness, thickness, d.magnitude + thickness), mat,
                        Quaternion.LookRotation(d, Vector3.up));
        }

        /// 橢球表面上指定 (y, z) 那一側（side = ±1）的點，再沿 x 往外推 lift。
        static Vector3 OnEllipsoid(Vector3 c, Vector3 r, float side, float y, float z, float lift)
        {
            float dy = (y - c.y) / r.y, dz = (z - c.z) / r.z;
            float x = r.x * Mathf.Sqrt(Mathf.Max(0f, 1f - dy * dy - dz * dz));
            return new Vector3(side * (x + lift), y, z);
        }

        static Vector3 EllipsoidNormal(Vector3 c, Vector3 r, Vector3 p)
        {
            Vector3 d = p - c;
            return new Vector3(d.x / (r.x * r.x), d.y / (r.y * r.y), d.z / (r.z * r.z)).normalized;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            string parent = path.Substring(0, slash);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(slash + 1));
        }

        /// 材質只在不存在時建立，之後你在 Inspector 改顏色不會被重建覆蓋。
        static Material GetMat(string name, Color color, float metallic, float smoothness)
        {
            string path = $"{MatDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            mat = new Material(shader) { name = name };
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        // ---------- 刀身與盾牌網格 ----------

        static Meshes BuildMeshes()
        {
            // 刀身輪廓在 (z, y)：z 是寬度方向（+z 為刀刃），y 沿刀身；刀背直、刀尖往下收（drop point）
            Vector2[] blade =
            {
                new Vector2(-0.03f, 0f),
                new Vector2(0.045f, 0f),
                new Vector2(0.05f, 0.18f),
                new Vector2(0.035f, 0.27f),
                new Vector2(0.005f, 0.32f),
                new Vector2(-0.03f, 0.23f),
            };
            float[] bladeHalf = { 0.008f, 0.0015f, 0.0015f, 0.0015f, 0.0015f, 0.007f }; // 刀背厚、刀刃薄

            var faceOutline = new Vector2[ShieldOutline.Length];
            var faceCenter = new Vector2(0f, -0.02f);
            for (int i = 0; i < faceOutline.Length; i++)
                faceOutline[i] = (ShieldOutline[i] - faceCenter) * 0.86f + faceCenter;

            return new Meshes
            {
                sphere = BuildSphereMesh("DaoDunDog_Sphere", 48, 32),
                knife = ExtrudeConvex("DaoDunDog_Knife", blade, bladeHalf, (q, s) => new Vector3(s, q.y, q.x)),
                shieldPlate = ExtrudeConvex("DaoDunDog_ShieldPlate", ShieldOutline, Uniform(ShieldOutline.Length, 0.02f),
                                            (q, s) => new Vector3(q.x, q.y, s)),
                shieldFace = ExtrudeConvex("DaoDunDog_ShieldFace", faceOutline, Uniform(faceOutline.Length, 0.006f),
                                           (q, s) => new Vector3(q.x, q.y, 0.026f + s)),
            };
        }

        /// 半徑 0.5 的經緯球（和內建 Sphere 同尺寸），lon × lat 分段。
        static Mesh BuildSphereMesh(string assetName, int lon, int lat)
        {
            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            for (int y = 0; y <= lat; y++)
            {
                float theta = Mathf.PI * y / lat;
                for (int x = 0; x <= lon; x++)
                {
                    float phi = 2f * Mathf.PI * x / lon;
                    var n = new Vector3(Mathf.Sin(theta) * Mathf.Cos(phi), Mathf.Cos(theta), Mathf.Sin(theta) * Mathf.Sin(phi));
                    verts.Add(n * 0.5f);
                    normals.Add(n);
                    uvs.Add(new Vector2((float)x / lon, 1f - (float)y / lat));
                }
            }
            for (int y = 0; y < lat; y++)
            {
                for (int x = 0; x < lon; x++)
                {
                    int a = y * (lon + 1) + x, b = a + lon + 1;
                    tris.Add(a); tris.Add(a + 1); tris.Add(b + 1);
                    tris.Add(a); tris.Add(b + 1); tris.Add(b);
                }
            }

            var mesh = GetMesh(assetName);
            mesh.Clear();
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        static Mesh GetMesh(string assetName)
        {
            string path = $"{MeshDir}/{assetName}.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null)
            {
                mesh = new Mesh { name = assetName };
                AssetDatabase.CreateAsset(mesh, path);
            }
            return mesh;
        }

        static float[] Uniform(int n, float v)
        {
            var a = new float[n];
            for (int i = 0; i < n; i++) a[i] = v;
            return a;
        }

        /// 凸多邊形輪廓擠出成平面著色的實心片。halfThick[i] 是第 i 點往兩側的半厚度，
        /// map(輪廓點, 厚度方向位移) 轉成 3D 座標（須為仿射）。輪廓順逆時針皆可。
        static Mesh ExtrudeConvex(string assetName, Vector2[] outline, float[] halfThick,
                                  System.Func<Vector2, float, Vector3> map)
        {
            var p = (Vector2[])outline.Clone();
            var h = (float[])halfThick.Clone();
            float area = 0f;
            for (int i = 0; i < p.Length; i++)
            {
                var a = p[i];
                var b = p[(i + 1) % p.Length];
                area += a.x * b.y - b.x * a.y;
            }
            if (area < 0f)
            {
                System.Array.Reverse(p);
                System.Array.Reverse(h);
            }

            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var tris = new List<int>();
            Vector3 origin = map(Vector2.zero, 0f);
            Vector3 thickDir = map(Vector2.zero, 1f) - origin;

            Vector3 V(int i, float side) => map(p[i], side * h[i]);

            void Tri(Vector3 a, Vector3 b, Vector3 c, Vector3 outward)
            {
                Vector3 n = Vector3.Cross(b - a, c - a);
                if (Vector3.Dot(n, outward) < 0f)
                {
                    (b, c) = (c, b);
                    n = -n;
                }
                n.Normalize();
                int i0 = verts.Count;
                verts.Add(a); verts.Add(b); verts.Add(c);
                normals.Add(n); normals.Add(n); normals.Add(n);
                tris.Add(i0); tris.Add(i0 + 1); tris.Add(i0 + 2);
            }

            int k = p.Length;
            for (int i = 1; i < k - 1; i++)
            {
                Tri(V(0, 1f), V(i, 1f), V(i + 1, 1f), thickDir);
                Tri(V(0, -1f), V(i, -1f), V(i + 1, -1f), -thickDir);
            }
            for (int i = 0; i < k; i++)
            {
                int j = (i + 1) % k;
                Vector2 e = p[j] - p[i];
                Vector3 outward = map(new Vector2(e.y, -e.x), 0f) - origin; // 逆時針輪廓的外法線
                Tri(V(i, 1f), V(j, 1f), V(j, -1f), outward);
                Tri(V(i, 1f), V(j, -1f), V(i, -1f), outward);
            }

            var mesh = GetMesh(assetName);
            mesh.Clear();
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            return mesh;
        }
    }
}
