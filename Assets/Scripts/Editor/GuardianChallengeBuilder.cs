using BossFight.Battle;
using BossFight.Boss;
using BossFight.Core;
using BossFight.Cutscene;
using BossFight.Progression;
using BossFight.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace BossFight.EditorTools
{
    /// <summary>
    /// Tools > Prologue > Wire Guardian Challenge（T34）：序章場景裡讓休息區傳送門的大樹守衛重複挑戰能用的接線。Build Cutscenes 結束時會跑。
    /// 1. 出生點：Spawns 底下的 GuardianChallenge（id = 大樹守衛 BossDefinition.portalArrival），放在教學對話的站位（菊花為圓心、TutorialData.talkAngle／talkDistance），面向菊花與守衛；
    ///    加進 PlayerSpawner 的 spawnPoints（同 id 的取代）。
    /// 2. 守衛登場演出（GuardianEntrance 的 CutsceneDirector）接上 IntroCutsceneSettings（只用長按跳過的秒數）；
    /// 3. 跳過提示：ChallengeSkipPrompt Canvas（右下角，跟 OIIA 開場同樣的版面），SkipPromptUI 只聽登場演出。
    /// 重複挑戰的 ESC 選單按鈕由 PauseMenuBuilder（序章版多建重新挑戰、返回休息區）建。
    /// </summary>
    public static class GuardianChallengeBuilder
    {
        const string SettingsPath = "Assets/ScriptableObjects/Cutscene/IntroCutsceneSettings.asset";
        const string InputNamesPath = "Assets/ScriptableObjects/UI/InputNameData.asset";
        const string SpawnName = "GuardianChallenge";
        const string PromptName = "ChallengeSkipPrompt";

        [MenuItem("Tools/Prologue/Wire Guardian Challenge")]
        public static void WireFromMenu() => Wire();

        public static void Wire()
        {
            var flow = Object.FindAnyObjectByType<PrologueCutsceneFlow>();
            if (flow == null)
            {
                Debug.LogWarning("[GuardianChallenge] 場景裡沒有 PrologueCutsceneFlow（Tools > Prologue > Build Cutscenes），略過。");
                return;
            }
            var flowSo = new SerializedObject(flow);
            var outcome = flowSo.FindProperty("outcome").objectReferenceValue as PrologueOutcome;
            var entrance = flowSo.FindProperty("entrance").objectReferenceValue as CutsceneDirector;
            var tutorial = flowSo.FindProperty("tutorial").objectReferenceValue as TutorialDirector;
            var mentor = flowSo.FindProperty("mentor").objectReferenceValue as DaisyMentor;
            var definition = outcome != null ? new SerializedObject(outcome).FindProperty("definition").objectReferenceValue as BossDefinition : null;
            if (definition == null || string.IsNullOrEmpty(definition.portalArrival) || entrance == null || tutorial == null || mentor == null)
            {
                Debug.LogError("[GuardianChallenge] 需要 PrologueOutcome.definition（portalArrival 不能空）、登場演出、教學與菊花。");
                return;
            }

            // 1. 出生點：教學對話的站位
            var spawner = PlayerSpawnerBuilder.Find();
            if (spawner != null)
            {
                var data = tutorial.Data;
                var center = mentor.transform.position;
                var dir = Quaternion.Euler(0f, data.talkAngle, 0f) * Vector3.forward;
                var parent = FindSpawns(spawner);
                var point = parent.Find(SpawnName);
                if (point == null)
                {
                    point = new GameObject(SpawnName).transform;
                    point.SetParent(parent, false);
                }
                point.position = new Vector3(center.x + dir.x * data.talkDistance, spawner.transform.position.y, center.z + dir.z * data.talkDistance);
                point.rotation = Quaternion.LookRotation(-new Vector3(dir.x, 0f, dir.z), Vector3.up);
                SetSpawnPoint(spawner, definition.portalArrival, point);
            }
            else Debug.LogWarning("[GuardianChallenge] 場景裡沒有 PlayerSpawner，沒有放出生點。");

            // 2. 登場演出可以長按跳過（只用 skipHoldTime；重複挑戰第二次起才開放）
            var settings = AssetDatabase.LoadAssetAtPath<IntroCutsceneSettings>(SettingsPath);
            DaoDunDogBuilder.SetReference(entrance, "settings", settings);

            // 3. 跳過提示
            BuildSkipPrompt(entrance);

            EditorSceneManager.MarkSceneDirty(flow.gameObject.scene);
            Debug.Log($"[GuardianChallenge] 已接好：出生點 {definition.portalArrival}、登場演出可跳過（{(settings != null ? settings.skipHoldTime + " 秒" : "缺 IntroCutsceneSettings")}）、跳過提示");
        }

        static Transform FindSpawns(PlayerSpawner spawner)
        {
            var spawns = GameObject.Find("Spawns");
            return spawns != null ? spawns.transform : spawner.transform;
        }

        static void SetSpawnPoint(PlayerSpawner spawner, string id, Transform point)
        {
            var so = new SerializedObject(spawner);
            var list = so.FindProperty("spawnPoints");
            int index = list.arraySize;
            for (int i = 0; i < list.arraySize; i++)
            {
                if (list.GetArrayElementAtIndex(i).FindPropertyRelative("id").stringValue == id) { index = i; break; }
            }
            if (index == list.arraySize) list.arraySize++;
            var element = list.GetArrayElementAtIndex(index);
            element.FindPropertyRelative("id").stringValue = id;
            element.FindPropertyRelative("point").objectReferenceValue = point;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // 右下角「長按 Space 跳過」與進度條（版面同 IntroCutsceneBuilder 的開場）
        static void BuildSkipPrompt(CutsceneDirector entrance)
        {
            var old = GameObject.Find(PromptName);
            if (old != null) Object.DestroyImmediate(old);
            var root = new GameObject(PromptName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10; // 在 ESC 選單（200）與黑幕（1000）下面
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var skip = Node("SkipPrompt", root.transform, typeof(CanvasGroup));
            skip.anchorMin = skip.anchorMax = skip.pivot = new Vector2(1f, 0f);
            skip.sizeDelta = new Vector2(380f, 80f);
            skip.anchoredPosition = new Vector2(-48f, 40f);
            var group = skip.GetComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            var labelRt = Node("Label", skip, typeof(CanvasRenderer), typeof(Text), typeof(Outline));
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = new Vector2(0f, 20f);
            labelRt.offsetMax = Vector2.zero;
            var label = labelRt.GetComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.text = "長按 Space 跳過";
            label.fontSize = 30;
            label.color = Color.white;
            label.alignment = TextAnchor.LowerRight;
            label.raycastTarget = false;
            labelRt.GetComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.8f);
            var bar = Node("Bar", skip, typeof(CanvasRenderer), typeof(Image));
            bar.anchorMin = new Vector2(0f, 0f);
            bar.anchorMax = new Vector2(1f, 0f);
            bar.pivot = new Vector2(0.5f, 0f);
            bar.sizeDelta = new Vector2(0f, 8f);
            bar.anchoredPosition = Vector2.zero;
            var barImage = bar.GetComponent<Image>();
            barImage.color = new Color(0f, 0f, 0f, 0.55f);
            barImage.raycastTarget = false;
            var fillRt = Node("Fill", bar, typeof(CanvasRenderer), typeof(Image));
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = Vector2.one;
            fillRt.offsetMin = fillRt.offsetMax = Vector2.zero;
            var fill = fillRt.GetComponent<Image>();
            fill.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 0f;
            fill.color = new Color(1f, 0.93f, 0.78f);
            fill.raycastTarget = false;

            var ui = skip.gameObject.AddComponent<SkipPromptUI>();
            var so = new SerializedObject(ui);
            var sources = so.FindProperty("sources");
            sources.arraySize = 1;
            sources.GetArrayElementAtIndex(0).objectReferenceValue = entrance;
            so.FindProperty("group").objectReferenceValue = group;
            so.FindProperty("fill").objectReferenceValue = fill;
            so.FindProperty("label").objectReferenceValue = label;
            so.FindProperty("inputNames").objectReferenceValue = AssetDatabase.LoadAssetAtPath<InputNameData>(InputNamesPath);
            var options = Object.FindAnyObjectByType<GameOptions>();
            so.FindProperty("hintDevice").objectReferenceValue = options != null ? options.GetComponent<InputHintDevice>() : null;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static RectTransform Node(string name, Transform parent, params System.Type[] components)
        {
            var types = new System.Collections.Generic.List<System.Type> { typeof(RectTransform) };
            types.AddRange(components);
            var go = new GameObject(name, types.ToArray());
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }
    }
}
