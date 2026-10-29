using System;
using System.Collections.Generic;
using System.Linq;
using BossFight.Battle;
using BossFight.Boss;
using BossFight.Cameras;
using BossFight.Core;
using BossFight.Cutscene;
using BossFight.Hub;
using BossFight.Player;
using BossFight.Progression;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace BossFight.EditorTools
{
    /// <summary>定點建立五段正式演出；時間軸只在不存在時建立，重建場景物件後按名稱重綁，不動 OIIA。</summary>
    public static class PrologueCutsceneBuilder
    {
        public const string DataPath = "Assets/ScriptableObjects/Cutscene/PrologueCutscene.asset";
        public const string TimelineDirectory = "Assets/Timelines/Prologue";
        const string ProfilePath = "Assets/ScriptableObjects/Cutscene/Prologue/PresentationVolume.asset";
        const string MaterialDirectory = "Assets/Models/Prologue/Cutscene";
        static readonly string[] Names = { "GuardianEntrance", "GuardianTransition", "Knockdown", "GuardianVictory", "HubWake" };

        [MenuItem("Tools/Prologue/Build Cutscenes")]
        public static void BuildAll()
        {
            Guard();
            // 只開啟已完成的場景並定點補演出，不執行重建地形、角色或 OIIA 的工具。
            if (SceneManager.GetActiveScene().isDirty) EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            EditorSceneManager.OpenScene(PrologueBuilder.ScenePath);
            BuildPrologue(); EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            EditorSceneManager.OpenScene(HubBuilder.ScenePath);
            BuildHub(); EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene(PrologueBuilder.ScenePath);
            Debug.Log(Validate());
        }
        [MenuItem("Tools/Prologue/Reset Cutscene Timelines")]
        public static void ResetTimelines()
        {
            Guard();
            foreach (string name in Names) AssetDatabase.DeleteAsset(TimelineDirectory + "/" + name + ".playable");
            BuildAll();
        }
        static void Guard()
        { if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("請先停止 Play，再建立演出。"); }
        static PrologueCutsceneData Data()
        {
            foreach (string path in new[] { TimelineDirectory, "Assets/ScriptableObjects/Cutscene/Prologue", MaterialDirectory }) DaoDunDogBuilder.EnsureFolder(path);
            var d = Get<PrologueCutsceneData>(DataPath);
            var sounds = PrologueAudioBuilder.Build();
            if (d.tutorialMusic == null) d.tutorialMusic = sounds["Tutorial"];
            if (d.phase1Music == null) d.phase1Music = sounds["Phase1"];
            if (d.phase2Music == null) d.phase2Music = sounds["Phase2"];
            EditorUtility.SetDirty(d); return d;
        }
        /// <summary>上游（守衛、教學）重建後，演出已經建過才接回；不遞迴執行上游 Builder。</summary>
        public static void ReconnectIfPresent()
        {
            if (AssetDatabase.LoadAssetAtPath<PrologueCutsceneData>(DataPath) == null) return;
            if (SceneManager.GetActiveScene().path == PrologueBuilder.ScenePath && Object.FindAnyObjectByType<GuardianPhaseTwo>() != null) BuildPrologue();
        }
        public static void BuildPrologue()
        {
            Guard();
            if (SceneManager.GetActiveScene().path != PrologueBuilder.ScenePath) throw new InvalidOperationException("需要 Prologue 場景。");
            var guardian = Object.FindAnyObjectByType<GuardianPhaseController>();
            var tutorial = Object.FindAnyObjectByType<TutorialDirector>();
            var phase = Object.FindAnyObjectByType<GuardianPhaseTwo>();
            if (guardian == null || tutorial == null || phase == null) throw new InvalidOperationException("先建好序章場景、教學與守衛（Build Prologue → Build Teaching Actors → Build Tutorial → Build Guardian）。");
            var d = Data(); var sounds = PrologueAudioBuilder.Build();
            var old = GameObject.Find("PrologueCutscenes"); if (old != null) Object.DestroyImmediate(old);
            var root = new GameObject("PrologueCutscenes");
            var actor = guardian.Boss.transform;
            var stage = BuildStage(root.transform, d);
            Ambient(root.transform, sounds["Wind"], 0.12f); Ambient(root.transform, sounds["Birds"], 0.035f);
            Set(stage, "guardianOrigin", actor); Set(stage, "guardianModel", actor.Find("Model")); Set(stage, "guardianCrown", actor.Find("Model/Crown"));
            Array(stage, "cracks", actor.GetComponentsInChildren<Renderer>(true).Where(r => r.name.StartsWith("Crack_")).ToArray());
            BuildGroundFx(stage, root.transform, actor.position);
            var playerStage = Node(root.transform, "PlayerStage", PlayerSpawnerBuilder.Preview().transform.position);
            Set(stage, "playerStage", playerStage);
            var mentor = Object.FindAnyObjectByType<DaisyMentor>();
            var flower = Node(root.transform, "FlowerLook", mentor.transform.position + Vector3.up * 0.5f);
            var head = Node(root.transform, "GuardianHead", actor.position + Vector3.up * 5.5f);
            var low = Node(root.transform, "GuardianGround", actor.position + Vector3.up);
            var deathLook = Node(playerStage, "DeathLook", Vector3.zero); deathLook.localPosition = Vector3.up * 0.4f;
            var shots = new Dictionary<string, CinemachineCamera>();
            Add("E1_OverShoulder", new Vector3(0.7f, 1.5f, -2.7f), flower, 50f, playerStage, true);
            Add("E2_Wide", new Vector3(10f, 7f, -11f), low, 65f);
            Add("E3_Crack", actor.position + new Vector3(4f, 0.4f, -5f), low, 62f);
            Add("E4_Eruption", new Vector3(0f, 1.6f, -5f), head, 62f);
            Add("E5_Eyes", actor.position + new Vector3(0f, 2f, -7f), head, 45f);
            Add("E6_Roar", actor.position + new Vector3(2.5f, 2f, -9f), head, 55f);
            Add("E7_Return", new Vector3(0f, 2f, -4f), head, 60f, playerStage, true);
            Add("T1_Front", actor.position + new Vector3(0f, 2f, -8f), head, 50f);
            Add("T2_Wave", new Vector3(10f, 6f, -6f), low, 65f);
            Add("K1_Fall", new Vector3(1.5f, 1.1f, -2.5f), deathLook, 50f, playerStage, true);
            Add("K2_Ground", new Vector3(0.8f, 0.18f, -1.3f), head, 55f, playerStage, true, d.cameraRoll);
            Add("V1_Wither", actor.position + new Vector3(3f, 2f, -9f), head, 55f);
            Add("V2_Sink", new Vector3(8f, 5f, -6f), low, 65f);
            Add("V3_Flower", mentor.transform.position + new Vector3(0f, 0.8f, -2.2f), flower, 45f);
            var entrance = Sequence(root.transform, "GuardianEntrance", d.entranceShots.Sum(), stage, shots, sounds, tl => Entrance(tl, d));
            var transition = Sequence(root.transform, "GuardianTransition", d.transitionDuration, stage, shots, sounds, tl => Transition(tl, d));
            var knockdown = Sequence(root.transform, "Knockdown", d.knockdownDuration, stage, shots, sounds, tl => Knockdown(tl, d));
            var victory = Sequence(root.transform, "GuardianVictory", d.victoryDuration, stage, shots, sounds, tl => Victory(tl, d));
            var flow = root.AddComponent<PrologueCutsceneFlow>();
            Set(flow, "tutorial", tutorial); Set(flow, "guardian", guardian); Set(flow, "phaseTwo", phase);
            Set(flow, "outcome", guardian.GetComponent<PrologueOutcome>()); Set(flow, "session", Object.FindAnyObjectByType<InputLockArbiter>());
            Set(flow, "dialogue", new SerializedObject(tutorial).FindProperty("dialogue").objectReferenceValue);
            Set(flow, "mentor", mentor); Set(flow, "fullPower", guardian.Boss.GetComponent<GuardianFullPowerVisual>());
            Set(flow, "audioManager", Object.FindAnyObjectByType<AudioManager>()); Set(flow, "cameraController", Object.FindAnyObjectByType<CameraController>());
            Set(flow, "sceneFlow", Object.FindAnyObjectByType<SceneFlow>()); Set(flow, "pause", Object.FindAnyObjectByType<GamePause>());
            Set(flow, "stage", stage); Set(flow, "data", d); Set(flow, "entrance", entrance); Set(flow, "transition", transition); Set(flow, "knockdown", knockdown); Set(flow, "victory", victory);
            Array(stage, "noise", root.GetComponentsInChildren<CinemachineBasicMultiChannelPerlin>(true));
            WireAudio(stage); PlayerSpawnerBuilder.Wire(); EditorSceneManager.MarkSceneDirty(root.scene);
            void Add(string name, Vector3 position, Transform look, float fov, Transform parent = null, bool local = false, float roll = 0f)
            { shots[name] = Shot(parent != null ? parent : root.transform, name, position, look, fov, local, roll); }
        }
        public static void BuildHub()
        {
            Guard();
            var hub = Object.FindAnyObjectByType<HubManager>(); if (hub == null) throw new InvalidOperationException("需要 Hub 場景。");
            var d = Data(); var sounds = PrologueAudioBuilder.Build();
            var old = GameObject.Find("HubWakeCutscene"); if (old != null) Object.DestroyImmediate(old);
            var root = new GameObject("HubWakeCutscene");
            var chief = Object.FindAnyObjectByType<VillageChief>();
            var session = DaoDunDogBuilder.GetOrAdd<InputLockArbiter>(hub.gameObject);
            Set(session, "cameraController", Object.FindAnyObjectByType<CameraController>()); Set(session, "pause", Object.FindAnyObjectByType<GamePause>());
            Set(session, "sceneFlow", Object.FindAnyObjectByType<SceneFlow>());
            Set(hub, "inputLock", session);
            var stage = BuildStage(root.transform, d);
            Ambient(root.transform, sounds["Fire"], 0.08f); Ambient(root.transform, sounds["Birds"], 0.035f);
            var playerStage = Node(root.transform, "PlayerStage", PlayerSpawnerBuilder.Preview().transform.position); Set(stage, "playerStage", playerStage);
            var face = chief.transform.Find("WakeFace"); if (face == null) face = Node(chief.transform, "WakeFace", Vector3.zero); face.localPosition = Vector3.up * 2.2f;
            var shots = new Dictionary<string, CinemachineCamera>
            {
                { "W1_Eyes", Shot(playerStage, "W1_Eyes", new Vector3(0f, 0.5f, 0.1f), face, 65f, true, 4f) },
                { "W2_TwoShot", chief.StationCamera },
            };
            var wake = Sequence(root.transform, "HubWake", d.wakeDuration, stage, shots, sounds, tl => Wake(tl, d));
            var arrival = root.AddComponent<HubPrologueArrival>();
            Set(arrival, "sceneFlow", Object.FindAnyObjectByType<SceneFlow>()); Set(arrival, "flowData", AssetDatabase.LoadAssetAtPath<SceneFlowData>(SceneFlowBuilder.DataPath));
            Set(arrival, "hub", hub); Set(arrival, "chief", chief); Set(arrival, "session", session); Set(arrival, "stage", stage); Set(arrival, "wake", wake);
            Array(stage, "noise", root.GetComponentsInChildren<CinemachineBasicMultiChannelPerlin>(true));
            // 新增欄位的預設台詞由 Unity 序列化，舊資產若為空則補企劃文本，不改任務 id 或獎勵。
            var story = AssetDatabase.LoadAssetAtPath<HubStoryData>("Assets/ScriptableObjects/Hub/HubStory.asset");
            var defaults = ScriptableObject.CreateInstance<HubStoryData>();
            if (story.knockdownLines == null || story.knockdownLines.Count == 0) story.knockdownLines = defaults.knockdownLines;
            if (story.victoryLines == null || story.victoryLines.Count == 0) story.victoryLines = defaults.victoryLines;
            Object.DestroyImmediate(defaults); EditorUtility.SetDirty(story);
            WireAudio(stage); SceneFlowBuilder.Build(); PlayerSpawnerBuilder.Wire(); EditorSceneManager.MarkSceneDirty(hub.gameObject.scene);
        }
        static PrologueCinematicStage BuildStage(Transform parent, PrologueCutsceneData d)
        {
            var stage = parent.gameObject.AddComponent<PrologueCinematicStage>(); Set(stage, "data", d);
            Set(stage, "pause", Object.FindAnyObjectByType<GamePause>()); Set(stage, "hitStop", Object.FindAnyObjectByType<HitStop>());
            Set(stage, "sun", Object.FindObjectsByType<Light>(FindObjectsSortMode.None).FirstOrDefault(l => l.type == LightType.Directional));
            var volume = new GameObject("PresentationVolume").AddComponent<Volume>(); volume.transform.SetParent(parent, false); volume.isGlobal = true; volume.priority = 100f;
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>(); AssetDatabase.CreateAsset(profile, ProfilePath);
                var dof = profile.Add<DepthOfField>(); dof.active = false;
                var vignette = profile.Add<Vignette>(); vignette.intensity.Override(0f);
                var colors = profile.Add<ColorAdjustments>(); colors.saturation.Override(0f);
                foreach (var c in profile.components) AssetDatabase.AddObjectToAsset(c, profile);
                EditorUtility.SetDirty(profile);
            }
            volume.sharedProfile = profile; Set(stage, "volume", volume);
            var canvasRoot = new GameObject("PresentationOverlay", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler)); canvasRoot.transform.SetParent(parent, false);
            var canvas = canvasRoot.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 900;
            var scaler = canvasRoot.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920f, 1080f);
            Set(stage, "redFlash", Overlay(canvasRoot.transform, "RedFlash", new Color(0.8f, 0f, 0f)));
            Set(stage, "whiteFlash", Overlay(canvasRoot.transform, "WhiteFlash", Color.white));
            Set(stage, "black", Overlay(canvasRoot.transform, "Black", Color.black));
            var title = new GameObject("Title", typeof(RectTransform), typeof(CanvasGroup)); title.transform.SetParent(canvasRoot.transform, false); title.GetComponent<CanvasGroup>().alpha = 0f;
            var titleText = Label(title.transform, "BossName", d.title, 72); titleText.rectTransform.anchoredPosition = new Vector2(0f, -180f);
            Set(stage, "title", title.GetComponent<CanvasGroup>());
            var notice = Label(canvasRoot.transform, "SaveFailure", "無法保存進度，請確認儲存空間後按確認重試。", 32);
            notice.gameObject.SetActive(false); Set(stage, "saveFailure", notice);
            return stage;
        }
        static CanvasGroup Overlay(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(CanvasGroup)); go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>(); image.color = color; image.raycastTarget = false; IntroCutsceneBuilder.Stretch(image.rectTransform);
            var g = go.GetComponent<CanvasGroup>(); g.alpha = 0f; g.blocksRaycasts = false; return g;
        }
        static Text Label(Transform parent, string name, string text, int size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text)); go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>(); t.text = text; t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); t.fontSize = size; t.alignment = TextAnchor.MiddleCenter; t.color = Color.white; t.raycastTarget = false;
            t.rectTransform.sizeDelta = new Vector2(1600f, 180f); return t;
        }
        static CinemachineCamera Shot(Transform parent, string name, Vector3 position, Transform look, float fov, bool local, float roll)
        {
            var t = Node(parent, name, position); if (local) t.localPosition = position;
            var c = t.gameObject.AddComponent<CinemachineCamera>(); c.Priority = -100; c.LookAt = look;
            var lens = LensSettings.Default; lens.FieldOfView = fov; lens.Dutch = roll; c.Lens = lens;
            t.gameObject.AddComponent<CinemachineHardLookAt>();
            var noise = t.gameObject.AddComponent<CinemachineBasicMultiChannelPerlin>(); noise.AmplitudeGain = 0f; noise.FrequencyGain = 1.5f;
            noise.NoiseProfile = AssetDatabase.LoadAssetAtPath<NoiseSettings>("Packages/com.unity.cinemachine/Presets/Noise/6D Shake.asset");
            return c;
        }
        static CutsceneDirector Sequence(Transform parent, string name, double duration, PrologueCinematicStage stage, Dictionary<string, CinemachineCamera> shots, Dictionary<string, AudioClip> sounds, Action<TimelineAsset> create)
        {
            string path = TimelineDirectory + "/" + name + ".playable";
            var tl = AssetDatabase.LoadAssetAtPath<TimelineAsset>(path);
            if (tl == null)
            {
                tl = ScriptableObject.CreateInstance<TimelineAsset>(); AssetDatabase.CreateAsset(tl, path);
                tl.durationMode = TimelineAsset.DurationMode.FixedLength; tl.fixedDuration = duration;
                create(tl); EditorUtility.SetDirty(tl);
            }
            var root = Node(parent, name, Vector3.zero); root.localPosition = Vector3.zero;
            tl.CreateMarkerTrack();
            if (!tl.markerTrack.GetMarkers().OfType<CutsceneCueMarker>().Any(m => m.kind == CutsceneCue.SequenceEnd))
            { Cue(tl, CutsceneCue.SequenceEnd, tl.fixedDuration - 0.001); EditorUtility.SetDirty(tl); }
            var pd = root.gameObject.AddComponent<PlayableDirector>(); pd.playOnAwake = false; pd.extrapolationMode = DirectorWrapMode.None; pd.timeUpdateMode = DirectorUpdateMode.UnscaledGameTime; pd.playableAsset = tl;
            var seq = root.gameObject.AddComponent<CutsceneDirector>(); Bool(seq, "externalControl", true); Bool(seq, "manualClock", true);
            Set(seq, "pause", Object.FindAnyObjectByType<GamePause>()); Set(seq, "hitStop", Object.FindAnyObjectByType<HitStop>());
            foreach (var track in tl.GetOutputTracks())
            {
                if (track is CinematicValueTrack) pd.SetGenericBinding(track, stage);
                else if (track is CinemachineTrack cameras)
                {
                    pd.SetGenericBinding(track, Object.FindAnyObjectByType<CinemachineBrain>());
                    foreach (var clip in cameras.GetClips())
                    {
                        if (!shots.TryGetValue(clip.displayName, out var camera)) throw new InvalidOperationException("鏡頭名稱沒有對應物件：" + clip.displayName);
                        var asset = (CinemachineShot)clip.asset;
                        pd.SetReferenceValue(asset.VirtualCamera.exposedName, camera);
                    }
                }
                else if (track is CameraMoveTrack)
                {
                    string key = track.name.Substring("Move_".Length);
                    if (!shots.ContainsKey(key)) throw new InvalidOperationException("路徑鏡頭不存在：" + key);
                    pd.SetGenericBinding(track, shots[key].transform);
                }
                else if (track is AudioTrack)
                {
                    var source = root.gameObject.AddComponent<AudioSource>(); source.playOnAwake = false; source.spatialBlend = 0f; source.volume = 0.7f;
                    pd.SetGenericBinding(track, source);
                }
                else if (track is ActorWalkTrack || track is ActorPushTrack) pd.SetGenericBinding(track, PlayerSpawnerBuilder.Preview().Controller);
            }
            return seq;
        }
        static void Entrance(TimelineAsset tl, PrologueCutsceneData d)
        {
            double at = 0; var cams = tl.CreateTrack<CinemachineTrack>(null, "Cameras");
            string[] names = { "E1_OverShoulder", "E2_Wide", "E3_Crack", "E4_Eruption", "E5_Eyes", "E6_Roar", "E7_Return" };
            for (int i = 0; i < names.Length; i++) { IntroCutsceneBuilder.ShotClip(cams, names[i], at, d.entranceShots[i]); at += d.entranceShots[i]; }
            double total = at;
            Value(tl, CinematicValue.Rise, 0, total, Curve((0, 0), (0.42f, 0), (0.58f, 1), (1, 1)));
            Value(tl, CinematicValue.GroundCrack, 2.5, total - 2.5, AnimationCurve.Linear(0f, 1f, 1f, 1f));
            Value(tl, CinematicValue.Darkness, 5.5, total - 5.5, Curve((0, 0), (0.5f, 1), (1, 1)));
            Value(tl, CinematicValue.Vignette, 5.5, total - 5.5, AnimationCurve.Linear(0f, 0f, 1f, 0.4f));
            Value(tl, CinematicValue.Shake, 1, 6.5, Curve((0, 0.15f), (0.45f, 0.5f), (0.65f, 1.2f), (1, 0)));
            Value(tl, CinematicValue.Debris, 3.8, 2, Curve((0, 1), (0.8f, 1), (1, 0)));
            Value(tl, CinematicValue.Birds, 1, 2, Curve((0, 1), (0.7f, 1), (1, 0)));
            Value(tl, CinematicValue.Title, total - 2.5, 2.5, Curve((0, 0), (0.15f, 1), (0.8f, 1), (1, 0)));
            Move(tl, "E4_Eruption", 4, 1.5, new Vector3(0f, 1.6f, -5f), new Vector3(0f, 3f, -4f));
            Audio(tl, "Giggle", 0, 1); Audio(tl, "Rumble", 1, 4); Audio(tl, "Crack", 2.5, 1.5); Audio(tl, "Eruption", 4, 1.5);
            Audio(tl, "Heartbeat", 5.5, 0.65); Audio(tl, "Roar", 7, 2);
        }
        static void Transition(TimelineAsset tl, PrologueCutsceneData d)
        {
            double end = d.transitionDuration, power = end - 1;
            Shots(tl, ("T1_Front", 0, power), ("T2_Wave", power, 1));
            Cue(tl, CutsceneCue.TransitionLine, power - d.dialogueTimeout); Cue(tl, CutsceneCue.Power, power);
            Value(tl, CinematicValue.WhiteFlash, power, 0.3, Curve((0, 0.65f), (1, 0)));
            Value(tl, CinematicValue.Wave, power, 1, AnimationCurve.Linear(0f, 0f, 1f, 1f));
            Value(tl, CinematicValue.Push, power, 0.8, AnimationCurve.EaseInOut(0f, 0f, 1f, 1f), true);
            Value(tl, CinematicValue.Shake, power, 0.8, AnimationCurve.Linear(0f, 0.7f, 1f, 0f));
            Audio(tl, "Rumble", 0, end); Audio(tl, "Wave", power, 1);
        }
        static void Knockdown(TimelineAsset tl, PrologueCutsceneData d)
        {
            double end = d.knockdownDuration;
            Shots(tl, ("K1_Fall", 0, 1), ("K2_Ground", 1, end - 1));
            Slow(tl, end, d.knockdownSlowTime);
            Value(tl, CinematicValue.RedFlash, 0, 0.35, Curve((0, 0.85f), (1, 0)));
            Value(tl, CinematicValue.Blur, 0.5, end - 0.5, Curve((0, 0), (0.6f, 1), (1, 1)));
            Value(tl, CinematicValue.Vignette, 0.4, end - 0.4, AnimationCurve.Linear(0f, 0.3f, 1f, 1f));
            Value(tl, CinematicValue.LowPass, 0, end, Curve((0, 0), (0.2f, 1), (1, 1)));
            Value(tl, CinematicValue.Black, 0, end, Curve((0, 0), ((float)((end - d.blackHold - 0.8) / end), 0), ((float)((end - d.blackHold) / end), 1), (1, 1)));
            Move(tl, "K2_Ground", 1, end - 1, new Vector3(0.8f, 0.5f, -1.8f), new Vector3(0.8f, 0.18f, -1.3f), true);
            Audio(tl, "Heartbeat", 1.5, 0.65); Audio(tl, "Heartbeat", 2.3, 0.65);
            Audio(tl, "Roar", end - d.blackHold, d.blackHold).easeOutDuration = d.blackHold;
        }
        static void Victory(TimelineAsset tl, PrologueCutsceneData d)
        {
            double end = d.victoryDuration;
            Shots(tl, ("V1_Wither", 0, end * 0.4), ("V2_Sink", end * 0.4, end * 0.25), ("V3_Flower", end * 0.65, end * 0.35));
            Slow(tl, end, d.victorySlowTime);
            Value(tl, CinematicValue.WhiteFlash, 0, 0.4, Curve((0, 0.9f), (1, 0)));
            Value(tl, CinematicValue.Wither, 0.8, end * 0.37, AnimationCurve.EaseInOut(0f, 0f, 1f, 1f), true);
            Value(tl, CinematicValue.Glow, 0.8, end * 0.35, AnimationCurve.Linear(0f, 1f, 1f, 0f), true);
            Value(tl, CinematicValue.Rise, end * 0.4, end * 0.25, AnimationCurve.EaseInOut(0f, 1f, 1f, 0f), true);
            Value(tl, CinematicValue.Darkness, end * 0.4, end * 0.25, AnimationCurve.Linear(0f, 1f, 1f, 0f), true);
            Value(tl, CinematicValue.GroundCrack, end * 0.65, end * 0.35, AnimationCurve.Linear(0f, 0f, 1f, 0f));
            Value(tl, CinematicValue.Debris, end * 0.4, end * 0.25, Curve((0, 1), (0.8f, 1), (1, 0)));
            Cue(tl, CutsceneCue.Farewell, 0.8); Cue(tl, CutsceneCue.FlowerEmerge, end * 0.65); Cue(tl, CutsceneCue.Surprise, end * 0.75); Cue(tl, CutsceneCue.FlowerBurrow, end * 0.9);
            Audio(tl, "Wither", 0.8, 2.5); Audio(tl, "Sink", end * 0.4, 2.5); Audio(tl, "Surprise", end * 0.75, 0.6); Audio(tl, "Victory", end * 0.7, end * 0.3);
        }
        static void Wake(TimelineAsset tl, PrologueCutsceneData d)
        {
            double end = d.wakeDuration;
            Shots(tl, ("W1_Eyes", 0, end * 0.75), ("W2_TwoShot", end * 0.75, end * 0.25));
            Value(tl, CinematicValue.Black, 0, end, Curve((0, 1), (0.12f, 1), (0.22f, 0.15f), (0.3f, 1), (0.4f, 0.05f), (0.46f, 1), (0.6f, 0), (1, 0)));
            Value(tl, CinematicValue.Blur, 0, end, Curve((0, 1), (0.45f, 1), (0.85f, 0), (1, 0)));
            Value(tl, CinematicValue.Lying, 0, end, Curve((0, 1), (0.55f, 1), (0.9f, 0), (1, 0)));
            Audio(tl, "Fire", 0, end); Audio(tl, "Birds", 0, end);
        }
        static void Slow(TimelineAsset tl, double total, float seconds)
        { float stop = seconds / (float)total; Value(tl, CinematicValue.Slow, 0, total, Curve((0, 1), (stop, 1), (stop + 0.0001f, 0), (1, 0))); }
        static void Value(TimelineAsset tl, CinematicValue channel, double at, double length, AnimationCurve curve, bool hold = false)
        {
            var track = tl.CreateTrack<CinematicValueTrack>(null, channel.ToString());
            var clip = IntroCutsceneBuilder.Clip<CinematicValueClip>(track, channel.ToString(), at, length);
            var b = ((CinematicValueClip)clip.asset).template; b.channel = channel; b.curve = curve;
            if (hold) IntroCutsceneBuilder.Hold(clip);
        }
        static AnimationCurve Curve(params (float at, float value)[] keys) => new AnimationCurve(keys.Select(k => new Keyframe(k.at, k.value)).ToArray());
        static void Shots(TimelineAsset tl, params (string name, double at, double duration)[] shots)
        { var track = tl.CreateTrack<CinemachineTrack>(null, "Cameras"); foreach (var shot in shots) IntroCutsceneBuilder.ShotClip(track, shot.name, shot.at, shot.duration); }
        static void Cue(TimelineAsset tl, CutsceneCue cue, double at)
        { tl.CreateMarkerTrack(); var m = tl.markerTrack.CreateMarker<CutsceneCueMarker>(at); m.kind = cue; }
        static TimelineClip Audio(TimelineAsset tl, string name, double at, double length)
        {
            var track = tl.CreateTrack<AudioTrack>(null, "Audio_" + name);
            var audio = AssetDatabase.LoadAssetAtPath<AudioClip>(PrologueAudioBuilder.DirectoryPath + "/" + name + ".wav");
            return IntroCutsceneBuilder.AudioClipAt(track, audio, at, length, length > audio.length);
        }
        static void Move(TimelineAsset tl, string camera, double at, double length, Vector3 from, Vector3 to, bool local = false)
        {
            var track = tl.CreateTrack<CameraMoveTrack>(null, "Move_" + camera);
            var clip = IntroCutsceneBuilder.Clip<CameraMoveClip>(track, camera, at, length);
            var b = ((CameraMoveClip)clip.asset).template; b.from = from; b.to = to; b.localSpace = local;
        }
        static void BuildGroundFx(PrologueCinematicStage stage, Transform parent, Vector3 center)
        {
            var cracks = Node(parent, "GroundCracks", center + Vector3.up * 0.04f);
            var material = Mat("Crack", new Color(3f, 0.02f, 0.005f));
            for (int i = 0; i < 10; i++)
            {
                float angle = i * 36f;
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = "Fissure_" + i; Object.DestroyImmediate(go.GetComponent<Collider>());
                go.transform.SetParent(cracks, false); go.transform.localRotation = Quaternion.Euler(0f, angle, 0f);
                go.transform.localPosition = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * 2.5f; go.transform.localScale = new Vector3(0.12f, 0.02f, 5f);
                go.GetComponent<Renderer>().sharedMaterial = material;
            }
            cracks.gameObject.SetActive(false); Set(stage, "groundCracks", cracks.gameObject);
            Set(stage, "debris", Particles(parent, "Debris", center, new Color(0.25f, 0.16f, 0.07f), false));
            Set(stage, "birds", Particles(parent, "Birds", new Vector3(0f, 8f, 10f), Color.black, true));
            var wave = new GameObject("HarmlessWave", typeof(MeshFilter), typeof(MeshRenderer));
            wave.GetComponent<MeshFilter>().sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Models/Hazards/Meshes/Fx_RingWall.asset");
            wave.transform.SetParent(parent, false); wave.transform.position = center + Vector3.up * 0.1f; wave.GetComponent<Renderer>().sharedMaterial = Mat("Wave", new Color(4f, 3f, 2f)); wave.SetActive(false); Set(stage, "wave", wave.transform);
        }
        static ParticleSystem Particles(Transform parent, string name, Vector3 position, Color color, bool birds)
        {
            var t = Node(parent, name, position); var ps = t.gameObject.AddComponent<ParticleSystem>(); ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main; main.playOnAwake = false; main.loop = false; main.duration = 2f; main.startLifetime = birds ? 3f : 1.5f;
            main.startSpeed = birds ? 7f : 5f; main.startSize = birds ? 0.3f : 0.2f; main.startColor = color; main.gravityModifier = birds ? 0f : 0.8f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = ps.emission; emission.rateOverTime = 0; emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)(birds ? 18 : 40)) });
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = birds ? 35f : 55f; shape.radius = birds ? 6f : 2f;
            t.rotation = Quaternion.Euler(-90f, 0f, 0f);
            ps.GetComponent<ParticleSystemRenderer>().sharedMaterial = Mat(name, color);
            return ps;
        }
        static Material Mat(string name, Color color)
        {
            string path = MaterialDirectory + "/" + name + ".mat"; var m = AssetDatabase.LoadAssetAtPath<Material>(path); if (m != null) return m;
            m = new Material(Shader.Find("Universal Render Pipeline/Unlit")); m.SetColor("_BaseColor", color); AssetDatabase.CreateAsset(m, path); return m;
        }
        static void WireAudio(PrologueCinematicStage stage)
        {
            var sources = Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var audio = Object.FindAnyObjectByType<AudioManager>();
            if (audio != null)
            {
                var so = new SerializedObject(audio); var old = so.FindProperty("cutsceneSources");
                var list = new List<AudioSource>(); for (int i = 0; i < old.arraySize; i++) if (old.GetArrayElementAtIndex(i).objectReferenceValue is AudioSource s) list.Add(s);
                list.AddRange(stage.GetComponentsInChildren<AudioSource>(true));
                Array(audio, "cutsceneSources", list.Distinct().ToArray());
            }
            var filters = sources.Select(s => { var f = DaoDunDogBuilder.GetOrAdd<AudioLowPassFilter>(s.gameObject); f.enabled = false; return f; }).Distinct().ToArray();
            Array(stage, "filters", filters);
        }
        static Transform Node(Transform parent, string name, Vector3 world)
        { var t = new GameObject(name).transform; t.SetParent(parent, false); t.position = world; return t; }
        static void Ambient(Transform parent, AudioClip clip, float volume)
        {
            var go = Node(parent, "Ambience_" + clip.name, Vector3.zero);
            var source = go.gameObject.AddComponent<AudioSource>(); source.clip = clip; source.playOnAwake = true; source.loop = true; source.spatialBlend = 0f; source.volume = volume;
        }
        static T Get<T>(string path) where T : ScriptableObject
        { var a = AssetDatabase.LoadAssetAtPath<T>(path); if (a != null) return a; a = ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(a, path); return a; }
        static void Set(Object o, string n, Object v) { var s = new SerializedObject(o); s.FindProperty(n).objectReferenceValue = v; s.ApplyModifiedPropertiesWithoutUndo(); }
        static void Bool(Object o, string n, bool v) { var s = new SerializedObject(o); s.FindProperty(n).boolValue = v; s.ApplyModifiedPropertiesWithoutUndo(); }
        static void Array<T>(Object o, string n, T[] values) where T : Object
        { var s = new SerializedObject(o); var p = s.FindProperty(n); p.arraySize = values.Length; for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i]; s.ApplyModifiedPropertiesWithoutUndo(); }
        public static string Validate()
        {
            var d = AssetDatabase.LoadAssetAtPath<PrologueCutsceneData>(DataPath);
            double[] durations = { d.entranceShots.Sum(), d.transitionDuration, d.knockdownDuration, d.victoryDuration, d.wakeDuration };
            for (int i = 0; i < Names.Length; i++)
            {
                var tl = AssetDatabase.LoadAssetAtPath<TimelineAsset>(TimelineDirectory + "/" + Names[i] + ".playable");
                if (tl == null || Math.Abs(tl.fixedDuration - durations[i]) > 0.01) throw new InvalidOperationException("Timeline 長度不符：" + Names[i]);
                if (!tl.GetOutputTracks().OfType<CinemachineTrack>().Any() || !tl.GetOutputTracks().OfType<AudioTrack>().Any() || !tl.GetOutputTracks().OfType<CinematicValueTrack>().Any()) throw new InvalidOperationException("缺少鏡頭／音軌／曲線：" + Names[i]);
                if (tl.markerTrack == null || !tl.markerTrack.GetMarkers().OfType<CutsceneCueMarker>().Any(m => m.kind == CutsceneCue.SequenceEnd && m.time > tl.fixedDuration - 0.01)) throw new InvalidOperationException("缺正常結束標記：" + Names[i]);
                foreach (var track in tl.GetOutputTracks()) foreach (var clip in track.GetClips())
                {
                    if (clip.duration <= 0 || clip.end > tl.fixedDuration + 0.01) throw new InvalidOperationException("片段越界：" + clip.displayName);
                    if (clip.asset is AudioPlayableAsset sound && (sound.clip == null || sound.clip.samples <= 0)) throw new InvalidOperationException("空音檔：" + clip.displayName);
                    if (clip.asset is CinematicValueClip value && (value.template.curve == null || value.template.curve.length < 2)) throw new InvalidOperationException("空效果曲線");
                }
            }
            int missing = SceneManager.GetActiveScene().GetRootGameObjects().Sum(r => r.GetComponentsInChildren<Transform>(true).Sum(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)));
            if (missing != 0) throw new InvalidOperationException("MissingScript=" + missing);
            foreach (var director in Object.FindObjectsByType<CutsceneDirector>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var so = new SerializedObject(director); if (!so.FindProperty("externalControl").boolValue || !so.FindProperty("manualClock").boolValue) continue;
                var pd = director.GetComponent<PlayableDirector>(); var tl = (TimelineAsset)pd.playableAsset;
                foreach (var track in tl.GetOutputTracks())
                {
                    if (track is MarkerTrack) continue;
                    if (pd.GetGenericBinding(track) == null) throw new InvalidOperationException("未綁軌道：" + track.name);
                    if (track is CinemachineTrack cams) foreach (var c in cams.GetClips()) if (pd.GetReferenceValue(((CinemachineShot)c.asset).VirtualCamera.exposedName, out bool valid) == null || !valid) throw new InvalidOperationException("未綁鏡頭");
                }
            }
            foreach (var stage in Object.FindObjectsByType<PrologueCinematicStage>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Require(stage, "data", "volume", "redFlash", "whiteFlash", "black", "title", "playerStage", "pause");
                var profile = ((Volume)new SerializedObject(stage).FindProperty("volume").objectReferenceValue).sharedProfile;
                if (!profile.TryGet<DepthOfField>(out _) || !profile.TryGet<Vignette>(out _) || !profile.TryGet<ColorAdjustments>(out _)) throw new InvalidOperationException("後處理不齊全");
                if (new SerializedObject(stage).FindProperty("filters").arraySize == 0) throw new InvalidOperationException("未接低通");
            }
            var spawner = Object.FindAnyObjectByType<PlayerSpawner>();
            var receivers = new SerializedObject(spawner).FindProperty("receivers");
            var allReceivers = Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(x => x is IPlayerBinding && !x.transform.IsChildOf(PlayerSpawnerBuilder.Preview().transform));
            foreach (var receiver in allReceivers)
            { bool found = false; for (int j = 0; j < receivers.arraySize; j++) found |= receivers.GetArrayElementAtIndex(j).objectReferenceValue == receiver; if (!found) throw new InvalidOperationException("未 Wire 玩家：" + receiver.GetType().Name); }
            if (SceneManager.GetActiveScene().path == PrologueBuilder.ScenePath)
            {
                Require(Object.FindAnyObjectByType<PrologueCutsceneFlow>(), "tutorial", "guardian", "phaseTwo", "outcome", "session", "dialogue", "mentor", "fullPower", "audioManager", "cameraController", "sceneFlow", "pause", "data", "stage", "entrance", "transition", "knockdown", "victory");
                GuardianPhaseTwoBuilder.ValidatePureRules();
            }
            else
            {
                Require(Object.FindAnyObjectByType<HubPrologueArrival>(), "sceneFlow", "flowData", "hub", "chief", "session", "stage", "wake");
                Require(Object.FindAnyObjectByType<HubManager>(), "inputLock");
            }
            ValidateCompletionProtocol();
            return "[PrologueCutscene] 演出接線檢查通過（五段 Timeline 的長度、軌道綁定、結束標記、後處理、玩家接收者與結局協定）";
        }
        static void Require(Object o, params string[] fields)
        { if (o == null) throw new InvalidOperationException("缺少演出元件"); var so = new SerializedObject(o); foreach (string f in fields) if (so.FindProperty(f)?.objectReferenceValue == null) throw new InvalidOperationException(o.name + " 缺少 " + f); }
        /// <summary>非 Play、隔離物件，只走通知與取消回呼；不呼叫正式結局保存，不讀真實 ProfileStore.Data。</summary>
        public static void ValidateCompletionProtocol()
        {
            int complete = 0, cancel = 0;
            var go = new GameObject("IsolatedProtocol"); go.hideFlags = HideFlags.HideInHierarchy; go.SetActive(false);
            var cue = ScriptableObject.CreateInstance<CutsceneCueMarker>(); cue.kind = CutsceneCue.SequenceEnd;
            try
            {
                var seq = go.AddComponent<CutsceneDirector>(); seq.BindPlayer(null); Bool(seq, "externalControl", true);
                seq.OnCutsceneFinished += _ => complete++; seq.OnCutsceneCancelled += () => cancel++;
                var playing = typeof(CutsceneDirector).GetField("<IsPlaying>k__BackingField", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                var stopped = typeof(CutsceneDirector).GetMethod("HandleStopped", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                playing.SetValue(seq, true);
                stopped.Invoke(seq, new object[] { go.GetComponent<PlayableDirector>() });
                if (cancel != 1 || complete != 0) throw new InvalidOperationException("提早停止被當成正常完成");
                playing.SetValue(seq, true); seq.OnNotify(Playable.Null, cue, null); seq.Cancel(); seq.Cancel();
                if (cancel != 2 || complete != 0) throw new InvalidOperationException("取消重入或冒充完成");
                playing.SetValue(seq, true); seq.OnNotify(Playable.Null, cue, null); stopped.Invoke(seq, new object[] { go.GetComponent<PlayableDirector>() });
                if (cancel != 2 || complete != 1) throw new InvalidOperationException("正常結束未恰好一次交棒");
                // 只呼叫不保存的取消接口，核對錯 token 不吞掉待呈現狀態、正確 token 可去重。
                var outcome = go.AddComponent<PrologueOutcome>();
                var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                typeof(PrologueOutcome).GetField("<PresentationToken>k__BackingField", flags).SetValue(outcome, 7);
                typeof(PrologueOutcome).GetField("<IsVictoryPending>k__BackingField", flags).SetValue(outcome, true);
                outcome.CancelVictoryPresentation(8);
                if (!outcome.IsVictoryPending) throw new InvalidOperationException("錯 token 取消了勝利呈現");
                outcome.CancelVictoryPresentation(7); outcome.CancelVictoryPresentation(7);
                if (outcome.IsVictoryPending) throw new InvalidOperationException("正確 token 未取消");
                var source = new ProfileData();
                if (!ProfileStore.PreparePrologueCompletion(source, PrologueResult.KnockedDown, null, 0, null, null, out var next, out bool first) || first || next.defeatedBosses.Count != 0 || !next.prologueDone || next.prologueResult != PrologueResult.KnockedDown || source.prologueDone) throw new InvalidOperationException("擊倒不應發獎或記擊敗");
                if (ProfileStore.PreparePrologueCompletion(next, PrologueResult.KnockedDown, null, 0, null, null, out _, out _)) throw new InvalidOperationException("擊倒重複提交");
            }
            finally { Object.DestroyImmediate(go); Object.DestroyImmediate(cue); }
        }
    }
}
