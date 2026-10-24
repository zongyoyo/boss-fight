using System.Collections.Generic;
using BossFight.Battle;
using BossFight.Boss;
using BossFight.Cameras;
using BossFight.Core;
using BossFight.Cutscene;
using BossFight.Hazards;
using BossFight.Player;
using BossFight.UI;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Timeline;
using UnityEngine.UI;

namespace BossFight.EditorTools
{
    /// <summary>
    /// 建立開場動畫（T6）。選單 Tools > Cutscene > Build Intro，可以重複執行：
    /// 1. 資產（只在不存在時建立，之後在 Inspector 改的不會被蓋掉）：IntroCutsceneSettings、Atmosphere（含 Sky_CalmPlaceholder 天空盒）、
    ///    魔法陣／雷雲／煙霧材質（Models/Cutscene/Materials）、Timeline（Assets/Timelines/Intro.playable，照企劃書分鏡排好）。
    ///    佔位音效每次重建（PlaceholderAudioBuilder）。
    /// 2. 場景物件（每次刪掉重建）：MagicCircle、Atmosphere（雷雲）、AudioManager、IntroCutscene（PlayableDirector、CutsceneDirector、
    ///    8 台演出鏡頭、看向的目標、閃電、煙霧、白屏、Timeline 用的 AudioSource）、IntroUI（字卡、跳過提示）、BattleManager。
    /// 3. 把 Timeline 的每條軌道綁到場景物件（依軌道類型；同類型有好幾條的依軌道名稱），Cinemachine 片段依片段名稱接到同名的鏡頭。
    ///    所以在 Timeline 視窗調時間點、加減片段都沒關係，但不要改軌道與鏡頭片段的名稱。
    /// 4. BossController.startOnPlay 關掉（場景與 prefab），改由 BattleManager 在開場結束時開打；重建的魔法陣接到 Boss 的幻影分身與大迴旋。
    /// 5. 最後重建轉階段演出（TransitionCutsceneBuilder）與戰鬥 UI（BattleUIBuilder），因為它們引用這裡重建的魔法陣、AudioManager、BattleManager 與開場；打擊回饋（CombatFeedbackBuilder）也重接到新的 AudioManager。
    /// Tools > Cutscene > Reset Intro Timeline 會刪掉 Timeline 重排一次（在 Timeline 視窗做的調整會不見）。
    /// 鏡頭位置、顏色、大小都是純視覺參數。
    /// </summary>
    public static class IntroCutsceneBuilder
    {
        const string TimelinePath = "Assets/Timelines/Intro.playable";
        const string SettingsPath = "Assets/ScriptableObjects/Cutscene/IntroCutsceneSettings.asset";
        const string AtmospherePath = "Assets/ScriptableObjects/Arena/Atmosphere.asset";
        const string ArenaDataPath = "Assets/ScriptableObjects/Arena/ArenaData.asset";
        const string SkyStormPath = "Assets/Models/Arena/Materials/Sky_StormPlaceholder.mat";
        const string SkyCalmPath = "Assets/Models/Arena/Materials/Sky_CalmPlaceholder.mat";
        const string MatDir = "Assets/Models/Cutscene/Materials";
        const string CircleShaderPath = "Assets/Shaders/MagicCircleDecal.shadergraph";
        const string BoltPrefabPath = "Assets/Prefabs/Hazards/Fx_Bolt.prefab";
        const string BossPrefabPath = "Assets/Prefabs/OiiaCat.prefab";
        const string NoisePath = "Packages/com.unity.cinemachine/Presets/Noise/6D Shake.asset";
        const string MusicKeyword = "oiia_bgm";

        // 場景物件
        const string IntroName = "IntroCutscene";
        const string CircleName = "MagicCircle";
        const string AtmosphereName = "Atmosphere";
        const string AudioName = "AudioManager";
        const string UiName = "IntroUI";
        const string BattleName = "BattleManager";

        // 鏡頭（Cinemachine 片段用同樣的名稱對應）
        const string Shot1 = "Shot1_Overview";
        const string Shot2 = "Shot2_Descend";
        const string Shot3 = "Shot3_Storm";
        const string Shot4 = "Shot4_CatMid";
        const string Shot5a = "Shot5_CatFace";
        const string Shot5b = "Shot5_DogFace";
        const string Shot5c = "Shot5_Side";
        const string Shot6 = "Shot6_PullBack";

        // 軌道名稱（同類型有好幾條時用名稱綁定）
        const string TrackShot1Move = "Shot1 Orbit";
        const string TrackShot2Move = "Shot2 Descend";
        const string TrackLightning = "Lightning";
        const string TrackFlash = "White Flash";
        const string TrackShake = "Camera Shake";
        const string TrackSmoke = "Smoke";
        const string TrackFootsteps = "Footsteps";
        const string TrackWind = "Wind";
        const string TrackHum = "Hum";
        const string TrackThunder = "Thunder";

        const int ShotPriority = -10;

        /// <summary>依場地算好的位置：玩家從 PlayerGate 進場，fwd 是玩家看向 Boss 的方向。</summary>
        struct Layout
        {
            public Vector3 center, fwd, right, walkFrom, spawn;
            public float gateAngle;

            /// x 往右、y 往上、z 往 Boss（以場地中心為原點）
            public Vector3 P(float x, float y, float z) => center + right * x + Vector3.up * y + fwd * z;
        }

        [MenuItem("Tools/Cutscene/Build Intro")]
        public static void BuildFromMenu()
        {
            var go = Build();
            if (go != null) Selection.activeGameObject = go;
        }

        [MenuItem("Tools/Cutscene/Reset Intro Timeline")]
        public static void ResetTimeline()
        {
            AssetDatabase.DeleteAsset(TimelinePath);
            BuildFromMenu();
        }

