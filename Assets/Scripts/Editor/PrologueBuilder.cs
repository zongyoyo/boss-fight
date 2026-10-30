using System;
using System.Linq;
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
        public const string ModelDir = "Assets/Models/Prologue";
        const string OptionsPath = "Assets/ScriptableObjects/Options/GameOptionsData.asset";
        public const string SkyMaterialPath = "Assets/Art/Materials/Sky_PrologueAfternoon.mat";
        const string SkyShader = "BossFight/Sky Gradient";
        const string AfternoonProfilePath = ModelDir + "/AfternoonVolume.asset";

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
            var earth = Material("Earth", new Color(0.62f, 0.53f, 0.34f));
            var level = new GameObject("Level").transform;
            // 地面、森林、草叢、道具、遠山（美術 Phase 2）；地面網格的碰撞頂面在 y = 0
            Debug.Log(PrologueSceneryBuilder.Build(level, data, FallbackMaterials()));
            Primitive("MentorSoil", level, PrimitiveType.Cylinder, Ground(data.mentorPosition) + Vector3.up * 0.015f,
                      new Vector3(2.5f, 0.01f, 2.5f), earth, false);
            BuildWalls(level, data);
            ApplyAtmosphere(data);

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

        /// <summary>佈景沒有正式模型時用的佔位材質（只在不存在時建立）。</summary>
        public static PrologueSceneryBuilder.Fallback FallbackMaterials() => new PrologueSceneryBuilder.Fallback
        {
            grass = Material("Grass", new Color(0.32f, 0.48f, 0.24f)),
            bark = Material("Bark", new Color(0.29f, 0.2f, 0.12f)),
            leaves = new[] { Material("Leaves0", new Color(0.16f, 0.36f, 0.16f)),
                             Material("Leaves1", new Color(0.24f, 0.43f, 0.2f)),
                             Material("Leaves2", new Color(0.12f, 0.3f, 0.19f)) },
            mountain = Material("Mountain", new Color(0.42f, 0.49f, 0.48f)),
        };

        /// <summary>佔位遠山的八邊圓錐（沒有正式山模型時用；只在不存在時建立）。</summary>
        public static Mesh MountainCone()
        {
            var meshPath = ModelDir + "/MountainCone.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (mesh != null) return mesh;
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
            return mesh;
        }

        /// <summary>Tools > Prologue > Apply Atmosphere：只把 PrologueLayoutData 的天色套到開著的序章場景（不重建場景）並存檔。</summary>
        [MenuItem("Tools/Prologue/Apply Atmosphere")]
        public static void ApplyAtmosphereFromMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                Debug.LogError("[Prologue] 請先開啟 Prologue 場景。");
                return;
            }
            ApplyAtmosphere(AssetDatabase.LoadAssetAtPath<PrologueLayoutData>(LayoutPath));
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[Prologue] 已套用天色（主光、漸層天空、環境光、霧、後處理）。");
        }

        /// <summary>
        /// 午後天色（docs/Art/ART_STYLE.md「光照與後處理」）：主光、三色漸層天空（地平線色 = 霧色）、Trilight 環境光、指數平方霧、
        /// 兩層全域 Volume（BaseVolume 優先度 0 是三個場景共用的 VP_LowPolyBase，AfternoonVolume 優先度 1 放序章自己的覆寫）。
        /// 場景裡已有的主光與 Volume 原地更新、不重建：序章演出（PrologueCinematicStage）引用這盞主光。
        /// </summary>
        public static void ApplyAtmosphere(PrologueLayoutData data)
        {
            var sun = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None).FirstOrDefault(l => l.type == LightType.Directional);
            if (sun == null)
            {
                sun = new GameObject("Directional Light").AddComponent<Light>();
                sun.type = LightType.Directional;
            }
            sun.shadows = LightShadows.Soft;
            sun.color = data.sunColor;
            sun.intensity = data.sunIntensity;
            sun.transform.rotation = Quaternion.Euler(data.sunAngles.x, data.sunAngles.y, 0f);
            var sky = AssetDatabase.LoadAssetAtPath<Material>(SkyMaterialPath);
            if (sky == null)
            {
                sky = new Material(Shader.Find(SkyShader)) { name = "Sky_PrologueAfternoon" };
                AssetDatabase.CreateAsset(sky, SkyMaterialPath);
            }
            // 天空顏色以 PrologueLayoutData 為準，每次套用都寫回材質
            sky.SetColor("_TopColor", data.skyTopColor);
            sky.SetColor("_HorizonColor", data.fogColor);
            sky.SetColor("_BottomColor", data.skyBottomColor);
            EditorUtility.SetDirty(sky);
            RenderSettings.sun = sun;
            RenderSettings.skybox = sky;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = data.ambientSky;
            RenderSettings.ambientEquatorColor = data.ambientEquator;
            RenderSettings.ambientGroundColor = data.ambientGround;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = data.fogColor;
            RenderSettings.fogDensity = data.fogDensity;
            var afternoon = AssetDatabase.LoadAssetAtPath<VolumeProfile>(AfternoonProfilePath);
            if (afternoon == null)
            {
                afternoon = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(afternoon, AfternoonProfilePath);
            }
            EnsureVolume("BaseVolume", 0f, ArtTools.EnsureBaseVolumeProfile());
            EnsureVolume("AfternoonVolume", 1f, afternoon);
            DynamicGI.UpdateEnvironment();
            EditorSceneManager.MarkSceneDirty(sun.gameObject.scene);
        }

        static void EnsureVolume(string name, float priority, VolumeProfile profile)
        {
            var volume = SceneManager.GetActiveScene().GetRootGameObjects()
                .Where(go => go.name == name).Select(go => go.GetComponent<Volume>()).FirstOrDefault(v => v != null);
            if (volume == null) volume = new GameObject(name).AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = priority;
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
