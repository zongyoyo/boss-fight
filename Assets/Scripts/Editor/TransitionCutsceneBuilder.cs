using System.Collections.Generic;
using BossFight.Battle;
using BossFight.Boss;
using BossFight.Cameras;
using BossFight.Core;
using BossFight.Cutscene;
using BossFight.Hazards;
using BossFight.Player;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Rendering;
using UnityEngine.Timeline;

namespace BossFight.EditorTools
{
    /// <summary>
    /// 建立轉階段演出（T7）。選單 Tools > Cutscene > Build Transition，可以重複執行；Build Intro 最後也會呼叫這裡。
    /// 1. Timeline（Assets/Timelines/Transition.playable，照企劃書「轉階段演出」5 步驟排好，5.0 秒）只在不存在時建立；
    ///    Tools > Cutscene > Reset Transition Timeline 會刪掉重排。佔位的唱片刮擦聲由 PlaceholderAudioBuilder 產生。
    /// 2. 場景物件 TransitionCutscene 每次刪掉重建：PlayableDirector、CutsceneDirector（不能跳過、沒有字卡）、
    ///    CutsceneStage（Stage 底下以貓為準的鏡頭、PlayerAnchor 底下的狗臉特寫、第 1 步的全景鏡頭）、白閃、煙霧、
    ///    場地邊緣的電光（ElectricArcs）、無傷害衝擊波（ExpandingRing）、Timeline 用的 AudioSource。
    /// 3. 軌道依類型綁定，同類型有好幾條的（CurveTrack、AudioTrack、ElectricArcsTrack）依軌道名稱；Cinemachine 片段依片段名稱接到同名的鏡頭。
    /// 4. BattleManager 的 transition 接到這裡的 CutsceneDirector（BattleManager 會關掉 Boss 的 AutoCompleteTransition）。
    /// 鏡頭在建立時依「玩家出生點 → 場地中心」擺好；執行時 CutsceneStage 依觸發時玩家的位置整組轉過去。位置、顏色都是純視覺參數。
    /// </summary>
    public static class TransitionCutsceneBuilder
    {
        const string TimelinePath = "Assets/Timelines/Transition.playable";
        const string ArenaDataPath = "Assets/ScriptableObjects/Arena/ArenaData.asset";
        const string FxMaterialPath = "Assets/Models/Hazards/Materials/Fx_Additive.mat";
        const string RingWallMeshPath = "Assets/Models/Hazards/Meshes/Fx_RingWall.asset";
        const string NoisePath = "Packages/com.unity.cinemachine/Presets/Noise/6D Shake.asset";
        const string RootName = "TransitionCutscene";

        // 鏡頭（Cinemachine 片段用同樣的名稱對應）
        const string ShotKnockback = "T1_Knockback";
        const string ShotCatFace = "T2_CatFace";
        const string ShotDogFace = "T2_DogFace";
        const string ShotCircle = "T3_CircleOverhead";
        const string ShotSpin = "T4_CatSpin";
        const string ShotWave = "T5_Shockwave";

        // 軌道名稱（同類型有好幾條時用名稱綁定）
        const string TrackFlash = "White Flash";
        const string TrackSmoke = "Smoke";
        const string TrackWave = "Shockwave";
        const string TrackShake = "Camera Shake";
        const string TrackRimArcs = "Rim Arcs";
        const string TrackAura = "Boss Aura";
        const string TrackScratch = "Scratch";
        const string TrackHum = "Hum";
        const string TrackThunder = "Thunder";

        const double Duration = 5.0;
        const float StandoffRadius = 6f; // 第 1 步：狗離貓太近時先退到這個距離，特寫才不會穿模

        [MenuItem("Tools/Cutscene/Build Transition")]
        public static void BuildFromMenu()
        {
            var go = Build();
            if (go != null) Selection.activeGameObject = go;
        }