        public static GameObject Build()
        {
            var player = Object.FindAnyObjectByType<PlayerController>();
            var cameraController = Object.FindAnyObjectByType<CameraController>();
            var boss = Object.FindAnyObjectByType<BossController>();
            var brain = Object.FindAnyObjectByType<CinemachineBrain>();
            var arena = AssetDatabase.LoadAssetAtPath<ArenaData>(ArenaDataPath);
            Light sun = null;
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (l.type == LightType.Directional) sun = l;
            }
            if (player == null || cameraController == null || boss == null || brain == null || arena == null || sun == null)
            {
                Debug.LogError("[Cutscene] 需要場景裡的 PlayerController、CameraController、BossController、CinemachineBrain、Directional Light，" +
                               $"以及 {ArenaDataPath}。請先建好競技場、刀盾狗、鏡頭與 Boss。");
                return null;
            }
            var poser = boss.GetComponent<OiiaCatPoser>();
            if (poser == null || poser.model == null)
            {
                Debug.LogError("[Cutscene] Boss 身上沒有 OiiaCatPoser（或沒有指定 model）。");
                return null;
            }

            // ---------- 資產 ----------
            var settings = GetOrCreate<IntroCutsceneSettings>(SettingsPath);
            var atmosphereData = GetAtmosphere();
            var sounds = PlaceholderAudioBuilder.Build();
            var circleMat = GetCircleMaterial(arena);
            var cloudMat = GetUnlitMaterial("StormCloud", new Color(0.09f, 0.1f, 0.13f));
            var smokeMat = GetSmokeMaterial();
            var boltPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BoltPrefabPath);
            if (boltPrefab == null)
            {
                HazardBuilder.Build();
                boltPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BoltPrefabPath);
            }
            if (atmosphereData == null || circleMat == null || boltPrefab == null) return null;

            Undo.SetCurrentGroupName("Build Intro Cutscene");
            int undoGroup = Undo.GetCurrentGroup();

            // 重建前記下已經指定的音樂
            AudioClip music = null;
            var oldAudio = Object.FindAnyObjectByType<AudioManager>();
            if (oldAudio != null) music = new SerializedObject(oldAudio).FindProperty("battleMusic").objectReferenceValue as AudioClip;
            if (music == null) music = FindMusic();

            foreach (var name in new[] { IntroName, CircleName, AtmosphereName, AudioName, UiName, BattleName })
            {
                var old = GameObject.Find(name);
                if (old != null) Undo.DestroyObjectImmediate(old);
            }

            var layout = GetLayout(arena);
            var atmosphere = BuildAtmosphere(atmosphereData, sun, cloudMat, layout);
            var circle = BuildCircle(arena, circleMat, layout);
            var audio = BuildAudioManager(music);

            // ---------- 開場 ----------
            var root = new GameObject(IntroName);
            Undo.RegisterCreatedObjectUndo(root, "Build Intro Cutscene");
            var director = root.AddComponent<PlayableDirector>();
            director.playOnAwake = false;
            director.extrapolationMode = DirectorWrapMode.None;
            director.timeUpdateMode = DirectorUpdateMode.GameTime;
            var cutscene = root.AddComponent<CutsceneDirector>();
            DaoDunDogBuilder.SetReference(cutscene, "settings", settings);
            DaoDunDogBuilder.SetReference(cutscene, "player", player);
            DaoDunDogBuilder.SetReference(cutscene, "cameraController", cameraController);
            DaoDunDogBuilder.SetReference(cutscene, "faceTarget", boss.transform);

            var targets = BuildTargets(root.transform, layout, boss, player);
            var freeLook = new SerializedObject(cameraController).FindProperty("freeLookCamera").objectReferenceValue as CinemachineCamera;
            var shots = BuildShots(root.transform, layout, targets, freeLook);
            var fx = DaoDunDogBuilder.Node(root.transform, "Fx", Vector3.zero);
            var lightning = BuildLightning(fx, boltPrefab, layout);
            var smoke = BuildSmoke(fx, smokeMat, layout.center);
            var flash = BuildFlash(root.transform);
            var shaker = shots[Shot3].GetComponent<CameraShaker>();
            var sources = BuildCutsceneAudio(root.transform);

            BuildUI(cutscene, settings);

            var battleGo = new GameObject(BattleName);
            Undo.RegisterCreatedObjectUndo(battleGo, "Build Intro Cutscene");
            var battle = battleGo.AddComponent<BattleManager>();
            DaoDunDogBuilder.SetReference(battle, "intro", cutscene);
            DaoDunDogBuilder.SetReference(battle, "boss", boss);
            DaoDunDogBuilder.SetReference(battle, "audioManager", audio);

            DisableStartOnPlay(boss);
            BossBuilder.WireMagicCircle(boss, circle); // 幻影分身、大迴旋引用重建後的魔法陣

            // ---------- Timeline ----------
            var timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(TimelinePath);
            if (timeline == null) timeline = CreateTimeline(layout, sounds);
            director.playableAsset = timeline;

            var named = new Dictionary<string, Object>
            {
                { TrackShot1Move, shots[Shot1].transform },
                { TrackShot2Move, shots[Shot2].transform },
                { TrackLightning, lightning },
                { TrackFlash, flash },
                { TrackShake, shaker },
                { TrackSmoke, smoke },
                { TrackFootsteps, sources[TrackFootsteps] },
                { TrackWind, sources[TrackWind] },
                { TrackHum, sources[TrackHum] },
                { TrackThunder, sources[TrackThunder] },
            };
            Bind(director, timeline, brain, shots, named, player, circle, atmosphere, poser, audio);

            // 轉階段演出引用這裡重建的 MagicCircle、AudioManager、BattleManager，一起重建
            TransitionCutsceneBuilder.Build();
            // 戰鬥 UI 引用這裡重建的 BattleManager 與開場（字卡），BattleManager 的 T9 欄位也在那邊接
            BattleUIBuilder.Build();
            // 打擊回饋的音效經這裡重建的 AudioManager 播放
            CombatFeedbackBuilder.Wire();

            Undo.CollapseUndoOperations(undoGroup);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(root.scene);
            Debug.Log($"[Cutscene] 開場動畫已建立：{TimelinePath}（{timeline.duration:0.0} 秒），" +
                      $"戰鬥音樂 {(music != null ? AssetDatabase.GetAssetPath(music) : "還沒有（AudioManager.battleMusic 留空）")}");
            return root;
        }

        // ---------- 位置 ----------

        static Layout GetLayout(ArenaData arena)
        {
            var gen = Object.FindAnyObjectByType<ArenaGenerator>();
            float angle = 180f;
            if (arena.TryGetGate("PlayerGate", out var gate)) angle = gate.angle;
            var L = new Layout
            {
                center = gen != null ? gen.transform.position : Vector3.zero,
                gateAngle = angle,
            };
            Vector3 gateDir = ArenaData.Dir(angle);
            L.fwd = -gateDir;
            L.right = Vector3.Cross(Vector3.up, L.fwd);
            L.spawn = L.center + gateDir * Mathf.Clamp(arena.playerSpawnDistance, 0f, arena.radius);
            L.walkFrom = L.center + gateDir * (arena.radius - 0.4f); // 入口門內側
            return L;
        }

        static Dictionary<string, Transform> BuildTargets(Transform root, Layout L, BossController boss, PlayerController player)
        {
            var parent = DaoDunDogBuilder.Node(root, "Targets", Vector3.zero);
            var t = new Dictionary<string, Transform>();
            Transform Add(string name, Vector3 pos)
            {
                var node = DaoDunDogBuilder.Node(parent, name, Vector3.zero);
                node.position = pos;
                t[name] = node;
                return node;
            }

            // 貓臉：兩眼中間稍低；狗臉：頭在角色上的位置，換到出生點、面向 Boss 時的位置
            Vector3 catFace = L.P(0f, 1.85f, -0.6f);
            var eyeL = boss.transform.Find("Model/EyeL");
            var eyeR = boss.transform.Find("Model/EyeR");
            if (eyeL != null && eyeR != null) catFace = (eyeL.position + eyeR.position) * 0.5f + Vector3.down * 0.12f;
            Vector3 dogFace = L.spawn + Vector3.up;
            var head = player.transform.Find("Model/Body/Head");
            if (head != null)
            {
                Vector3 local = player.transform.InverseTransformPoint(head.position);
                dogFace = L.spawn + Quaternion.LookRotation(L.fwd) * local + Vector3.up * 0.15f; // 頭的中心稍上，眼睛才會在畫面裡
            }

            Add("OverviewFocus", L.P(0f, 0f, -3f));
            Add("CircleCenter", L.center);
            Add("SkyFocus", L.P(0f, 4.5f, 0f));
            Add("CatBody", L.P(0f, 1.3f, 0f));
            Add("CatFace", catFace);
            Add("DogFace", dogFace);
            Add("Standoff", (L.center + L.spawn) * 0.5f + Vector3.up * 1.1f);
            return t;
        }

        // ---------- 鏡頭 ----------

        static Dictionary<string, CinemachineCamera> BuildShots(Transform root, Layout L, Dictionary<string, Transform> t, CinemachineCamera freeLook)
        {
            var parent = DaoDunDogBuilder.Node(root, "Shots", Vector3.zero);
            var lens = freeLook != null ? freeLook.Lens : LensSettings.Default;
            var shots = new Dictionary<string, CinemachineCamera>();

            Vector3 catFace = t["CatFace"].position;
            Vector3 dogFace = t["DogFace"].position;
            shots[Shot1] = Shot(parent, Shot1, OrbitPoint(L, 24f, 17f, L.gateAngle + 35f), t["OverviewFocus"], 50f, lens);
            shots[Shot2] = Shot(parent, Shot2, L.P(0f, 28f, -5f), t["CircleCenter"], 55f, lens);
            shots[Shot3] = Shot(parent, Shot3, L.P(-6f, 1f, -10.5f), t["SkyFocus"], 60f, lens);
            shots[Shot4] = Shot(parent, Shot4, L.P(0.8f, 1.6f, -5.5f), t["CatBody"], 50f, lens);
            shots[Shot5a] = Shot(parent, Shot5a, catFace - L.fwd * 1.9f + Vector3.down * 0.15f, t["CatFace"], 38f, lens);
            shots[Shot5b] = Shot(parent, Shot5b, dogFace + L.fwd * 2.1f + Vector3.up * 0.05f, t["DogFace"], 38f, lens);
            // 從左側（玩家入場方向的左邊）拍：T0 的比例參考膠囊在出生點右側，這樣會被狗擋在後面
            shots[Shot5c] = Shot(parent, Shot5c, t["Standoff"].position - L.right * 10f + Vector3.up * 0.3f, t["Standoff"], 45f, lens);
            shots[Shot6] = Shot(parent, Shot6, L.P(1.8f, 1.4f, -3.4f), t["CatBody"], 45f, lens);

            // 第 3 鏡：閃電劈下時震動（CameraShaker 由 Timeline 推 Perlin 雜訊的強度）
            var cam3 = shots[Shot3];
            var noise = cam3.gameObject.AddComponent<CinemachineBasicMultiChannelPerlin>();
            noise.NoiseProfile = AssetDatabase.LoadAssetAtPath<NoiseSettings>(NoisePath);
            noise.AmplitudeGain = 0f;
            noise.FrequencyGain = 2f;
            if (noise.NoiseProfile == null) Debug.LogWarning($"[Cutscene] 找不到 {NoisePath}，第 3 鏡的震動要手動指定 Noise Profile。");
            var shaker = cam3.gameObject.AddComponent<CameraShaker>();
            DaoDunDogBuilder.SetReference(shaker, "noise", noise);
            return shots;
        }

        static Vector3 OrbitPoint(Layout L, float radius, float height, float angle) =>
            L.center + ArenaData.Dir(angle) * radius + Vector3.up * height;

        internal static CinemachineCamera Shot(Transform parent, string name, Vector3 pos, Transform lookAt, float fov, LensSettings lens)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(lookAt.position - pos));
            var cam = go.AddComponent<CinemachineCamera>();
            cam.Priority = ShotPriority; // 只有 Timeline 會用到，平常不會搶走玩家鏡頭
            lens.FieldOfView = fov;
            cam.Lens = lens;
            cam.LookAt = lookAt;
            go.AddComponent<CinemachineHardLookAt>();
            return cam;
        }

        // ---------- 效果 ----------

        static IntroLightning BuildLightning(Transform parent, GameObject boltPrefab, Layout L)
        {
            var go = DaoDunDogBuilder.Node(parent, "Lightning", Vector3.zero);
            go.position = L.center;
            var src = boltPrefab.GetComponentInChildren<MeshFilter>();
            var srcRenderer = boltPrefab.GetComponentInChildren<MeshRenderer>();

            var bolts = new List<Renderer>();
            for (int i = 0; i < 2; i++)
            {
                var bolt = new GameObject(i == 0 ? "Bolt" : "BoltCore");
                bolt.transform.SetParent(go, false);
                bolt.transform.localRotation = Quaternion.Euler(0f, i * 47f, 0f);
                bolt.transform.localScale = i == 0 ? new Vector3(3.5f, 45f, 3.5f) : new Vector3(1.6f, 45f, 1.6f);
                bolt.AddComponent<MeshFilter>().sharedMesh = src.sharedMesh;
                var r = bolt.AddComponent<MeshRenderer>();
                r.sharedMaterial = srcRenderer.sharedMaterial;
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
                r.enabled = false;
                bolts.Add(r);
            }

            var lightGo = new GameObject("FlashLight");
            lightGo.transform.SetParent(go, false);
            lightGo.transform.localPosition = Vector3.up * 6f;
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 40f;
            light.shadows = LightShadows.None;
            light.enabled = false;

            var lightning = go.gameObject.AddComponent<IntroLightning>();
            var so = new SerializedObject(lightning);
            SetArray(so.FindProperty("bolts"), bolts);
            so.FindProperty("flashLight").objectReferenceValue = light;
            so.ApplyModifiedPropertiesWithoutUndo();
            return lightning;
        }

        internal static SmokePuffs BuildSmoke(Transform parent, Material mat, Vector3 center)
        {
            var go = DaoDunDogBuilder.Node(parent, "Smoke", Vector3.zero);
            go.position = center;
            var rnd = new System.Random(7);
            var puffs = new List<Renderer>();
            for (int i = 0; i < 9; i++)
            {
                float a = i * 40f + (float)rnd.NextDouble() * 15f;
                float r = 0.9f + (float)rnd.NextDouble() * 0.8f;
                float y = 0.5f + (float)rnd.NextDouble() * 1.4f;
                float s = 1.6f + (float)rnd.NextDouble() * 1f;
                var puff = DaoDunDogBuilder.Prim(PrimitiveType.Sphere, go, $"Puff{i}", ArenaData.Dir(a) * r + Vector3.up * y, Vector3.one * s, mat);
                var mr = puff.GetComponent<MeshRenderer>();
                mr.shadowCastingMode = ShadowCastingMode.Off;
                mr.receiveShadows = false;
                mr.enabled = false;
                puffs.Add(mr);
            }
            var smoke = go.gameObject.AddComponent<SmokePuffs>();
            var so = new SerializedObject(smoke);
            SetArray(so.FindProperty("puffs"), puffs);
            so.ApplyModifiedPropertiesWithoutUndo();
            return smoke;
        }

        internal static ScreenFlash BuildFlash(Transform root)
        {
            var canvasGo = new GameObject("Overlay", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(root, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100; // 蓋在所有 UI 上面

            var go = new GameObject("WhiteFlash", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
            go.transform.SetParent(canvasGo.transform, false);
            Stretch(go.GetComponent<RectTransform>());
            var image = go.GetComponent<Image>();
            image.color = Color.white;
            image.raycastTarget = false;
            var group = go.GetComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;
            var flash = go.AddComponent<ScreenFlash>();
            DaoDunDogBuilder.SetReference(flash, "group", group);
            return flash;
        }

        static Dictionary<string, AudioSource> BuildCutsceneAudio(Transform root)
        {
            var parent = DaoDunDogBuilder.Node(root, "Audio", Vector3.zero);
            var d = new Dictionary<string, AudioSource>();
            AudioSource Add(string track, float volume)
            {
                var src = DaoDunDogBuilder.Node(parent, "Audio_" + track.Replace(" ", ""), Vector3.zero).gameObject.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.spatialBlend = 0f;
                src.volume = volume;
                d[track] = src;
                return src;
            }
            Add(TrackFootsteps, 0.8f);
            Add(TrackWind, 0.6f);
            Add(TrackHum, 0.9f);
            Add(TrackThunder, 1f);
            return d;
        }

        // ---------- 魔法陣、天色、音樂 ----------

        static MagicCircle BuildCircle(ArenaData arena, Material mat, Layout L)
        {
            var go = new GameObject(CircleName);
            Undo.RegisterCreatedObjectUndo(go, "Build Intro Cutscene");
            go.transform.SetPositionAndRotation(L.center, Quaternion.LookRotation(Vector3.down, Vector3.forward));
            var projector = go.AddComponent<DecalProjector>();
            projector.material = mat;
            float d = arena.magicCircleRadius * 2f;
            projector.size = new Vector3(d, d, 0.3f);
            projector.pivot = Vector3.zero;
            projector.startAngleFade = 40f;
            projector.endAngleFade = 70f;
            var circle = go.AddComponent<MagicCircle>();
            DaoDunDogBuilder.SetReference(circle, "projector", projector);
            DaoDunDogBuilder.SetReference(circle, "arena", arena);
            return circle;
        }

        static ArenaAtmosphere BuildAtmosphere(AtmosphereData data, Light sun, Material cloudMat, Layout L)
        {
            var root = new GameObject(AtmosphereName);
            Undo.RegisterCreatedObjectUndo(root, "Build Intro Cutscene");
            root.transform.position = L.center;
            var cloudsRoot = DaoDunDogBuilder.Node(root.transform, "StormClouds", Vector3.zero);

            // 雷雲：一圈深色扁球團，聚集在看台上空；ArenaAtmosphere 在暴風進度 0 時把它們推到遠處、縮小
            var rnd = new System.Random(5);
            var clouds = new List<Transform>();
            for (int i = 0; i < 16; i++)
            {
                float a = i * 22.5f + ((float)rnd.NextDouble() - 0.5f) * 14f;
                float r = 20f + (float)rnd.NextDouble() * 12f;
                float h = 27f + (float)rnd.NextDouble() * 6f;
                var cloud = DaoDunDogBuilder.Node(cloudsRoot, $"Cloud{i}", ArenaData.Dir(a) * r + Vector3.up * h);
                cloud.localRotation = Quaternion.Euler(0f, (float)rnd.NextDouble() * 360f, 0f);
                for (int k = 0; k < 3; k++)
                {
                    var offset = new Vector3(((float)rnd.NextDouble() - 0.5f) * 10f, (float)rnd.NextDouble() * 1.5f, ((float)rnd.NextDouble() - 0.5f) * 6f);
                    var scale = new Vector3(12f + (float)rnd.NextDouble() * 7f, 4f + (float)rnd.NextDouble() * 2.5f, 8f + (float)rnd.NextDouble() * 5f);
                    var puff = DaoDunDogBuilder.Prim(PrimitiveType.Sphere, cloud, $"Puff{k}", offset, scale, cloudMat);
                    var mr = puff.GetComponent<MeshRenderer>();
                    mr.shadowCastingMode = ShadowCastingMode.Off;
                    mr.receiveShadows = false;
                }
                clouds.Add(cloud);
            }

            var atmosphere = root.AddComponent<ArenaAtmosphere>();
            var so = new SerializedObject(atmosphere);
            so.FindProperty("data").objectReferenceValue = data;
            so.FindProperty("sun").objectReferenceValue = sun;
            SetArray(so.FindProperty("clouds"), clouds);
            so.ApplyModifiedPropertiesWithoutUndo();
            return atmosphere;
        }

        static AudioManager BuildAudioManager(AudioClip music)
        {
            var root = new GameObject(AudioName);
            Undo.RegisterCreatedObjectUndo(root, "Build Intro Cutscene");
            AudioSource Source(string name, bool loop)
            {
                var src = DaoDunDogBuilder.Node(root.transform, name, Vector3.zero).gameObject.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.loop = loop;
                src.spatialBlend = 0f;
                return src;
            }
            var musicSource = Source("Music", true);
            musicSource.priority = 0;
            var sfxSource = Source("Sfx", false);

            var audio = root.AddComponent<AudioManager>();
            var so = new SerializedObject(audio);
            so.FindProperty("musicSource").objectReferenceValue = musicSource;
            so.FindProperty("sfxSource").objectReferenceValue = sfxSource;
            so.FindProperty("battleMusic").objectReferenceValue = music;
            so.ApplyModifiedPropertiesWithoutUndo();
            return audio;
        }

        // Assets/Audio 底下路徑含 oiia_bgm 的第一個 AudioClip（資料夾或檔名都算）
        static AudioClip FindMusic()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Audio")) return null;
            foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/Audio" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.Contains(MusicKeyword)) return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            }
            return null;
        }

        // ---------- UI ----------

        static void BuildUI(CutsceneDirector cutscene, IntroCutsceneSettings settings)
        {
            var root = new GameObject(UiName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            Undo.RegisterCreatedObjectUndo(root, "Build Intro Cutscene");
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); // 內建字型會借用系統字型顯示中文

            // 字卡
            var card = UiNode("TitleCard", root.transform, typeof(CanvasGroup));
            Stretch(card);
            var cardGroup = card.GetComponent<CanvasGroup>();
            cardGroup.alpha = 0f;
            cardGroup.blocksRaycasts = false;
            var panel = UiNode("Panel", card);
            panel.sizeDelta = new Vector2(1400f, 400f);
            panel.anchoredPosition = new Vector2(0f, 140f);
            var title = Label("Title", panel, font, settings.title, 140, FontStyle.Bold, new Color(1f, 0.93f, 0.78f), TextAnchor.MiddleCenter);
            title.rectTransform.sizeDelta = new Vector2(1400f, 180f);
            title.rectTransform.anchoredPosition = new Vector2(0f, 60f);
            var subtitle = Label("BossName", panel, font, settings.bossName, 64, FontStyle.Bold, new Color(0.86f, 0.66f, 1f), TextAnchor.MiddleCenter);
            subtitle.rectTransform.sizeDelta = new Vector2(1400f, 90f);
            subtitle.rectTransform.anchoredPosition = new Vector2(0f, -85f);
            var cardUi = card.gameObject.AddComponent<TitleCardUI>();
            var cso = new SerializedObject(cardUi);
            cso.FindProperty("source").objectReferenceValue = cutscene;
            cso.FindProperty("group").objectReferenceValue = cardGroup;
            cso.FindProperty("panel").objectReferenceValue = panel;
            cso.FindProperty("titleText").objectReferenceValue = title;
            cso.FindProperty("subtitleText").objectReferenceValue = subtitle;
            cso.ApplyModifiedPropertiesWithoutUndo();

            // 跳過提示（右下角）
            var skip = UiNode("SkipPrompt", root.transform, typeof(CanvasGroup));
            skip.anchorMin = skip.anchorMax = skip.pivot = new Vector2(1f, 0f);
            skip.sizeDelta = new Vector2(380f, 80f);
            skip.anchoredPosition = new Vector2(-48f, 40f);
            var skipGroup = skip.GetComponent<CanvasGroup>();
            skipGroup.alpha = 0f;
            skipGroup.blocksRaycasts = false;
            var label = Label("Label", skip, font, "長按 Space 跳過", 30, FontStyle.Normal, Color.white, TextAnchor.LowerRight);
            label.rectTransform.anchorMin = new Vector2(0f, 0f);
            label.rectTransform.anchorMax = new Vector2(1f, 1f);
            label.rectTransform.offsetMin = new Vector2(0f, 20f);
            label.rectTransform.offsetMax = Vector2.zero;
            var sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            var bar = UiNode("Bar", skip, typeof(CanvasRenderer), typeof(Image));
            bar.anchorMin = new Vector2(0f, 0f);
            bar.anchorMax = new Vector2(1f, 0f);
            bar.pivot = new Vector2(0.5f, 0f);
            bar.sizeDelta = new Vector2(0f, 8f);
            bar.anchoredPosition = Vector2.zero;
            var barImage = bar.GetComponent<Image>();
            barImage.color = new Color(0f, 0f, 0f, 0.55f);
            barImage.raycastTarget = false;
            var fillRect = UiNode("Fill", bar, typeof(CanvasRenderer), typeof(Image));
            Stretch(fillRect);
            var fill = fillRect.GetComponent<Image>();
            fill.sprite = sprite;
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 0f;
            fill.color = new Color(1f, 0.93f, 0.78f);
            fill.raycastTarget = false;
            var skipUi = skip.gameObject.AddComponent<SkipPromptUI>();
            var sso = new SerializedObject(skipUi);
            SetArray(sso.FindProperty("sources"), new List<CutsceneDirector> { cutscene }); // 轉階段演出由 TransitionCutsceneBuilder 加進來
            sso.FindProperty("group").objectReferenceValue = skipGroup;
            sso.FindProperty("fill").objectReferenceValue = fill;
            sso.ApplyModifiedPropertiesWithoutUndo();
        }

        static RectTransform UiNode(string name, Transform parent, params System.Type[] components)
        {
            var types = new List<System.Type> { typeof(RectTransform) };
            types.AddRange(components);
            var go = new GameObject(name, types.ToArray());
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        static Text Label(string name, Transform parent, Font font, string text, int size, FontStyle style, Color color, TextAnchor align)
        {
            var rt = UiNode(name, parent, typeof(CanvasRenderer), typeof(Text), typeof(Outline));
            var t = rt.GetComponent<Text>();
            t.font = font;
            t.text = text;
            t.fontSize = size;
            t.fontStyle = style;
            t.color = color;
            t.alignment = align;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            var outline = rt.GetComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            outline.effectDistance = new Vector2(3f, -3f);
            return t;
        }

        internal static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        // ---------- Timeline ----------

        // 照企劃書「開場動畫分鏡」排好的預設時間軸；之後在 Timeline 視窗調整
        static TimelineAsset CreateTimeline(Layout L, PlaceholderAudioBuilder.Result sounds)
        {
            DaoDunDogBuilder.EnsureFolder("Assets/Timelines");
            var tl = ScriptableObject.CreateInstance<TimelineAsset>();
            tl.name = "Intro";
            AssetDatabase.CreateAsset(tl, TimelinePath);
            tl.durationMode = TimelineAsset.DurationMode.FixedLength;
            tl.fixedDuration = 14.5;

            // 鏡頭：第 5 鏡三段硬切；第 6 鏡最後 2.5 秒混回玩家的第三人稱鏡頭；第 7 鏡沒有片段（戰鬥視角）
            var cams = tl.CreateTrack<CinemachineTrack>(null, "Cameras");
            ShotClip(cams, Shot1, 0.0, 1.5);
            ShotClip(cams, Shot2, 1.5, 2.5);
            ShotClip(cams, Shot3, 4.0, 1.5);
            ShotClip(cams, Shot4, 5.5, 1.0);
            ShotClip(cams, Shot5a, 6.5, 1.2);
            ShotClip(cams, Shot5b, 7.7, 1.2);
            ShotClip(cams, Shot5c, 8.9, 1.1);
            ShotClip(cams, Shot6, 10.0, 3.0).easeOutDuration = 2.5;

            // 第 1 鏡高空環繞、第 2 鏡從高空緩降到法陣
            var orbit = Clip<CameraMoveClip>(tl.CreateTrack<CameraMoveTrack>(null, TrackShot1Move), "Orbit", 0.0, 1.5);
            var ob = ((CameraMoveClip)orbit.asset).template;
            ob.path = CameraPath.Orbit;
            ob.center = L.center;
            ob.radius = 24f;
            ob.height = 17f;
            ob.fromAngle = L.gateAngle + 35f;
            ob.toAngle = L.gateAngle - 15f;
            ob.progress = AnimationCurve.Linear(0f, 0f, 1f, 1f);
            var descend = Clip<CameraMoveClip>(tl.CreateTrack<CameraMoveTrack>(null, TrackShot2Move), "Descend", 1.5, 2.5);
            var db = ((CameraMoveClip)descend.asset).template;
            db.path = CameraPath.Line;
            db.from = L.P(0f, 28f, -5f);
            db.to = L.P(0f, 8.5f, -4f);

            // 刀盾狗從入口走到出生點停下
            var walk = Clip<ActorWalkClip>(tl.CreateTrack<ActorWalkTrack>(null, "Dog Walk"), "Walk In", 0.0, 1.5);
            var wb = ((ActorWalkClip)walk.asset).template;
            wb.from = L.walkFrom;
            wb.to = L.spawn;
            Hold(walk);

            // 魔法陣：1.5–4.0 由中心往外描繪、外圈分段點亮；10.0 起隨音樂脈動
            var reveal = tl.CreateTrack<MagicCircleTrack>(null, "Magic Circle Reveal");
            reveal.channel = MagicCircleChannel.Reveal;
            Hold(CurveClipAt(reveal, "Reveal", 1.5, 2.5, AnimationCurve.Linear(0f, 0f, 1f, 1f), 1f));
            var pulse = tl.CreateTrack<MagicCircleTrack>(null, "Magic Circle Pulse");
            pulse.channel = MagicCircleChannel.Pulse;
            Hold(CurveClipAt(pulse, "Pulse", 10.0, 4.5, Constant(1f), 1f));

            // 第 3 鏡：天色變暗、雷雲聚集，5.0 閃電劈中法陣中央（白屏 0.15、震動 0.4），貓在白屏時出現並冒煙
            Hold(CurveClipAt(tl.CreateTrack<StormTrack>(null, "Storm"), "Darken", 4.0, 1.0, AnimationCurve.EaseInOut(0f, 0f, 1f, 1f), 1f));
            CurveClipAt(tl.CreateTrack<CurveTrack>(null, TrackLightning), "Strike", 5.0, 0.4,
                        new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.2f, 0.3f), new Keyframe(0.35f, 1f), new Keyframe(1f, 0f)), 1f);
            CurveClipAt(tl.CreateTrack<CurveTrack>(null, TrackFlash), "White", 5.0, 0.15, Constant(1f), 1f);
            CurveClipAt(tl.CreateTrack<CurveTrack>(null, TrackShake), "Shake", 5.0, 0.4, AnimationCurve.Linear(0f, 1f, 1f, 0f), 2.5f);
            CurveClipAt(tl.CreateTrack<CurveTrack>(null, TrackSmoke), "Smoke Clears", 5.0, 1.5, AnimationCurve.Linear(0f, 0.02f, 1f, 1f), 1f);
            var visible = tl.CreateTrack<ActivationTrack>(null, "Cat Visible");
            visible.postPlaybackState = ActivationTrack.PostPlaybackState.Active;
            var show = visible.CreateDefaultClip();
            show.start = 5.0;
            show.duration = 9.5;
            show.displayName = "Visible";

            // 第 6 鏡：貓原地旋轉逐漸加速到 720 度/秒，維持到開打（迴旋衝撞從這個轉速接著加速）
            Hold(CurveClipAt(tl.CreateTrack<BossSpinTrack>(null, "Cat Spin"), "Spin Up", 10.0, 3.0,
                             new AnimationCurve(new Keyframe(0f, 0f, 0f, 0f), new Keyframe(1f, 1f, 2f, 2f)), 720f));
            Clip<MusicCueClip>(tl.CreateTrack<MusicCueTrack>(null, "Music"), "OIIA", 10.0, 4.5);

            // 聲音：腳步、風聲（第 5 鏡只剩它）、嗡鳴（第 4 鏡停）、雷聲（第 5 鏡前收完）
            AudioClipAt(tl.CreateTrack<AudioTrack>(null, TrackFootsteps), sounds.footsteps, 0.0, 1.3, false);
            AudioClipAt(tl.CreateTrack<AudioTrack>(null, TrackWind), sounds.wind, 0.0, 10.5, true).easeOutDuration = 0.5;
            AudioClipAt(tl.CreateTrack<AudioTrack>(null, TrackHum), sounds.hum, 1.5, 4.0, false);
            AudioClipAt(tl.CreateTrack<AudioTrack>(null, TrackThunder), sounds.thunder, 5.0, 1.5, false);

            // 第 7 鏡 13.0 字卡；跳過時跳到 12.95（字卡、音樂、戰鬥視角照常出現）
            tl.CreateMarkerTrack();
            var title = tl.markerTrack.CreateMarker<CutsceneEventMarker>(13.0);
            title.kind = CutsceneEvent.TitleCard;
            title.duration = 1.5f;
            var skip = tl.markerTrack.CreateMarker<CutsceneEventMarker>(12.95);
            skip.kind = CutsceneEvent.SkipTarget;

            EditorUtility.SetDirty(tl);
            AssetDatabase.SaveAssets();
            return tl;
        }

        internal static TimelineClip Clip<T>(TrackAsset track, string name, double start, double duration) where T : PlayableAsset
        {
            var clip = track.CreateClip<T>();
            clip.start = start;
            clip.duration = duration;
            clip.displayName = name;
            return clip;
        }

        internal static TimelineClip ShotClip(CinemachineTrack track, string camera, double start, double duration)
        {
            var clip = Clip<CinemachineShot>(track, camera, start, duration);
            var shot = (CinemachineShot)clip.asset;
            shot.DisplayName = camera;
            shot.VirtualCamera = new ExposedReference<CinemachineVirtualCameraBase> { exposedName = new PropertyName(GUID.Generate().ToString()) };
            return clip;
        }

        internal static TimelineClip CurveClipAt(TrackAsset track, string name, double start, double duration, AnimationCurve curve, float scale)
        {
            var clip = Clip<CurveClip>(track, name, start, duration);
            var b = ((CurveClip)clip.asset).template;
            b.curve = curve;
            b.scale = scale;
            return clip;
        }

        internal static TimelineClip AudioClipAt(AudioTrack track, AudioClip audio, double start, double duration, bool loop)
        {
            var clip = track.CreateClip(audio);
            clip.start = start;
            clip.duration = duration;
            ((AudioPlayableAsset)clip.asset).loop = loop;
            return clip;
        }

        internal static AnimationCurve Constant(float v) => new AnimationCurve(new Keyframe(0f, v), new Keyframe(1f, v));

        // 片段結束後維持最後的狀態（直到下一個片段或時間軸結束）；Timeline 沒有公開這個 setter
        internal static TimelineClip Hold(TimelineClip clip)
        {
            typeof(TimelineClip).GetProperty("postExtrapolationMode").GetSetMethod(true)
                .Invoke(clip, new object[] { TimelineClip.ClipExtrapolation.Hold });
            return clip;
        }

        // 每條軌道綁到場景物件；Cinemachine 片段依名稱接到同名的鏡頭
        static void Bind(PlayableDirector director, TimelineAsset tl, CinemachineBrain brain, Dictionary<string, CinemachineCamera> shots,
                         Dictionary<string, Object> named, PlayerController player, MagicCircle circle, ArenaAtmosphere atmosphere,
                         OiiaCatPoser poser, AudioManager audio)
        {
            foreach (var track in tl.GetOutputTracks())
            {
                if (track is MarkerTrack) continue; // 標記送到 PlayableDirector 自己的物件（CutsceneDirector）
                Object binding = null;
                switch (track)
                {
                    case CinemachineTrack cams:
                        binding = brain;
                        foreach (var clip in cams.GetClips())
                        {
                            var shot = (CinemachineShot)clip.asset;
                            if (!shots.TryGetValue(clip.displayName, out var cam))
                            {
                                Debug.LogWarning($"[Cutscene] 鏡頭片段「{clip.displayName}」找不到同名的鏡頭，請在 Timeline 手動指定。");
                                continue;
                            }
                            if (PropertyName.IsNullOrEmpty(shot.VirtualCamera.exposedName))
                            {
                                shot.VirtualCamera = new ExposedReference<CinemachineVirtualCameraBase> { exposedName = new PropertyName(GUID.Generate().ToString()) };
                                EditorUtility.SetDirty(shot);
                            }
                            director.SetReferenceValue(shot.VirtualCamera.exposedName, cam);
                        }
                        break;
                    case ActorWalkTrack _: binding = player; break;
                    case MagicCircleTrack _: binding = circle; break;
                    case StormTrack _: binding = atmosphere; break;
                    case BossSpinTrack _: binding = poser; break;
                    case MusicCueTrack _: binding = audio; break;
                    case ActivationTrack _: binding = poser.model.gameObject; break;
                    default:
                        named.TryGetValue(track.name, out binding);
                        break;
                }
                if (binding == null) Debug.LogWarning($"[Cutscene] Timeline 軌道「{track.name}」沒有對應的場景物件，請手動綁定。");
                director.SetGenericBinding(track, binding);
            }
        }

        // ---------- 資產 ----------

        static void DisableStartOnPlay(BossController boss)
        {
            var so = new SerializedObject(boss);
            so.FindProperty("startOnPlay").boolValue = false;
            so.ApplyModifiedProperties();

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BossPrefabPath);
            var prefabController = prefab != null ? prefab.GetComponent<BossController>() : null;
            if (prefabController == null) return;
            var pso = new SerializedObject(prefabController);
            pso.FindProperty("startOnPlay").boolValue = false;
            pso.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(prefab);
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

        static AtmosphereData GetAtmosphere()
        {
            var data = AssetDatabase.LoadAssetAtPath<AtmosphereData>(AtmospherePath);
            if (data != null) return data;
            var storm = AssetDatabase.LoadAssetAtPath<Material>(SkyStormPath);
            if (storm == null)
            {
                Debug.LogError($"[Cutscene] 找不到 {SkyStormPath}（T0 的雷雲天空盒）。");
                return null;
            }

            // 暴風雨前：同一個 Procedural 天空盒，偏暖、較亮
            var calm = AssetDatabase.LoadAssetAtPath<Material>(SkyCalmPath);
            if (calm == null)
            {
                calm = new Material(storm) { name = "Sky_CalmPlaceholder" };
                calm.SetColor("_SkyTint", new Color(0.62f, 0.52f, 0.45f));
                calm.SetColor("_GroundColor", new Color(0.3f, 0.27f, 0.24f));
                calm.SetFloat("_Exposure", 1.05f);
                calm.SetFloat("_AtmosphereThickness", 1.1f);
                AssetDatabase.CreateAsset(calm, SkyCalmPath);
            }

            data = ScriptableObject.CreateInstance<AtmosphereData>();
            data.calm = new AtmosphereData.Look
            {
                skybox = calm,
                sunColor = new Color(1f, 0.86f, 0.68f),
                sunIntensity = 1.1f,
                ambientSky = new Color(0.55f, 0.5f, 0.47f),
                ambientEquator = new Color(0.4f, 0.36f, 0.33f),
                ambientGround = new Color(0.2f, 0.18f, 0.16f),
            };
            // 暴風雨：T0 的雷雲天空盒與環境光，主光源調暗、偏藍
            data.storm = new AtmosphereData.Look
            {
                skybox = storm,
                sunColor = new Color(0.62f, 0.7f, 0.92f),
                sunIntensity = 0.35f,
                ambientSky = new Color(0.34f, 0.37f, 0.45f),
                ambientEquator = new Color(0.22f, 0.23f, 0.27f),
                ambientGround = new Color(0.1f, 0.1f, 0.11f),
            };
            DaoDunDogBuilder.EnsureFolder(AtmospherePath.Substring(0, AtmospherePath.LastIndexOf('/')));
            AssetDatabase.CreateAsset(data, AtmospherePath);
            return data;
        }

        static Material GetCircleMaterial(ArenaData arena)
        {
            DaoDunDogBuilder.EnsureFolder(MatDir);
            string path = $"{MatDir}/MagicCircleDecal.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(CircleShaderPath);
            if (shader == null)
            {
                Debug.LogError($"[Cutscene] 找不到 {CircleShaderPath}。");
                return null;
            }
            // 預設值讓編輯模式也看得到完整的法陣；執行時 MagicCircle 用自己的複本設定
            mat = new Material(shader) { name = "MagicCircleDecal" };
            mat.SetColor("_Color", new Color(0.62f, 0.18f, 1f, 1f));
            mat.SetVector("_Params", new Vector4(1f, 1f, 1f, 0f));
            mat.SetVector("_Style", new Vector4(0.07f, 1f, 2.5f, 8f));
            mat.SetVector("_Size", new Vector4(arena.magicCircleRadius * 2f, 0f, 0f, 0f));
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        static Material GetUnlitMaterial(string name, Color color)
        {
            DaoDunDogBuilder.EnsureFolder(MatDir);
            string path = $"{MatDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;
            mat = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = name };
            mat.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        // 半透明（Alpha 混合）的 Particles/Unlit；不透明度由 SmokePuffs 用 MaterialPropertyBlock 改 _BaseColor
        internal static Material GetSmokeMaterial()
        {
            DaoDunDogBuilder.EnsureFolder(MatDir);
            string path = $"{MatDir}/Smoke.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;
            mat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")) { name = "Smoke" };
            mat.SetFloat("_Surface", 1f); // Transparent
            mat.SetFloat("_Blend", 0f);   // Alpha
            mat.SetColor("_BaseColor", new Color(0.32f, 0.31f, 0.36f, 0.9f));
            BaseShaderGUI.SetupMaterialBlendMode(mat);
            UnityEditor.Rendering.Universal.ShaderGUI.ParticleGUI.SetMaterialKeywords(mat);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        internal static void SetArray<T>(SerializedProperty list, List<T> values) where T : Object
        {
            list.arraySize = values.Count;
            for (int i = 0; i < values.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
    }
}
