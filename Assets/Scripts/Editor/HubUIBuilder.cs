using System.Collections.Generic;
using BossFight.Core;
using BossFight.Hub;
using BossFight.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace BossFight.EditorTools
{
    /// <summary>
    /// Tools > UI > Build Hub UI：休息區的介面（T20），HubBuilder 結束前會呼叫。
    /// 資產 ScriptableObjects/Hub/HubUIData.asset（沒有才建）；HubUI Canvas（每次重建，sortingOrder 5，在 ESC 選單與黑幕下面）：
    /// 下方的互動提示（InteractionPromptUI）、右邊的站點面板（HubStationUI：標題、空內容、返回提示；傳送門的挑戰清單與確認框）、
    /// 同一個位置的營火面板（CampfireUI，T21：「角色」「技能樹」頁籤、角色清單的樣板列、技能樹的「（準備中）」）。
    /// 接上 HubManager、InteractionScanner、Campfire、EventSystem、GameOptions 物件上的 InputHintDevice 與按鍵名稱。版面、顏色在建立時照 HubUIData 寫進場景，改了要重跑。
    /// </summary>
    public static class HubUIBuilder
    {
        const string DataPath = HubBuilder.DataDir + "/HubUIData.asset";
        const string InputNamesPath = "Assets/ScriptableObjects/UI/InputNameData.asset";
        const string CanvasName = "HubUI";

        // 版面（Canvas 單位，參考解析度 1920×1080）
        static readonly Vector2 PanelSize = new Vector2(760f, 560f);
        static readonly Vector2 PanelPosition = new Vector2(-80f, 0f);
        static readonly Vector2 ButtonSize = new Vector2(440f, 76f);
        static readonly Vector2 TabSize = new Vector2(220f, 64f);
        static readonly Vector2 RowSize = new Vector2(440f, 70f);

        static Font font;
        static Sprite roundSprite;
        static HubUIData data;

        [MenuItem("Tools/UI/Build Hub UI")]
        public static void BuildFromMenu()
        {
            var hub = Object.FindAnyObjectByType<HubManager>();
            var scanner = Object.FindAnyObjectByType<InteractionScanner>();
            if (hub == null || scanner == null)
            {
                Debug.LogError("[HubUI] 需要場景裡的 HubManager 與 InteractionScanner（Tools > Hub > Build Hub）。");
                return;
            }
            var go = Build(hub, scanner);
            if (go != null) Selection.activeGameObject = go;
        }

        public static GameObject Build(HubManager hub, InteractionScanner scanner)
        {
            data = AssetDatabase.LoadAssetAtPath<HubUIData>(DataPath);
            if (data == null)
            {
                DaoDunDogBuilder.EnsureFolder(HubBuilder.DataDir);
                data = ScriptableObject.CreateInstance<HubUIData>();
                AssetDatabase.CreateAsset(data, DataPath);
            }
            var inputNames = AssetDatabase.LoadAssetAtPath<InputNameData>(InputNamesPath);
            var options = Object.FindAnyObjectByType<GameOptions>();
            var hintDevice = options != null ? DaoDunDogBuilder.GetOrAdd<InputHintDevice>(options.gameObject) : null;
            var eventSystem = BattleUIBuilder.EnsureEventSystem();
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); // 內建字型會借用系統字型顯示中文
            roundSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

            var old = GameObject.Find(CanvasName);
            if (old != null) Undo.DestroyObjectImmediate(old);
            var root = new GameObject(CanvasName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Undo.RegisterCreatedObjectUndo(root, "Build Hub UI");
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5; // 在 ESC 選單（200）與黑幕（1000）下面
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            // ---------- 互動提示 ----------
            var prompt = Label("InteractionPrompt", root.transform, string.Format(data.promptFormat, "E", "對話"), data.promptFontSize, FontStyle.Bold,
                               data.promptColor, TextAnchor.MiddleCenter);
            Place(prompt.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, data.promptY), new Vector2(1200f, 60f));
            var shadow = prompt.gameObject.AddComponent<Outline>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.8f);
            shadow.effectDistance = new Vector2(2f, -2f);
            var promptUi = prompt.gameObject.AddComponent<InteractionPromptUI>();
            var promptSo = new SerializedObject(promptUi);
            promptSo.FindProperty("data").objectReferenceValue = data;
            promptSo.FindProperty("scanner").objectReferenceValue = scanner;
            promptSo.FindProperty("hub").objectReferenceValue = hub;
            promptSo.FindProperty("inputNames").objectReferenceValue = inputNames;
            promptSo.FindProperty("hintDevice").objectReferenceValue = hintDevice;
            promptSo.FindProperty("label").objectReferenceValue = prompt;
            promptSo.ApplyModifiedPropertiesWithoutUndo();

            // ---------- 站點面板 ----------
            var panel = Img("StationPanel", root.transform, roundSprite, data.panelColor);
            panel.type = Image.Type.Sliced;
            panel.raycastTarget = true;
            Place(panel.rectTransform, new Vector2(1f, 0.5f), PanelPosition, PanelSize); // 畫面右邊：左半留給站點鏡頭
            var group = panel.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
            var title = Label("Title", panel.transform, "", 56, FontStyle.Bold, data.titleColor, TextAnchor.MiddleCenter);
            Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -25f), new Vector2(PanelSize.x - 80f, 80f));
            var empty = Label("Empty", panel.transform, data.emptyStationText, 34, FontStyle.Normal, data.hintColor, TextAnchor.MiddleCenter);
            Place(empty.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -10f), new Vector2(PanelSize.x - 80f, 60f));
            var hint = Label("Hint", panel.transform, data.closeHintKeyboard, 24, FontStyle.Normal, data.hintColor, TextAnchor.MiddleCenter);
            Place(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 29f), new Vector2(PanelSize.x - 80f, 30f));

            // 傳送門：挑戰清單（一列）與確認框
            var list = Rect("PortalList", panel.transform);
            Place(list, new Vector2(0.5f, 0.5f), Vector2.zero, PanelSize);
            var boss = MenuButton("Boss", list, data.bossLabel, new Vector2(0f, 10f), ButtonSize);
            var confirm = Rect("Confirm", panel.transform);
            Place(confirm, new Vector2(0.5f, 0.5f), Vector2.zero, PanelSize);
            var message = Label("Message", confirm, string.Format(data.departConfirmFormat, data.bossLabel), 38, FontStyle.Normal, data.textColor, TextAnchor.MiddleCenter);
            Place(message.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(PanelSize.x - 80f, 120f));
            message.horizontalOverflow = HorizontalWrapMode.Wrap;
            var yes = MenuButton("Yes", confirm, data.departYes, new Vector2(-130f, -90f), new Vector2(220f, 76f));
            var no = MenuButton("No", confirm, data.departNo, new Vector2(130f, -90f), new Vector2(220f, 76f));
            list.gameObject.SetActive(false);
            confirm.gameObject.SetActive(false);

            var stationUi = panel.gameObject.AddComponent<HubStationUI>();
            var so = new SerializedObject(stationUi);
            so.FindProperty("data").objectReferenceValue = data;
            so.FindProperty("hub").objectReferenceValue = hub;
            so.FindProperty("eventSystem").objectReferenceValue = eventSystem;
            so.FindProperty("hintDevice").objectReferenceValue = hintDevice;
            so.FindProperty("group").objectReferenceValue = group;
            so.FindProperty("titleText").objectReferenceValue = title;
            so.FindProperty("emptyText").objectReferenceValue = empty;
            so.FindProperty("closeHint").objectReferenceValue = hint;
            so.FindProperty("portalList").objectReferenceValue = list.gameObject;
            so.FindProperty("bossButton").objectReferenceValue = boss;
            so.FindProperty("confirmBox").objectReferenceValue = confirm.gameObject;
            so.FindProperty("confirmText").objectReferenceValue = message;
            so.FindProperty("confirmYesButton").objectReferenceValue = yes;
            so.FindProperty("confirmNoButton").objectReferenceValue = no;
            so.ApplyModifiedPropertiesWithoutUndo();

            var campfire = Object.FindAnyObjectByType<Campfire>();
            if (campfire != null) BuildCampfirePanel(root.transform, hub, campfire, eventSystem, hintDevice);
            else Debug.LogWarning("[HubUI] 場景裡沒有 Campfire，沒有建營火面板（Tools > Hub > Build Hub）。");

            EditorSceneManager.MarkSceneDirty(root.scene);
            Debug.Log($"[HubUI] 休息區介面已建立：{DataPath}" + (hintDevice == null ? "；場景裡沒有 GameOptions，按鍵提示一律顯示鍵鼠" : ""));
            return root;
        }

        // 營火面板（T21）：跟站點面板同一個位置與大小
        static void BuildCampfirePanel(Transform root, HubManager hub, Campfire campfire, UnityEngine.EventSystems.EventSystem eventSystem, InputHintDevice hintDevice)
        {
            var panel = Img("CampfirePanel", root, roundSprite, data.panelColor);
            panel.type = Image.Type.Sliced;
            panel.raycastTarget = true;
            Place(panel.rectTransform, new Vector2(1f, 0.5f), PanelPosition, PanelSize);
            var group = panel.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
            var station = data.Get(HubStationKind.Campfire);
            var title = Label("Title", panel.transform, station != null ? station.title : "", 56, FontStyle.Bold, data.titleColor, TextAnchor.MiddleCenter);
            Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -25f), new Vector2(PanelSize.x - 80f, 80f));
            var charactersTab = MenuButton("Tab_Characters", panel.transform, data.campfireCharactersTab, Vector2.zero, TabSize);
            Place(charactersTab.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(-120f, -125f), TabSize);
            var skillTreeTab = MenuButton("Tab_SkillTree", panel.transform, data.campfireSkillTreeTab, Vector2.zero, TabSize);
            Place(skillTreeTab.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(120f, -125f), TabSize);
            var hint = Label("Hint", panel.transform, data.closeHintKeyboard, 24, FontStyle.Normal, data.hintColor, TextAnchor.MiddleCenter);
            Place(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 29f), new Vector2(PanelSize.x - 80f, 30f));

            // 角色頁：一列一個按鈕，由上往下排
            var charactersPage = Rect("CharactersPage", panel.transform);
            Place(charactersPage, new Vector2(0.5f, 1f), new Vector2(0f, -195f), new Vector2(PanelSize.x - 80f, PanelSize.y - 265f));
            var rows = Rect("Rows", charactersPage, typeof(VerticalLayoutGroup));
            rows.anchorMin = Vector2.zero;
            rows.anchorMax = Vector2.one;
            rows.offsetMin = rows.offsetMax = Vector2.zero;
            var layout = rows.GetComponent<VerticalLayoutGroup>();
            layout.spacing = data.characterRowSpacing;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = layout.childControlHeight = false;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            var template = MenuButton("RowTemplate", rows, "", Vector2.zero, RowSize);
            template.gameObject.SetActive(false);

            // 技能樹頁（T23）
            var skillTreePage = Rect("SkillTreePage", panel.transform);
            Place(skillTreePage, new Vector2(0.5f, 1f), new Vector2(0f, -195f), new Vector2(PanelSize.x - 80f, PanelSize.y - 265f));
            var empty = Label("Empty", skillTreePage, data.emptyStationText, 34, FontStyle.Normal, data.hintColor, TextAnchor.MiddleCenter);
            Place(empty.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(PanelSize.x - 80f, 60f));
            skillTreePage.gameObject.SetActive(false);

            var ui = panel.gameObject.AddComponent<CampfireUI>();
            var so = new SerializedObject(ui);
            so.FindProperty("data").objectReferenceValue = data;
            so.FindProperty("hub").objectReferenceValue = hub;
            so.FindProperty("campfire").objectReferenceValue = campfire;
            so.FindProperty("eventSystem").objectReferenceValue = eventSystem;
            so.FindProperty("hintDevice").objectReferenceValue = hintDevice;
            so.FindProperty("group").objectReferenceValue = group;
            so.FindProperty("closeHint").objectReferenceValue = hint;
            so.FindProperty("charactersTab").objectReferenceValue = charactersTab;
            so.FindProperty("skillTreeTab").objectReferenceValue = skillTreeTab;
            so.FindProperty("charactersPage").objectReferenceValue = charactersPage.gameObject;
            so.FindProperty("skillTreePage").objectReferenceValue = skillTreePage.gameObject;
            so.FindProperty("rowTemplate").objectReferenceValue = template;
            so.FindProperty("rowContainer").objectReferenceValue = rows;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------- 工具 ----------

        static Button MenuButton(string name, Transform parent, string label, Vector2 pos, Vector2 size)
        {
            var image = Img(name, parent, roundSprite, Color.white);
            image.type = Image.Type.Sliced;
            image.raycastTarget = true;
            Place(image.rectTransform, new Vector2(0.5f, 0.5f), pos, size);
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.normalColor = data.buttonColor;
            colors.highlightedColor = colors.selectedColor = data.buttonHighlightColor;
            colors.pressedColor = data.buttonHighlightColor * 0.8f;
            button.colors = colors;
            var text = Label("Label", image.transform, label, 34, FontStyle.Normal, data.textColor, TextAnchor.MiddleCenter);
            var rt = text.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return button;
        }

        static RectTransform Rect(string name, Transform parent, params System.Type[] components)
        {
            var types = new List<System.Type> { typeof(RectTransform) };
            types.AddRange(components);
            var go = new GameObject(name, types.ToArray());
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        static Image Img(string name, Transform parent, Sprite sprite, Color color)
        {
            var rt = Rect(name, parent, typeof(CanvasRenderer), typeof(Image));
            var image = rt.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        static Text Label(string name, Transform parent, string text, int size, FontStyle style, Color color, TextAnchor align)
        {
            var rt = Rect(name, parent, typeof(CanvasRenderer), typeof(Text));
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
            return t;
        }

        static void Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
        }
    }
}