        [MenuItem("Tools/Cutscene/Reset Transition Timeline")]
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
            var circle = Object.FindAnyObjectByType<MagicCircle>();
            var audio = Object.FindAnyObjectByType<AudioManager>();
            var battle = Object.FindAnyObjectByType<BattleManager>();
            var arenaGen = Object.FindAnyObjectByType<ArenaGenerator>();
            var arena = AssetDatabase.LoadAssetAtPath<ArenaData>(ArenaDataPath);
            var fxMat = AssetDatabase.LoadAssetAtPath<Material>(FxMaterialPath);
            var ringMesh = AssetDatabase.LoadAssetAtPath<Mesh>(RingWallMeshPath);
            if (player == null || cameraController == null || boss == null || brain == null || circle == null || audio == null ||
                battle == null || arena == null || fxMat == null || ringMesh == null)
            {
                Debug.LogError("[Cutscene] 轉階段演出需要場景裡的 PlayerController、CameraController、BossController、CinemachineBrain、" +
                               $"MagicCircle、AudioManager、BattleManager（先跑 Tools > Cutscene > Build Intro），以及 {ArenaDataPath}、" +
                               $"{FxMaterialPath}、{RingWallMeshPath}（Tools > Boss > Build Hazards）。");
                return null;
            }
            var poser = boss.GetComponent<OiiaCatPoser>();
            var visual = boss.GetComponent<BossPhaseVisual>();
            var aura = visual != null ? new SerializedObject(visual).FindProperty("aura").objectReferenceValue as ElectricArcs : null;
            if (poser == null || aura == null)
            {
                Debug.LogError("[Cutscene] Boss 身上沒有 OiiaCatPoser 或 BossPhaseVisual 的電光，請先跑 Tools > Boss > Build Boss Placeholder。");
                return null;
            }

            var sounds = PlaceholderAudioBuilder.Build();
            Undo.SetCurrentGroupName("Build Transition Cutscene");
            int undoGroup = Undo.GetCurrentGroup();

            var old = GameObject.Find(RootName);
            if (old != null) Undo.DestroyObjectImmediate(old);

            Vector3 center = arenaGen != null ? arenaGen.transform.position : Vector3.zero;
            float gateAngle = arena.TryGetGate("PlayerGate", out var gate) ? gate.angle : 180f;
            Vector3 fwd = -ArenaData.Dir(gateAngle); // 預設：狗在出生點、看向貓

            var root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "Build Transition Cutscene");
            var director = root.AddComponent<PlayableDirector>();
            director.playOnAwake = false;
            director.extrapolationMode = DirectorWrapMode.None;
            director.timeUpdateMode = DirectorUpdateMode.GameTime;
            var cutscene = root.AddComponent<CutsceneDirector>();
            DaoDunDogBuilder.SetReference(cutscene, "player", player);
            DaoDunDogBuilder.SetReference(cutscene, "cameraController", cameraController);
            DaoDunDogBuilder.SetReference(cutscene, "faceTarget", boss.transform);

            // ---------- 舞台與鏡頭 ----------
            var stageTf = DaoDunDogBuilder.Node(root.transform, "Stage", Vector3.zero);
            stageTf.SetPositionAndRotation(center, Quaternion.LookRotation(fwd));
            var anchor = DaoDunDogBuilder.Node(root.transform, "PlayerAnchor", Vector3.zero);
            anchor.SetPositionAndRotation(center - fwd * arena.playerSpawnDistance, stageTf.rotation);
            var midpoint = DaoDunDogBuilder.Node(root.transform, "Midpoint", Vector3.zero);
            midpoint.position = center - fwd * (arena.playerSpawnDistance * 0.5f) + Vector3.up * 1.1f;

            var freeLook = new SerializedObject(cameraController).FindProperty("freeLookCamera").objectReferenceValue as CinemachineCamera;
            var lens = freeLook != null ? freeLook.Lens : LensSettings.Default;
            var shots = BuildShots(root.transform, stageTf, anchor, midpoint, boss, player, lens);

            var stage = stageTf.gameObject.AddComponent<CutsceneStage>();
            var sso = new SerializedObject(stage);
            sso.FindProperty("player").objectReferenceValue = player;
            sso.FindProperty("boss").objectReferenceValue = boss.transform;
            sso.FindProperty("arenaCenter").objectReferenceValue = arenaGen != null ? arenaGen.transform : null;
            sso.FindProperty("playerAnchor").objectReferenceValue = anchor;
            sso.FindProperty("midpoint").objectReferenceValue = midpoint;
            sso.FindProperty("openingShot").objectReferenceValue = shots[ShotKnockback].transform;
            sso.FindProperty("openingShotMaxRadius").floatValue = arena.radius - 1.5f;
            sso.ApplyModifiedPropertiesWithoutUndo();
            DaoDunDogBuilder.SetReference(cutscene, "stage", stage);

