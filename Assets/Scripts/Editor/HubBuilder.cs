using System.Collections.Generic;
using BossFight.Core;
using BossFight.Hub;
using BossFight.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BossFight.EditorTools
{
    /// <summary>
    /// Tools > Hub > Build Hub (T17)：建立空的休息區場景 Assets/Scenes/Hub.unity（每次整個重建，存檔前會先問要不要存目前的場景）。
    /// T17 只有：地板、四面隱形牆、方向光、紫色傳送門方塊（站在觸發區按 Interact 進 Boss 戰）、兩個出生點（Start＝之後營火的位置、Portal＝傳送門前）、
    /// PlayerSpawner（T18：預設生成在 Start，出生點清單 Start、Portal；底下有刀盾狗的 EditorOnly 預覽模型）與鏡頭（CameraRigBuilder）、GameOptions（改鍵、音量也在休息區套用）、AudioManager（沒有音樂，只播打擊音效）、SceneFlow（SceneFlowBuilder）。
    /// 也把 Build Settings 設成 Hub 0、BossArena 1。T20 會照 HubLayoutData 擴充成正式的休息區。
    /// 尺寸、位置是佔位場景的版面，不是玩法數值。
    /// </summary>
    public static class HubBuilder
    {
        public const string ScenePath = "Assets/Scenes/Hub.unity";
        const string BattleScenePath = "Assets/Scenes/BossArena.unity";
        const string PlayerPrefabPath = DaoDunDogBuilder.PrefabPath;
        const string OptionsDataPath = "Assets/ScriptableObjects/Options/GameOptionsData.asset";
        const string MatDir = "Assets/Models/Hub/Materials";

        // 版面（公尺）：原點是休息區中心，傳送門在南邊
        static readonly Vector2 GroundSize = new Vector2(30f, 40f);
        const float WallHeight = 4f, WallThickness = 1f;
        static readonly Vector3 StartSpawn = new Vector3(-8f, 0f, 0f);
        const float StartYaw = 90f;  // 面向東
        static readonly Vector3 PortalPosition = new Vector3(0f, 0f, -15f);
        static readonly Vector3 PortalSize = new Vector3(3f, 4f, 0.6f);
        static readonly Vector3 PortalTriggerSize = new Vector3(5f, 3f, 3f);
        const float PortalSpawnDistance = 5f; // 傳送門前的出生點離傳送門多遠（面向北，背對傳送門）

        [MenuItem("Tools/Hub/Build Hub (T17)")]
        public static void BuildFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Build();
        }

        public static void Build()
        {
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (playerPrefab == null)
            {
                Debug.LogError($"[Hub] 找不到 {PlayerPrefabPath}（Tools > Player > Build DaoDun Dog）。");
                return;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            DaoDunDogBuilder.EnsureFolder(MatDir);
            var groundMat = GetMat("Hub_Ground", new Color(0.32f, 0.42f, 0.24f), 0.1f);
            var portalMat = GetMat("Hub_Portal", new Color(0.55f, 0.25f, 0.95f), 0.6f, new Color(0.45f, 0.15f, 0.9f) * 1.5f);

            // 光
            var light = new GameObject("Directional Light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.86f, 0.7f);
            light.intensity = 1.2f;
            light.shadows = LightShadows.Soft;
            light.transform.rotation = Quaternion.Euler(40f, -30f, 0f);

            // 地板與隱形牆
            var level = new GameObject("Level").transform;
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Ground";
            ground.transform.SetParent(level, false);
            ground.transform.localPosition = new Vector3(0f, -0.5f, 0f);
            ground.transform.localScale = new Vector3(GroundSize.x, 1f, GroundSize.y);
            ground.GetComponent<MeshRenderer>().sharedMaterial = groundMat;
            ground.isStatic = true;
            var walls = new GameObject("InvisibleWalls").transform;
            walls.SetParent(level, false);
            float hx = GroundSize.x * 0.5f, hz = GroundSize.y * 0.5f;
            Wall(walls, "North", new Vector3(0f, WallHeight * 0.5f, hz + WallThickness * 0.5f), new Vector3(GroundSize.x, WallHeight, WallThickness));
            Wall(walls, "South", new Vector3(0f, WallHeight * 0.5f, -hz - WallThickness * 0.5f), new Vector3(GroundSize.x, WallHeight, WallThickness));
            Wall(walls, "East", new Vector3(hx + WallThickness * 0.5f, WallHeight * 0.5f, 0f), new Vector3(WallThickness, WallHeight, GroundSize.y));
            Wall(walls, "West", new Vector3(-hx - WallThickness * 0.5f, WallHeight * 0.5f, 0f), new Vector3(WallThickness, WallHeight, GroundSize.y));

            // 傳送門：實心的門（擋路）＋觸發區（Portal）
            var portal = new GameObject("Portal");
            portal.transform.position = PortalPosition;
            var trigger = portal.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = PortalTriggerSize;
            trigger.center = new Vector3(0f, PortalTriggerSize.y * 0.5f, PortalTriggerSize.z * 0.5f); // 門的北側（休息區那一邊）
            portal.AddComponent<Portal>();
            var gate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            gate.name = "Gate";
            gate.transform.SetParent(portal.transform, false);
            gate.transform.localPosition = new Vector3(0f, PortalSize.y * 0.5f, 0f);
            gate.transform.localScale = PortalSize;
            gate.GetComponent<MeshRenderer>().sharedMaterial = portalMat;

            // 出生點
            var spawns = new GameObject("Spawns").transform;
            var start = new GameObject("Start").transform;
            start.SetParent(spawns, false);
            start.SetPositionAndRotation(StartSpawn, Quaternion.Euler(0f, StartYaw, 0f));
            var portalSpawn = new GameObject("Portal").transform;
            portalSpawn.SetParent(spawns, false);
            portalSpawn.SetPositionAndRotation(PortalPosition + Vector3.forward * PortalSpawnDistance, Quaternion.identity);

            // 設定、音效、玩家、鏡頭
            PauseMenuBuilder.EnsureOptions(AssetDatabase.LoadAssetAtPath<GameOptionsData>(OptionsDataPath), out _);
            IntroCutsceneBuilder.BuildAudioManager(null);
            var flowData = AssetDatabase.LoadAssetAtPath<BossFight.Progression.SceneFlowData>(SceneFlowBuilder.DataPath);
            if (flowData == null) SceneFlowBuilder.Build(); // 建立 SceneFlowData（PauseMenuBuilder.Wire 之後會沿用這個 SceneFlow）
            flowData = AssetDatabase.LoadAssetAtPath<BossFight.Progression.SceneFlowData>(SceneFlowBuilder.DataPath);
            PlayerSpawnerBuilder.Build(start.position, start.rotation, (flowData.startArrival, start), (flowData.portalArrival, portalSpawn));
            CameraRigBuilder.Build(PlayerSpawnerBuilder.Preview().Controller); // 也會接打擊回饋（CombatFeedbackBuilder.Wire）
            PauseMenuBuilder.Wire(); // AudioManager 的音量設定；最後建 SceneFlow（SceneFlowBuilder.Build）並接 spawner

            EditorSceneManager.SaveScene(scene, ScenePath);
            SetBuildScenes();
            Debug.Log($"[Hub] 休息區已建立：{ScenePath}；Build Settings：Hub 0、BossArena 1");
        }

        /// <summary>Build Settings：Hub 0、BossArena 1（SampleScene 拿掉）。</summary>
        public static void SetBuildScenes()
        {
            EditorBuildSettings.scenes = new List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(ScenePath, true),
                new EditorBuildSettingsScene(BattleScenePath, true),
            }.ToArray();
        }

        static void Wall(Transform parent, string name, Vector3 position, Vector3 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.AddComponent<BoxCollider>().size = size;
            go.isStatic = true;
        }

        static Material GetMat(string name, Color color, float smoothness, Color? emission = null)
        {
            string path = $"{MatDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            mat = new Material(shader) { name = name };
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            if (emission.HasValue && mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", emission.Value);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }
    }
}
