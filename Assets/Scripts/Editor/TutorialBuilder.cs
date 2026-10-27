using System;
using System.Linq;
using BossFight.Battle;
using BossFight.Boss;
using BossFight.Core;
using BossFight.Hazards;
using BossFight.Hub;
using BossFight.Player;
using BossFight.Progression;
using BossFight.UI;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace BossFight.EditorTools
{
    /// <summary>T27 可重建教學接線；保留 T25 地形鏡頭及 T26 已驗收角色。</summary>
    public static class TutorialBuilder
    {
        public const string DataPath = "Assets/ScriptableObjects/Tutorial/Tutorial.asset";
        public const string UIPath = "Assets/ScriptableObjects/UI/TutorialUIData.asset";
        const string StepsDir = "Assets/ScriptableObjects/Tutorial/Steps";
        const string FeedbackPath = "Assets/ScriptableObjects/Feedback/CombatFeedback.asset";
        const string StylePath = "Assets/ScriptableObjects/Hub/HubUIData.asset";

        [MenuItem("Tools/Prologue/Build Tutorial (T27)")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("請先停止 Play。");
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != PrologueBuilder.ScenePath) throw new InvalidOperationException("請開啟 Prologue。");
            var mentor = Object.FindAnyObjectByType<DaisyMentor>();
            var sapling = Object.FindAnyObjectByType<TrainingSapling>();
            if (mentor == null || sapling == null) throw new InvalidOperationException("先完成 T26 角色。");
            EnsureData();
            ApplyLockOnCamera();
            var anchors = GameObject.Find("ActorAnchors").transform;
            var layout = AssetDatabase.LoadAssetAtPath<PrologueLayoutData>(PrologueBuilder.LayoutPath);
            var saplingAnchor = anchors.Find("Sapling");
            saplingAnchor.position = new Vector3(layout.saplingPosition.x, saplingAnchor.position.y, layout.saplingPosition.y);
            sapling.transform.position = saplingAnchor.position;
            PrefabUtility.RecordPrefabInstancePropertyModifications(sapling.transform);
            WireActorPrefab(PrologueActorsBuilder.DaisyPrefabPath, true);
            WireActorPrefab(PrologueActorsBuilder.SaplingPrefabPath, false);
            WireActor(mentor.gameObject, true, Object.FindAnyObjectByType<AudioManager>());
            WireActor(sapling.gameObject, false, Object.FindAnyObjectByType<AudioManager>());
            Set(Object.FindAnyObjectByType<PrologueActorBinding>(), "saplingFeedback", sapling.GetComponent<BossHitFeedback>());
            SetBool(mentor, "emergeOnStart", false);
            SetBool(sapling, "appearOnStart", false);
            var previous = GameObject.Find("Tutorial");
            if (previous != null) Undo.DestroyObjectImmediate(previous);
            var root = new GameObject("Tutorial");
            var dialogue = root.AddComponent<DialogueSession>();
            var director = root.AddComponent<TutorialDirector>();
            Set(director, "data", AssetDatabase.LoadAssetAtPath<TutorialData>(DataPath));
            Set(director, "session", Object.FindAnyObjectByType<PrologueSession>());
            Set(director, "dialogue", dialogue); Set(director, "mentor", mentor); Set(director, "sapling", sapling);
            Set(director, "startCenter", anchors.Find("Daisy"));
            var visuals = root.AddComponent<TutorialPlayerVisual>();
            Set(visuals, "slashPrefab", Fx("Fx_ClawSlash"));
            Set(visuals, "ringPrefab", Fx("Fx_ImpactRing"));
            Set(visuals, "sparkPrefab", Fx("Fx_HitSpark"));
            BuildUI(root.transform, director, dialogue);
            PlayerSpawnerBuilder.Wire();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[T27] 六步教學、共用對話、HUD、角色回饋已建立並接線。");
        }

        [MenuItem("Tools/Prologue/Upgrade Hub Dialogue (T27)")]
        public static void UpgradeHubDialogue()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("請先停止 Play。");
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != "Assets/Scenes/Hub.unity") throw new InvalidOperationException("請開啟 Hub。");
            var box = Object.FindAnyObjectByType<DialogueUI>(FindObjectsInactive.Include);
            HubUIBuilder.WireDialogue(box.gameObject, Object.FindAnyObjectByType<HubManager>(),
                Object.FindAnyObjectByType<VillageChief>(), Object.FindAnyObjectByType<EventSystem>(),
                AssetDatabase.LoadAssetAtPath<HubUIData>(StylePath));
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[T27] Hub 原對話框已升級，未重建站點或場景。");
        }

        static void EnsureData()
        {
            DaoDunDogBuilder.EnsureFolder(StepsDir);
            if (AssetDatabase.LoadAssetAtPath<TutorialUIData>(UIPath) == null)
                AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<TutorialUIData>(), UIPath);
            if (AssetDatabase.LoadAssetAtPath<TutorialData>(DataPath) != null) return;
            var data = ScriptableObject.CreateInstance<TutorialData>();
            data.speaker = "神祕小花";
            data.initialBones = 0; data.grantedBones = 1;
            data.completionDelay = 0.5f; data.endingPause = 1f;
            data.opening = new[] {
                Line("又來一個懵懂的冒險者？"), Line("沒事，我來教你。", TutorialCue.Summon), Line("就把這個當訓練靶吧。") };
            data.ending = new[] {
                Line("不錯嘛，你已經是個合格的冒險者了，可以去挑戰大樹守衛了。"),
                Line("順帶一提，你剛剛打到的小樹精是大樹守衛的小孩喔，嘻嘻。") };
            data.parryPraise = "喔！抓準時機還能把它彈回去，有天分。";
            data.prankLine = "打我幹嘛啦！是打那邊！";
            data.steps = new[] {
                Step("01_Move", TutorialCondition.Move, 4f, "Player/Move", "[{0}] 移動", SaplingMode.Idle, true,
                    new[] { Line("這是移動。先走兩步給我看看。") }, "嗯，至少腳是好的。"),
                Step("02_Heal", TutorialCondition.Heal, 1f, "Player/Heal", "[{0}] 啃骨頭回血", SaplingMode.Idle, true,
                    new[] { Line("接下來是冒險者最重要的一課……", TutorialCue.Whip),
                        Line("痛嗎？嘻嘻，外面的世界可不會先打招呼喔。"),
                        Line("這是啃骨頭。拿去，受傷了就啃一根。", TutorialCue.GiveBone) }, "吃得真香，回滿了吧？那我們繼續。"),
                Step("03_Dodge", TutorialCondition.Dodge, 2f, "Player/Roll", "[{0}] 翻滾閃避", SaplingMode.DodgeAttack, true,
                    new[] { Line("這是閃避。剛剛那下要是會滾，就不會痛了。小樹精要甩樹枝了，紅光這招不能擋，打到之前滾開！") }, "滾得挺圓的嘛。"),
                Step("04_Block", TutorialCondition.Block, 2f, "Player/Block", "按住 [{0}] 格擋", SaplingMode.Attack, true,
                    new[] { Line("這是格擋。看清楚，這次是白光。一直滾會累，用盾擋下來也行。") }, "擋得住，就不怕了。"),
                Step("05_Attack", TutorialCondition.Hit, 3f, "Player/Attack", "[{0}] 攻擊", SaplingMode.Target, false,
                    new[] { Line("這是攻擊。好啦，換你打它，先砍三刀。") }, "砍得不錯，可是它還站著呢。"),
                Step("06_Execute", TutorialCondition.Execute, 1f, "Player/Execute", "靠近按 [{0}] 處決", SaplingMode.Target, false,
                    new[] { Line("這是處決。看好了——", TutorialCue.Slap), Line("它暈了！快，靠近一口氣解決它。") }, string.Empty)
            };
            AssetDatabase.CreateAsset(data, DataPath);
        }
        static TutorialLine Line(string text, TutorialCue cue = TutorialCue.None) => new TutorialLine { text = text, after = cue };
        static TutorialStepData Step(string id, TutorialCondition condition, float target, string action, string hint,
            SaplingMode mode, bool invulnerable, TutorialLine[] opening, string praise)
        {
            string path = StepsDir + "/" + id + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<TutorialStepData>(path);
            if (existing != null) return existing;
            var step = ScriptableObject.CreateInstance<TutorialStepData>();
            step.id = id; step.condition = condition; step.target = target; step.actionPath = action; step.hintFormat = hint;
            step.mode = mode; step.invulnerable = invulnerable; step.opening = opening; step.praise = praise;
            AssetDatabase.CreateAsset(step, path);
            return step;
        }

        static TimedFx Fx(string name) => AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Hazards/" + name + ".prefab").GetComponent<TimedFx>();
        static void WireActorPrefab(string path, bool isMentor)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try { WireActor(root, isMentor, null); PrefabUtility.SaveAsPrefabAsset(root, path); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        static void WireActor(GameObject root, bool isMentor, AudioManager audio)
        {
            var emitter = DaoDunDogBuilder.GetOrAdd<FeedbackEmitter>(root);
            var impulse = DaoDunDogBuilder.GetOrAdd<CinemachineImpulseSource>(root);
            Set(emitter, "data", AssetDatabase.LoadAssetAtPath<CombatFeedbackData>(FeedbackPath));
            Set(emitter, "audioManager", audio); Set(emitter, "impulse", impulse);
            var feedback = DaoDunDogBuilder.GetOrAdd<TeachingActorFeedback>(root);
            Set(feedback, "mentor", isMentor ? root.GetComponent<DaisyMentor>() : null);
            Set(feedback, "sapling", isMentor ? null : root.GetComponent<TrainingSapling>());
            Set(feedback, "sparkPrefab", Fx("Fx_HitSpark"));
            Set(feedback, "impactAnchor", root.transform.Find(isMentor ? "Model/Flower" : "AimPoint"));
            if (!isMentor) PrologueActorsBuilder.WireSaplingFeedback(root.GetComponent<TrainingSapling>());
        }
        static void BuildUI(Transform parent, TutorialDirector director, DialogueSession session)
        {
            var root = new GameObject("TutorialUI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.transform.SetParent(parent, false);
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            root.GetComponent<Canvas>().sortingOrder = 6;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f); scaler.matchWidthOrHeight = 0.5f;
            var uiData = AssetDatabase.LoadAssetAtPath<TutorialUIData>(UIPath);
            var hint = Rect("Hint", root.transform, new Vector2(0.5f, 1f), new Vector2(0f, -uiData.top), uiData.size);
            var hintGroup = hint.gameObject.AddComponent<CanvasGroup>();
            hintGroup.blocksRaycasts = hintGroup.interactable = false; hintGroup.alpha = 0f;
            var hintLabel = Label("Text", hint, Vector2.zero, uiData.size, uiData.fontSize, TextAnchor.MiddleCenter);
            var hud = root.AddComponent<TutorialHudUI>();
            Set(hud, "director", director); Set(hud, "data", uiData); Set(hud, "group", hintGroup); Set(hud, "label", hintLabel);
            Set(hud, "hintDevice", Object.FindAnyObjectByType<InputHintDevice>());
            Set(hud, "inputNames", AssetDatabase.LoadAssetAtPath<InputNameData>("Assets/ScriptableObjects/UI/InputNameData.asset"));
            var battleUI = AssetDatabase.LoadAssetAtPath<BattleUIData>("Assets/ScriptableObjects/UI/BattleUIData.asset");
            var marker = BattleUIBuilder.BuildLockMarker(root.transform as RectTransform, battleUI, UnityEngine.Camera.main);
            Set(marker.parent.GetComponent<LockOnMarkerUI>(), "executionMarker", Object.FindAnyObjectByType<ExecutionMarkerUI>());
            var style = AssetDatabase.LoadAssetAtPath<HubUIData>(StylePath);
            var box = Rect("Dialogue", root.transform, new Vector2(0.5f, 0f), new Vector2(0f, style.dialogueY), style.dialogueSize);
            box.pivot = new Vector2(0.5f, 0f);
            var image = box.gameObject.AddComponent<Image>(); image.color = style.dialogueBoxColor;
            image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"); image.type = Image.Type.Sliced;
            var group = box.gameObject.AddComponent<CanvasGroup>(); group.alpha = 0f;
            var padding = uiData.dialoguePadding;
            var speaker = Label("Speaker", box, Vector2.zero, new Vector2(style.dialogueSize.x - padding.x * 2f, uiData.speakerHeight), 36, TextAnchor.MiddleLeft);
            PlaceAtTop(speaker.rectTransform, new Vector2(padding.x, -padding.y));
            speaker.color = style.speakerColor;
            float bodyTop = padding.y + uiData.speakerHeight + uiData.speakerBodyGap;
            var body = Label("Body", box, Vector2.zero, new Vector2(style.dialogueSize.x - padding.x * 2f - 40f,
                style.dialogueSize.y - bodyTop - padding.y), 36, TextAnchor.UpperLeft);
            PlaceAtTop(body.rectTransform, new Vector2(padding.x, -bodyTop));
            body.horizontalOverflow = HorizontalWrapMode.Wrap; body.lineSpacing = 1.15f;
            var mark = Label("Continue", box, new Vector2(style.dialogueSize.x * 0.5f - 50f, -90f), new Vector2(40f, 40f), 30, TextAnchor.MiddleCenter);
            mark.color = style.speakerColor;
            var dialogue = box.gameObject.AddComponent<DialogueUI>();
            Set(dialogue, "data", style); Set(dialogue, "session", session); Set(dialogue, "pause", Object.FindAnyObjectByType<GamePause>());
            Set(dialogue, "group", group); Set(dialogue, "speakerText", speaker); Set(dialogue, "bodyText", body); Set(dialogue, "continueMark", mark);
        }
        static void PlaceAtTop(RectTransform rect, Vector2 position)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
        }
        /// <summary>只調序章的鎖定鏡頭；可在重建鏡頭後單獨重接。</summary>
        public static void ApplyLockOnCamera()
        {
            var controller = Object.FindAnyObjectByType<BossFight.Cameras.CameraController>();
            if (controller == null) throw new InvalidOperationException("缺少序章鏡頭。");
            var layout = AssetDatabase.LoadAssetAtPath<PrologueLayoutData>(PrologueBuilder.LayoutPath);
            var orbit = new SerializedObject(controller).FindProperty("lockOnOrbit").objectReferenceValue as CinemachineOrbitalFollow;
            if (orbit == null || layout == null) throw new InvalidOperationException("缺少鎖定鏡頭或序章版面資料。");
            Undo.RecordObject(orbit, "Set Prologue Lock On Pitch");
            orbit.VerticalAxis.Value = orbit.VerticalAxis.ClampValue(layout.lockOnPitch);
            orbit.VerticalAxis.Center = orbit.VerticalAxis.Value;
            EditorUtility.SetDirty(orbit);
        }
        static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var root = new GameObject(name, typeof(RectTransform));
            var rect = root.GetComponent<RectTransform>(); rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = anchor; rect.anchoredPosition = position; rect.sizeDelta = size;
            return rect;
        }
        static Text Label(string name, Transform parent, Vector2 position, Vector2 size, int fontSize, TextAnchor alignment)
        {
            var rect = Rect(name, parent, new Vector2(0.5f, 0.5f), position, size);
            var label = rect.gameObject.AddComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); label.fontSize = fontSize;
            label.alignment = alignment; label.color = Color.white; label.raycastTarget = false;
            return label;
        }
        static void Set(Object target, string name, Object value)
        {
            var so = new SerializedObject(target); so.FindProperty(name).objectReferenceValue = value; so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }
        static void SetBool(Object target, string name, bool value)
        {
            var so = new SerializedObject(target); so.FindProperty(name).boolValue = value; so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.RecordPrefabInstancePropertyModifications(target);
        }

        public static string Validate()
        {
            ValidateRules();
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            int missing = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true))
                .Sum(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject));
            if (missing > 0) throw new InvalidOperationException("Missing Script: " + missing);
            if (scene.path == "Assets/Scenes/Hub.unity")
            {
                Require(Object.FindAnyObjectByType<HubDialogueUI>(FindObjectsInactive.Include), "data", "hub", "chief", "session", "choiceCanvas", "choiceGroup", "questText", "acceptButton", "declineButton");
                Require(Object.FindAnyObjectByType<DialogueUI>(FindObjectsInactive.Include), "data", "session", "group", "speakerText", "bodyText", "continueMark");
                return "T27 Hub PASS: MissingScript=0; generic dialogue + quest adapter refs OK";
            }
            if (scene.path != PrologueBuilder.ScenePath) throw new InvalidOperationException("請開啟序章或休息區。");
            var director = Object.FindAnyObjectByType<TutorialDirector>();
            Require(director, "data", "session", "dialogue", "mentor", "sapling", "startCenter");
            Require(Object.FindAnyObjectByType<PrologueActorBinding>(), "mentor", "sapling", "session", "saplingFeedback");
            Require(Object.FindAnyObjectByType<TutorialHudUI>(), "director", "data", "hintDevice", "inputNames", "group", "label");
            Require(Object.FindAnyObjectByType<DialogueUI>(), "data", "session", "pause", "group", "speakerText", "bodyText", "continueMark");
            Require(Object.FindAnyObjectByType<TutorialPlayerVisual>(), "slashPrefab", "ringPrefab", "sparkPrefab");
            Require(Object.FindAnyObjectByType<LockOnMarkerUI>(), "data", "viewCamera", "marker", "executionMarker");
            foreach (var feedback in Object.FindObjectsByType<TeachingActorFeedback>(FindObjectsSortMode.None))
            {
                Require(feedback, "sparkPrefab", "impactAnchor");
                Require(feedback.GetComponent<FeedbackEmitter>(), "data", "audioManager", "impulse");
            }
            if (Object.FindObjectsByType<TeachingActorFeedback>(FindObjectsSortMode.None).Length != 2) throw new InvalidOperationException("教學角色回饋不完整。");
            var data = AssetDatabase.LoadAssetAtPath<TutorialData>(DataPath);
            if (data.steps.Length != 6 || data.grantedBones != 1 || data.initialBones != 0 || data.endingPause != 1f)
                throw new InvalidOperationException("教學步數／骨頭／停頓不符。");
            if (data.steps[2].mode != SaplingMode.DodgeAttack || data.steps[3].mode != SaplingMode.Attack)
                throw new InvalidOperationException("教學紅白模式不符。");
            var combat = AssetDatabase.LoadAssetAtPath<CombatFeedbackData>(FeedbackPath);
            foreach (var cue in new[] { combat.telegraphBlockable, combat.telegraphUnblockable, combat.swing, combat.heavySwing,
                combat.hit, combat.hitDeflected, combat.executionHit, combat.hurt, combat.block, combat.parry, combat.evade, combat.roll, combat.stunned, combat.defeated })
                if (cue.clips == null || cue.clips.Length == 0 || cue.clips.Any(c => c == null)) throw new InvalidOperationException("回饋音效有空引用。");
            var spawner = Object.FindAnyObjectByType<PlayerSpawner>();
            var receivers = new SerializedObject(spawner).FindProperty("receivers");
            foreach (var expected in new MonoBehaviour[] { director, Object.FindAnyObjectByType<TutorialPlayerVisual>(), Object.FindAnyObjectByType<PlayerHudUI>(), Object.FindAnyObjectByType<LockOnMarkerUI>() })
            {
                bool found = false;
                for (int i = 0; i < receivers.arraySize; i++) found |= receivers.GetArrayElementAtIndex(i).objectReferenceValue == expected;
                if (!found) throw new InvalidOperationException("缺少玩家接收者：" + expected.GetType().Name);
            }
            return "T27 Prologue PASS: MissingScript=0; 6 steps; red/white modes; dialogue/HUD/spawner refs; 2 actor emitters + VFX; 14 SFX cues OK";
        }
        /// <summary>非 Play 的事件管道與閃避邊界檢查；不生成玩家、不讀存檔。</summary>
        public static string ValidateRules()
        {
            var evaded = new HitReport(null, null, default, new DamageOutcome(DamageResult.Evaded));
            var hit = new HitReport(null, null, default, new DamageOutcome(DamageResult.Hit));
            var rollOutside = new SaplingAttackReport(null, 1, true, false, default, false, false, default);
            var rolledClear = new SaplingAttackReport(null, 2, true, false, default, false, true, default);
            var rolledIFrame = new SaplingAttackReport(null, 3, true, true, evaded, false, true, default);
            var rolledButHit = new SaplingAttackReport(null, 4, true, true, hit, false, true, default);
            var cancelled = new SaplingAttackReport(null, 5, true, false, default, true, true, default);
            if (TutorialDirector.IsSuccessfulDodge(rollOutside, false) || !TutorialDirector.IsSuccessfulDodge(rolledClear, false) ||
                !TutorialDirector.IsSuccessfulDodge(rolledIFrame, false) || TutorialDirector.IsSuccessfulDodge(rolledButHit, false) ||
                TutorialDirector.IsSuccessfulDodge(cancelled, false) || TutorialDirector.IsSuccessfulDodge(rolledIFrame, true))
                throw new InvalidOperationException("閃避結算邊界失敗。");
            var root = new GameObject("T27_DialogueValidation") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                var session = root.AddComponent<DialogueSession>();
                int finished = 0, request = 0, index = -1;
                session.OnViewChanged += view => { request = view.id; index = view.index; };
                session.OnFinished += (id, completed) => { if (completed) finished++; };
                session.Begin("測試", new[] { "第一句", "第二句" }, false);
                session.Cancel(request);
                if (finished != 0 || index != 0) throw new InvalidOperationException("必要對話被取消。");
                session.Advance(request - 1);
                if (index != 0) throw new InvalidOperationException("舊回報推進新對話。");
                session.Advance(request);
                if (index != 1) throw new InvalidOperationException("漏掉第二句。");
                session.PublishState();
                session.Advance(request);
                session.Advance(request);
                if (finished != 1) throw new InvalidOperationException("對話重複完成。");
                session.Begin("神祕小花", new[] { "招呼" }, false, false);
                session.Advance(request);
                if (finished != 1 || index != 0) throw new InvalidOperationException("被動招呼消耗了互動鍵。");
                session.Begin("神祕小花", new[] { "正式劇情" }, false);
                session.Advance(request);
                if (finished != 2) throw new InvalidOperationException("被動招呼後正式對話無法推進。");
            }
            finally { Object.DestroyImmediate(root); }
            return "T27 rules PASS: 6 dodge cases; mandatory dialogue/stale id/replay/exactly-once completion";
        }
        static void Require(Object target, params string[] fields)
        {
            if (target == null) throw new InvalidOperationException("缺少 T27 元件。");
            var so = new SerializedObject(target);
            foreach (string field in fields)
                if (so.FindProperty(field)?.objectReferenceValue == null) throw new InvalidOperationException(target.name + ": 缺少 " + field);
        }
    }
}