            // ---------- 效果 ----------
            var fx = DaoDunDogBuilder.Node(root.transform, "Fx", Vector3.zero);
            var flash = IntroCutsceneBuilder.BuildFlash(root.transform);
            var smoke = IntroCutsceneBuilder.BuildSmoke(fx, IntroCutsceneBuilder.GetSmokeMaterial(), center);
            var rim = BuildRimArcs(fx, fxMat, center, arena);
            var wave = BuildShockwave(fx, fxMat, ringMesh, center, arena);
            var shaker = shots[ShotWave].GetComponent<CameraShaker>();
            var sources = BuildAudio(root.transform);

            // ---------- Timeline ----------
            var timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(TimelinePath);
            if (timeline == null) timeline = CreateTimeline(center, arena, sounds);
            director.playableAsset = timeline;

            var named = new Dictionary<string, Object>
            {
                { TrackFlash, flash },
                { TrackSmoke, smoke },
                { TrackWave, wave },
                { TrackShake, shaker },
                { TrackRimArcs, rim },
                { TrackAura, aura },
                { TrackScratch, sources[TrackScratch] },
                { TrackHum, sources[TrackHum] },
                { TrackThunder, sources[TrackThunder] },
            };
            Bind(director, timeline, brain, shots, named, player, boss, circle, poser, audio);

            // 戰鬥流程：Boss 進入轉階段時由 BattleManager 播這段，播完才套用第二階段
            DaoDunDogBuilder.SetReference(battle, "transition", cutscene);
            DaoDunDogBuilder.SetReference(battle, "audioManager", audio);
            PauseMenuBuilder.Wire(); // 演出的音軌 AudioSource 重建了：音效音量要重接

