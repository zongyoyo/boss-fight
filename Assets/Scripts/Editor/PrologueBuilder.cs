using System;
using BossFight.Core;
using BossFight.Progression;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace BossFight.EditorTools
{
    /// <summary>重建序章佔位場景；版面只讀 SO，資產已存在時不覆寫調整過的數值。</summary>
    public static class PrologueBuilder
    {
        public const string ScenePath = "Assets/Scenes/Prologue.unity";
        public const string LayoutPath = "Assets/ScriptableObjects/Progression/PrologueLayout.asset";
        const string ModelDir = "Assets/Models/Prologue";
        const string OptionsPath = "Assets/ScriptableObjects/Options/GameOptionsData.asset";

        [MenuItem("Tools/Prologue/Build Prologue")]
        public static void BuildFromMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Build();
        }

        /// <summary>Tools > Prologue > Validate Prologue：在 Prologue 場景依序跑所有序章 Builder 的接線檢查（不改場景、不檢查玩法數值）。</summary>
        [MenuItem("Tools/Prologue/Validate Prologue")]
        public static void ValidateAll()
        {
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != ScenePath)
            {
                Debug.LogError("[Prologue] 請先開啟 Prologue 場景。");
                return;
            }
            Debug.Log(PrologueActorsBuilder.Validate());
            Debug.Log(TutorialBuilder.Validate());
            Debug.Log(GuardianBuilder.Validate());
            Debug.Log(GuardianPhaseTwoBuilder.Validate());
            Debug.Log(PrologueCutsceneBuilder.Validate());
        }

        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("不能在 Play 模式重建序章。");
            if (AssetDatabase.LoadAssetAtPath<GameObject>(DaoDunDogBuilder.PrefabPath) == null)
                throw new InvalidOperationException("請先建立刀盾狗 prefab。");
            // 與 HubBuilder 一樣，先建立場景再取資產，避免 NewScene 卸載先前取到的引用。
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            DaoDunDogBuilder.EnsureFolder(ModelDir);
            DaoDunDogBuilder.EnsureFolder(LayoutPath.Substring(0, LayoutPath.LastIndexOf('/')));
            var data = AssetDatabase.LoadAssetAtPath<PrologueLayoutData>(LayoutPath);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<PrologueLayoutData>();
                AssetDatabase.CreateAsset(data, LayoutPath);
            }
            var grass = Material("Grass", new Color(0.32f, 0.48f, 0.24f));
            var earth = Material("Earth", new Color(0.62f, 0.53f, 0.34f));
            var bark = Material("Bark", new Color(0.29f, 0.2f, 0.12f));
            var leaves = new[] { Material("Leaves0", new Color(0.16f, 0.36f, 0.16f)),
                                 Material("Leaves1", new Color(0.24f, 0.43f, 0.2f)),
                                 Material("Leaves2", new Color(0.12f, 0.3f, 0.19f)) };
            var mountain = Material("Mountain", new Color(0.42f, 0.49f, 0.48f));
            var level = new GameObject("Level").transform;
            float groundRadius = data.clearingRadius + data.treeRows * data.rowSpacing + data.wallThickness;
            var ground = Primitive("Ground", level, PrimitiveType.Cylinder, new Vector3(0f, -0.25f, 0f),
                                   new Vector3(groundRadius * 2f, 0.25f, groundRadius * 2f), grass, false);
            // 圓柱預設的 CapsuleCollider 會隨水平放大變成巨球，不能用於壓扁的地板。
            // 地板固定不動，使用與可見圓盤相同的靜態網格碰撞，頂面維持在 y = 0。
            ground.AddComponent<MeshCollider>().sharedMesh = ground.GetComponent<MeshFilter>().sharedMesh;
            Primitive("MentorSoil", level, PrimitiveType.Cylinder, Ground(data.mentorPosition) + Vector3.up * 0.015f,
                      new Vector3(2.5f, 0.01f, 2.5f), earth, false);
            BuildForest(level, data, bark, leaves);
            BuildWalls(level, data);
            BuildMountains(level, data, mountain);
            BuildAtmosphere(data);

            var spawns = new GameObject("Spawns").transform;
            var start = Anchor(spawns, "Start", data.playerSpawn);
            start.rotation = Quaternion.Euler(0f, data.playerFacing, 0f);
            var actors = new GameObject("ActorAnchors").transform;
            Anchor(actors, "Daisy", data.mentorPosition);
            Anchor(actors, "Sapling", data.saplingPosition);
            Anchor(actors, "Guardian", data.guardianPosition);

            PauseMenuBuilder.EnsureOptions(AssetDatabase.LoadAssetAtPath<GameOptionsData>(OptionsPath), out _);
            IntroCutsceneBuilder.BuildAudioManager(null);
            PlayerSpawnerBuilder.Build(start.position, start.rotation, ("Start", start));
            var camera = CameraRigBuilder.Build(PlayerSpawnerBuilder.Preview().Controller);
            UnityEngine.Camera.main.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            var session = new GameObject("Prologue").AddComponent<InputLockArbiter>();
            DaoDunDogBuilder.SetReference(session, "cameraController", camera);
            PauseMenuBuilder.Build();
            SceneFlowBuilder.Build();
            PrologueActorsBuilder.Build();
            PlayerSpawnerBuilder.Wire();
            EditorSceneManager.SaveScene(scene, ScenePath);
            TutorialBuilder.Build();
            // 正式演出已安裝的專案，完整重建時也接回既有守衛與二階段；資產數值仍保留。
            if (AssetDatabase.LoadAssetAtPath<BossFight.Cutscene.PrologueCutsceneData>(PrologueCutsceneBuilder.DataPath) != null &&
                AssetDatabase.LoadAssetAtPath<BossFight.Boss.BossPhaseData>(GuardianPhaseTwoBuilder.PhasePath) != null) GuardianBuilder.Build();
            AssetDatabase.SaveAssets();
            HubBuilder.SetBuildScenes();
            Debug.Log($"[Prologue] 已建立 {ScenePath}；空地半徑 {data.clearingRadius} m；Build Settings：Boot 0、Hub 1、BossArena 2、Prologue 3");
        }

        static void BuildForest(Transform parent, PrologueLayoutData data, Material bark, Material[] leaves)
        {
            var root = new GameObject("Forest").transform;
            root.SetParent(parent, false);
            var random = new System.Random(data.visualSeed);
            for (int row = 0; row < data.treeRows; row++)
            {
                float radius = data.clearingRadius + data.trunkRadius * 2f + row * data.rowSpacing;
                int count = Mathf.CeilToInt(Mathf.PI * 2f * radius / data.treeSpacing);
                for (int i = 0; i < count; i++)
                {
                    float angle = (i + 0.5f + row * 0.5f) / count * Mathf.PI * 2f;
                    var position = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * radius;
                    float height = Mathf.Lerp(data.treeHeight.x, data.treeHeight.y, (float)random.NextDouble());
                    var tree = new GameObject($"Tree_{row}_{i:00}").transform;
                    tree.SetParent(root, false);
                    tree.position = position;
                    Primitive("Trunk", tree, PrimitiveType.Cylinder, Vector3.up * height * 0.5f,
                              new Vector3(data.trunkRadius * 2f, height * 0.5f, data.trunkRadius * 2f), bark, true);
                    Primitive("Crown", tree, PrimitiveType.Sphere, Vector3.up * height * 0.8f,
                              new Vector3(3.8f, height * 0.65f, 3.8f), leaves[(i + row) % leaves.Length], false);
                }
            }
        }

        static void BuildWalls(Transform parent, PrologueLayoutData data)
        {
            var root = new GameObject("InvisibleWalls").transform;
            root.SetParent(parent, false);
            const int segments = 96; // 編輯器的圓周近似精度
            float radius = data.clearingRadius + data.wallOffset + data.wallThickness * 0.5f;
            for (int i = 0; i < segments; i++)
            {
                Vector3 Point(int index)
                {
                    float a = index * Mathf.PI * 2f / segments;
                    return new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * radius;
                }
                Vector3 a = Point(i), b = Point(i + 1), d = b - a;
                var wall = new GameObject($"Wall_{i:00}");
                wall.layer = LayerMask.NameToLayer("ArenaWall");
                wall.transform.SetParent(root, false);
                wall.transform.SetPositionAndRotation((a + b) * 0.5f + Vector3.up * data.wallHeight * 0.5f, Quaternion.LookRotation(d));
                wall.AddComponent<BoxCollider>().size = new Vector3(data.wallThickness, data.wallHeight, d.magnitude + data.wallThickness);
                wall.isStatic = true;
            }
        }

        static void BuildMountains(Transform parent, PrologueLayoutData data, Material material)
        {
            var meshPath = ModelDir + "/MountainCone.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (mesh == null)
            {
                mesh = new Mesh { name = "MountainCone" };
                const int sides = 8;
                var vertices = new Vector3[sides + 1];
                vertices[0] = Vector3.up;
                var triangles = new int[sides * 3];
                for (int i = 0; i < sides; i++)
                {
                    float a = i * Mathf.PI * 2f / sides;
                    vertices[i + 1] = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                    triangles[i * 3] = 0;
                    triangles[i * 3 + 1] = i + 1;
                    triangles[i * 3 + 2] = (i + 1) % sides + 1;
                }
                mesh.vertices = vertices;
                mesh.triangles = triangles;
                mesh.RecalculateNormals();
                AssetDatabase.CreateAsset(mesh, meshPath);
            }
            var root = new GameObject("Mountains").transform;
            root.SetParent(parent, false);
            var random = new System.Random(data.visualSeed);
            for (int i = 0; i < data.mountainCount; i++)
            {
                float a = i * Mathf.PI * 2f / data.mountainCount;
                var go = new GameObject($"Mountain_{i:00}", typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(root, false);
                go.transform.position = new Vector3(Mathf.Sin(a) * data.mountainRadius, -1f, Mathf.Cos(a) * data.mountainRadius);
                go.transform.localScale = new Vector3(17f, 14f + (float)random.NextDouble() * 14f, 17f);
                go.GetComponent<MeshFilter>().sharedMesh = mesh;
                go.GetComponent<MeshRenderer>().sharedMaterial = material;
                go.isStatic = true;
            }
        }

        static void BuildAtmosphere(PrologueLayoutData data)
        {
            var sun = new GameObject("Directional Light").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.shadows = LightShadows.Soft;
            sun.color = data.sunColor;
            sun.intensity = data.sunIntensity;
            sun.transform.rotation = Quaternion.Euler(data.sunAngles.x, data.sunAngles.y, 0f);
            var skyPath = ModelDir + "/AfternoonSky.mat";
            var sky = AssetDatabase.LoadAssetAtPath<Material>(skyPath);
            if (sky == null)
            {
                sky = new Material(Shader.Find("Skybox/Procedural")) { name = "PrologueAfternoonSky" };
                sky.SetColor("_SkyTint", new Color(0.58f, 0.73f, 0.85f));
                sky.SetFloat("_Exposure", 1.15f);
                AssetDatabase.CreateAsset(sky, skyPath);
            }
            RenderSettings.sun = sun;
            RenderSettings.skybox = sky;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.65f, 0.74f, 0.78f);
            RenderSettings.ambientEquatorColor = new Color(0.55f, 0.58f, 0.46f);
            RenderSettings.ambientGroundColor = new Color(0.25f, 0.3f, 0.2f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = data.fogColor;
            RenderSettings.fogDensity = data.fogDensity;
            var profilePath = ModelDir + "/AfternoonVolume.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, profilePath);
                var bloom = profile.Add<Bloom>();
                bloom.intensity.Override(data.bloomIntensity);
                var color = profile.Add<ColorAdjustments>();
                color.colorFilter.Override(new Color(1f, 0.97f, 0.91f));
                foreach (var component in profile.components) AssetDatabase.AddObjectToAsset(component, profile);
                EditorUtility.SetDirty(profile);
            }
            var volume = new GameObject("AfternoonVolume").AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;
        }

        static Vector3 Ground(Vector2 p) => new Vector3(p.x, 0f, p.y);

        static Transform Anchor(Transform parent, string name, Vector2 position)
        {
            var anchor = new GameObject(name).transform;
            anchor.SetParent(parent, false);
            anchor.position = Ground(position);
            return anchor;
        }

        static GameObject Primitive(string name, Transform parent, PrimitiveType type, Vector3 localPosition, Vector3 scale, Material material, bool collide)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = material;
            if (!collide) UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            go.isStatic = true;
            return go;
        }

        static Material Material(string name, Color color)
        {
            string path = $"{ModelDir}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = $"Prologue_{name}" };
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", 0.1f);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
    }
}