            Undo.CollapseUndoOperations(undoGroup);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(root.scene);
            Debug.Log($"[Cutscene] 轉階段演出已建立：{TimelinePath}（{timeline.duration:0.0} 秒），BattleManager 已接上");
            return root;
        }

        // ---------- 鏡頭 ----------

        // Stage 底下以貓為準（原點是場地中心、+Z 是狗看貓的方向，貓面向 −Z）；PlayerAnchor 底下以狗為準（+Z 朝向貓）
        static Dictionary<string, CinemachineCamera> BuildShots(Transform root, Transform stage, Transform anchor, Transform midpoint,
                                                                BossController boss, PlayerController player, LensSettings lens)
        {
            Transform Target(Transform parent, string name, Vector3 local)
            {
                var t = DaoDunDogBuilder.Node(parent, name, Vector3.zero);
                t.localPosition = local;
                return t;
            }

            // 貓臉：兩眼中間稍低（貓面向 −Z，所以眼睛的區域座標轉 180 度）
            Vector3 catFace = new Vector3(0f, 1.83f, -0.7f);
            var eyeL = boss.transform.Find("Model/EyeL");
            var eyeR = boss.transform.Find("Model/EyeR");
            if (eyeL != null && eyeR != null)
            {
                Vector3 mid = (boss.transform.InverseTransformPoint(eyeL.position) + boss.transform.InverseTransformPoint(eyeR.position)) * 0.5f;
                catFace = Quaternion.Euler(0f, 180f, 0f) * mid + Vector3.down * 0.12f;
            }
            // 狗臉：頭在角色上的位置（狗面向 +Z）
            Vector3 dogFace = Vector3.up;
            var head = player.transform.Find("Model/Body/Head");
            if (head != null) dogFace = player.transform.InverseTransformPoint(head.position) + Vector3.up * 0.15f;

            var targets = DaoDunDogBuilder.Node(stage, "Targets", Vector3.zero);
            var catFaceT = Target(targets, "CatFace", catFace);
            var catBody = Target(targets, "CatBody", new Vector3(0f, 1.3f, 0f));
            var circleCenter = Target(targets, "CircleCenter", new Vector3(0f, 0f, -1.5f));
            var waveFocus = Target(targets, "WaveFocus", new Vector3(0f, 0.6f, -5.5f));
            var dogFaceT = Target(anchor, "DogFace", dogFace);

            var shots = new Dictionary<string, CinemachineCamera>();
            CinemachineCamera Add(Transform parent, string name, Vector3 local, Transform lookAt, float fov)
            {
                var cam = IntroCutsceneBuilder.Shot(parent, name, parent.TransformPoint(local), lookAt, fov, lens);
                shots[name] = cam;
                return cam;
            }

            // 第 1 步的全景鏡頭：位置由 CutsceneStage.Align 依玩家與 Boss 擺
            Add(root, ShotKnockback, root.InverseTransformPoint(midpoint.position + stage.right * 7f + Vector3.up * 3.4f), midpoint, 50f);
            Add(stage, ShotCatFace, catFace + new Vector3(0f, -0.15f, -1.9f), catFaceT, 38f);
            Add(anchor, ShotDogFace, dogFace + new Vector3(0f, 0.05f, 2.1f), dogFaceT, 38f);
            Add(stage, ShotCircle, new Vector3(0f, 21f, -8f), circleCenter, 62f);
            Add(stage, ShotSpin, new Vector3(2.4f, 1.6f, -5.2f), catBody, 45f);
            var waveCam = Add(stage, ShotWave, new Vector3(6.5f, 7.5f, -3.5f), waveFocus, 55f);

            // 第 5 步：衝擊波放出時震動
            var noise = waveCam.gameObject.AddComponent<CinemachineBasicMultiChannelPerlin>();
            noise.NoiseProfile = AssetDatabase.LoadAssetAtPath<NoiseSettings>(NoisePath);
            noise.AmplitudeGain = 0f;
            noise.FrequencyGain = 2f;
            var shaker = waveCam.gameObject.AddComponent<CameraShaker>();
            DaoDunDogBuilder.SetReference(shaker, "noise", noise);
            return shots;
        }

        // ---------- 效果 ----------

        static ElectricArcs BuildRimArcs(Transform parent, Material mat, Vector3 center, ArenaData arena)
        {
            var go = DaoDunDogBuilder.Node(parent, "RimArcs", Vector3.zero);
            go.position = center;
            var arcs = go.gameObject.AddComponent<ElectricArcs>();
            var so = new SerializedObject(arcs);
            so.FindProperty("material").objectReferenceValue = mat;
            so.FindProperty("layout").enumValueIndex = (int)ArcLayout.Rim;
            so.FindProperty("arcCount").intValue = 28;
            so.FindProperty("points").intValue = 7;
            so.FindProperty("radius").floatValue = arena.radius - 0.4f;
            so.FindProperty("baseHeight").floatValue = 0f;
            so.FindProperty("height").floatValue = 1.8f;
            so.FindProperty("jitter").floatValue = 0.2f;
            so.FindProperty("width").floatValue = 0.07f;
            so.FindProperty("flickerInterval").floatValue = 0.05f;
            so.FindProperty("showChance").floatValue = 0.6f;
            so.ApplyModifiedPropertiesWithoutUndo();
            return arcs;
        }

        static ExpandingRing BuildShockwave(Transform parent, Material mat, Mesh ringMesh, Vector3 center, ArenaData arena)
        {
            var go = DaoDunDogBuilder.Node(parent, "Shockwave", Vector3.zero);
            go.position = center;
            var wall = DaoDunDogBuilder.Node(go, "Wall", Vector3.zero);
            wall.gameObject.AddComponent<MeshFilter>().sharedMesh = ringMesh;
            var r = wall.gameObject.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.enabled = false;
            var ring = go.gameObject.AddComponent<ExpandingRing>();
            var so = new SerializedObject(ring);
            so.FindProperty("wall").objectReferenceValue = wall;
            so.FindProperty("wallRenderer").objectReferenceValue = r;
            so.FindProperty("endRadius").floatValue = arena.radius;
            so.ApplyModifiedPropertiesWithoutUndo();
            return ring;
        }

        static Dictionary<string, AudioSource> BuildAudio(Transform root)
        {
            var parent = DaoDunDogBuilder.Node(root, "Audio", Vector3.zero);
            var d = new Dictionary<string, AudioSource>();
            void Add(string track, float volume)
            {
                var src = DaoDunDogBuilder.Node(parent, "Audio_" + track.Replace(" ", ""), Vector3.zero).gameObject.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.spatialBlend = 0f;
                src.volume = volume;
                d[track] = src;
            }
            Add(TrackScratch, 1f);
            Add(TrackHum, 0.8f);
            Add(TrackThunder, 0.8f);
            return d;
        }

        // ---------- Timeline ----------

        // 照企劃書「轉階段演出」排好的預設時間軸（5.0 秒）；之後在 Timeline 視窗調整
        static TimelineAsset CreateTimeline(Vector3 center, ArenaData arena, PlaceholderAudioBuilder.Result sounds)
        {
            DaoDunDogBuilder.EnsureFolder("Assets/Timelines");
            var tl = ScriptableObject.CreateInstance<TimelineAsset>();
            tl.name = "Transition";
            AssetDatabase.CreateAsset(tl, TimelinePath);
            tl.durationMode = TimelineAsset.DurationMode.FixedLength;
            tl.fixedDuration = Duration;

            // 鏡頭：1 全景 → 2 貓臉、狗臉特寫（呼應開場第 5 鏡）→ 3 俯瞰法陣 → 4 貓旋轉中景 → 5 衝擊波，最後 0.6 秒混回玩家的第三人稱鏡頭
            var cams = tl.CreateTrack<CinemachineTrack>(null, "Cameras");
            IntroCutsceneBuilder.ShotClip(cams, ShotKnockback, 0.0, 1.0);
            IntroCutsceneBuilder.ShotClip(cams, ShotCatFace, 1.0, 0.5);
            IntroCutsceneBuilder.ShotClip(cams, ShotDogFace, 1.5, 0.5);
            IntroCutsceneBuilder.ShotClip(cams, ShotCircle, 2.0, 1.2);
            IntroCutsceneBuilder.ShotClip(cams, ShotSpin, 3.2, 1.0);
            IntroCutsceneBuilder.ShotClip(cams, ShotWave, 4.2, 0.8).easeOutDuration = 0.6;

            // 1. Boss 被擊退，0.6 白閃加煙霧瞬移回法陣中央、面向狗；狗太近時先退到對視距離
            var bossTrack = tl.CreateTrack<BossActorTrack>(null, "Boss");
            var knock = IntroCutsceneBuilder.Clip<BossActorClip>(bossTrack, "Knockback", 0.0, 0.35);
            ((BossActorClip)knock.asset).template.action = BossActorAction.Knockback;
            var back = IntroCutsceneBuilder.Clip<BossActorClip>(bossTrack, "Return To Center", 0.6, Duration - 0.6);
            ((BossActorClip)back.asset).template.action = BossActorAction.ReturnToCenter;
            IntroCutsceneBuilder.CurveClipAt(tl.CreateTrack<CurveTrack>(null, TrackFlash), "Blink", 0.6, 0.15, AnimationCurve.Linear(0f, 1f, 1f, 0f), 0.9f);
            IntroCutsceneBuilder.CurveClipAt(tl.CreateTrack<CurveTrack>(null, TrackSmoke), "Appear", 0.6, 1.0, AnimationCurve.Linear(0f, 0.02f, 1f, 1f), 1f);

            var dogTrack = tl.CreateTrack<ActorPushTrack>(null, "Dog Push");
            var standoff = IntroCutsceneBuilder.Clip<ActorPushClip>(dogTrack, "Standoff", 0.05, 0.5);
            var sb = ((ActorPushClip)standoff.asset).template;
            sb.center = center;
            sb.arena = null;
            sb.radius = StandoffRadius;

            // 2. 唱片刮擦、音樂停，靜音 1 秒
            var music = tl.CreateTrack<MusicCueTrack>(null, "Music");
            var stop = IntroCutsceneBuilder.Clip<MusicCueClip>(music, "Stop", 1.0, 0.1);
            ((MusicCueClip)stop.asset).template.action = MusicCueAction.Stop;
            IntroCutsceneBuilder.AudioClipAt(tl.CreateTrack<AudioTrack>(null, TrackScratch), sounds.scratch, 1.0, 0.55, false);

            // 3. 法陣由紫轉紅、擴大到場地邊緣；邊緣冒出電光
            var color = tl.CreateTrack<MagicCircleTrack>(null, "Magic Circle Color");
            color.channel = MagicCircleChannel.Color;
            IntroCutsceneBuilder.Hold(IntroCutsceneBuilder.CurveClipAt(color, "Purple To Red", 2.0, 0.8, AnimationCurve.EaseInOut(0f, 0f, 1f, 1f), 1f));
            var expand = tl.CreateTrack<MagicCircleTrack>(null, "Magic Circle Expand");
            expand.channel = MagicCircleChannel.Expand;
            IntroCutsceneBuilder.Hold(IntroCutsceneBuilder.CurveClipAt(expand, "Expand", 2.0, 1.0, AnimationCurve.EaseInOut(0f, 0f, 1f, 1f), 1f));
            IntroCutsceneBuilder.CurveClipAt(tl.CreateTrack<ElectricArcsTrack>(null, TrackRimArcs), "Rim Sparks", 2.5, Duration - 2.5,
                                             new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.15f, 1f), new Keyframe(0.75f, 1f), new Keyframe(1f, 0f)), 1f);
            IntroCutsceneBuilder.AudioClipAt(tl.CreateTrack<AudioTrack>(null, TrackHum), sounds.hum, 2.0, 1.2, false);

            // 4. 貓開始旋轉、身上纏繞紅色閃電；OIIA 音樂以第二階段的速度重新播放（BattleManager 演出開始時已排好速度）
            IntroCutsceneBuilder.Hold(IntroCutsceneBuilder.CurveClipAt(tl.CreateTrack<ElectricArcsTrack>(null, TrackAura), "Red Lightning", 3.2, Duration - 3.2,
                                                                       new AnimationCurve(new Keyframe(0f, 0.2f), new Keyframe(0.3f, 1f), new Keyframe(1f, 1f)), 1f));
            IntroCutsceneBuilder.CurveClipAt(tl.CreateTrack<BossSpinTrack>(null, "Cat Spin"), "Spin", 3.2, Duration - 3.2,
                                             new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.5f, 1f), new Keyframe(0.78f, 1f), new Keyframe(1f, 0f)), 1440f);
            var restart = IntroCutsceneBuilder.Clip<MusicCueClip>(music, "Restart", 3.2, 0.1);
            ((MusicCueClip)restart.asset).template.action = MusicCueAction.Restart;
            var thunder = tl.CreateTrack<AudioTrack>(null, TrackThunder);
            IntroCutsceneBuilder.AudioClipAt(thunder, sounds.thunder, 2.5, 1.5, false);

            // 5. 無傷害衝擊波把狗推到外圈（ArenaData.transitionPushRadius），鏡頭震動
            IntroCutsceneBuilder.CurveClipAt(tl.CreateTrack<CurveTrack>(null, TrackWave), "Wave", 4.2, 0.65, AnimationCurve.Linear(0f, 0.02f, 1f, 1f), 1f);
            IntroCutsceneBuilder.CurveClipAt(tl.CreateTrack<CurveTrack>(null, TrackShake), "Shake", 4.2, 0.4, AnimationCurve.Linear(0f, 1f, 1f, 0f), 2f);
            var push = IntroCutsceneBuilder.Clip<ActorPushClip>(dogTrack, "Pushed Out", 4.25, 0.6);
            var pb = ((ActorPushClip)push.asset).template;
            pb.center = center;
            pb.arena = arena;
            IntroCutsceneBuilder.AudioClipAt(thunder, sounds.thunder, 4.2, 0.8, false);

            EditorUtility.SetDirty(tl);
            AssetDatabase.SaveAssets();
            return tl;
        }

        // 每條軌道綁到場景物件；Cinemachine 片段依名稱接到同名的鏡頭
        static void Bind(PlayableDirector director, TimelineAsset tl, CinemachineBrain brain, Dictionary<string, CinemachineCamera> shots,
                         Dictionary<string, Object> named, PlayerController player, BossController boss, MagicCircle circle,
                         OiiaCatPoser poser, AudioManager audio)
        {
            foreach (var track in tl.GetOutputTracks())
            {
                if (track is MarkerTrack) continue;
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
                    case BossActorTrack _: binding = boss; break;
                    case ActorPushTrack _: binding = player; break;
                    case ActorWalkTrack _: binding = player; break;
                    case MagicCircleTrack _: binding = circle; break;
                    case BossSpinTrack _: binding = poser; break;
                    case MusicCueTrack _: binding = audio; break;
                    default:
                        named.TryGetValue(track.name, out binding);
                        break;
                }
                if (binding == null) Debug.LogWarning($"[Cutscene] 轉階段 Timeline 軌道「{track.name}」沒有對應的場景物件，請手動綁定。");
                director.SetGenericBinding(track, binding);
            }
        }
    }
}
