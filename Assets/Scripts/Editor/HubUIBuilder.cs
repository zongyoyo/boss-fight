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
    /// 下方的互動提示（InteractionPromptUI）、右邊的傳送門面板（HubStationUI：標題、挑戰清單的樣板列、Boss 說明、返回提示、確認框；T24 起只剩傳送門）、
    /// 村長的對話框（DialogueUI，T24：畫面下方，說話者、內文、繼續記號、接任務的選項）與頭上的「!」「?」（QuestMarkerUI，投影用 Main Camera）、
    /// 營火面板（CampfireUI，T21：「角色」「技能樹」頁籤、角色清單的樣板列；T23 起跟鐵匠鋪一樣大，技能樹頁有節點〔SkillNodeCellUI 樣板〕、
    /// 連線樣板、剩餘點數、重置與確認框、說明與「學習」、Debug 的「給技能點」）、
    /// 較大的鐵匠鋪面板（BlacksmithUI，T22：左邊裝備欄、右邊裝備格子〔EquipmentCellUI 樣板〕、下方說明與數值差異、右上角 Debug 的「解鎖全部裝備」）。
    /// 接上 HubManager、InteractionScanner、Campfire、Blacksmith、VillageChief、Portal、EventSystem、GameOptions 物件上的 InputHintDevice 與按鍵名稱。版面、顏色在建立時照 HubUIData 寫進場景，改了要重跑。
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
        // 鐵匠鋪（T22）：面板比較大，仍在畫面右邊
        static readonly Vector2 SmithPanelSize = new Vector2(980f, 760f);
        static readonly Vector2 SmithPanelPosition = new Vector2(-60f, 0f);
        static readonly Vector2 SlotCellSize = new Vector2(300f, 100f);
        static readonly Vector2 ItemCellSize = new Vector2(132f, 150f);
        // 營火（T23 起跟鐵匠鋪一樣大：技能樹頁要放節點、說明與按鈕）
        static readonly Vector2 CampfirePanelSize = SmithPanelSize;
        static readonly Vector2 CampfirePanelPosition = SmithPanelPosition;
        static readonly Vector2 SkillTopButtonSize = new Vector2(160f, 52f);
        static readonly Vector2 SkillDebugButtonSize = new Vector2(300f, 52f);
        static readonly Vector2 SkillAreaSize = new Vector2(440f, 423f);
        static readonly Vector2 SkillDetailSize = new Vector2(444f, 423f);
        const float SkillAreaTop = -72f;

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
            var hint = Label("Hint", panel.transform, data.closeHintKeyboard, 24, FontStyle.Normal, data.hintColor, TextAnchor.MiddleCenter);
            Place(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 29f), new Vector2(PanelSize.x - 80f, 30f));

            // 傳送門：挑戰清單（GameDatabase.bosses 一隻一列，樣板列複製）、選到的 Boss 的說明，與確認框
            var list = Rect("PortalList", panel.transform);
            Place(list, new Vector2(0.5f, 0.5f), Vector2.zero, PanelSize);
            var bossRows = Rect("Rows", list, typeof(VerticalLayoutGroup));
            Place(bossRows, new Vector2(0.5f, 1f), new Vector2(0f, -125f), new Vector2(ButtonSize.x, 250f));
            var bossLayout = bossRows.GetComponent<VerticalLayoutGroup>();
            bossLayout.spacing = data.bossRowSpacing;
            bossLayout.childAlignment = TextAnchor.UpperCenter;
            bossLayout.childControlWidth = bossLayout.childControlHeight = false;
            bossLayout.childForceExpandWidth = bossLayout.childForceExpandHeight = false;
            var bossTemplate = MenuButton("RowTemplate", bossRows, "", Vector2.zero, ButtonSize);
            bossTemplate.gameObject.SetActive(false);
            var description = Label("Description", list, "", 28, FontStyle.Normal, data.textColor, TextAnchor.UpperCenter);
            Place(description.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 75f), new Vector2(PanelSize.x - 100f, 110f));
            description.rectTransform.pivot = new Vector2(0.5f, 0f);
            description.horizontalOverflow = HorizontalWrapMode.Wrap;
            var confirm = Rect("Confirm", panel.transform);
            Place(confirm, new Vector2(0.5f, 0.5f), Vector2.zero, PanelSize);
            var message = Label("Message", confirm, "", 38, FontStyle.Normal, data.textColor, TextAnchor.MiddleCenter);
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
            so.FindProperty("portal").objectReferenceValue = Object.FindAnyObjectByType<Portal>();
            so.FindProperty("eventSystem").objectReferenceValue = eventSystem;
            so.FindProperty("hintDevice").objectReferenceValue = hintDevice;
            so.FindProperty("group").objectReferenceValue = group;
            so.FindProperty("titleText").objectReferenceValue = title;
            so.FindProperty("closeHint").objectReferenceValue = hint;
            so.FindProperty("portalList").objectReferenceValue = list.gameObject;
            so.FindProperty("rowTemplate").objectReferenceValue = bossTemplate;
            so.FindProperty("rowContainer").objectReferenceValue = bossRows;
            so.FindProperty("descriptionText").objectReferenceValue = description;
            so.FindProperty("confirmBox").objectReferenceValue = confirm.gameObject;
            so.FindProperty("confirmText").objectReferenceValue = message;
            so.FindProperty("confirmYesButton").objectReferenceValue = yes;
            so.FindProperty("confirmNoButton").objectReferenceValue = no;
            so.ApplyModifiedPropertiesWithoutUndo();

            var campfire = Object.FindAnyObjectByType<Campfire>();
            if (campfire != null) BuildCampfirePanel(root.transform, hub, campfire, eventSystem, hintDevice);
            else Debug.LogWarning("[HubUI] 場景裡沒有 Campfire，沒有建營火面板（Tools > Hub > Build Hub）。");
            var blacksmith = Object.FindAnyObjectByType<Blacksmith>();
            if (blacksmith != null) BuildBlacksmithPanel(root.transform, hub, blacksmith, eventSystem, hintDevice);
            else Debug.LogWarning("[HubUI] 場景裡沒有 Blacksmith，沒有建鐵匠鋪面板（Tools > Hub > Build Hub）。");
            var chief = Object.FindAnyObjectByType<VillageChief>();
            if (chief != null)
            {
                BuildQuestMarker(root.transform, hub, chief);
                BuildDialogue(root.transform, hub, chief, eventSystem);
            }
            else Debug.LogWarning("[HubUI] 場景裡沒有 VillageChief，沒有建對話框（Tools > Hub > Build Hub）。");

            EditorSceneManager.MarkSceneDirty(root.scene);
            Debug.Log($"[HubUI] 休息區介面已建立：{DataPath}" + (hintDevice == null ? "；場景裡沒有 GameOptions，按鍵提示一律顯示鍵鼠" : ""));
            return root;
        }

        // 村長頭上的「!」「?」（T24）：全螢幕的範圍裡一個字，位置每幀由 QuestMarkerUI 投影
        static void BuildQuestMarker(Transform root, HubManager hub, VillageChief chief)
        {
            var area = Rect("QuestMarker", root);
            Stretch(area);
            var label = Label("Mark", area, data.newQuestMarker, data.markerFontSize, FontStyle.Bold, data.newQuestMarkerColor, TextAnchor.LowerCenter);
            Place(label.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(200f, 200f));
            label.rectTransform.pivot = new Vector2(0.5f, 0f); // 字的下緣在村長頭上
            var outline = label.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            outline.effectDistance = new Vector2(3f, -3f);
            var ui = area.gameObject.AddComponent<QuestMarkerUI>();
            var so = new SerializedObject(ui);
            so.FindProperty("data").objectReferenceValue = data;
            so.FindProperty("chief").objectReferenceValue = chief;
            so.FindProperty("hub").objectReferenceValue = hub;
            so.FindProperty("viewCamera").objectReferenceValue = Camera.main;
            so.FindProperty("area").objectReferenceValue = area;
            so.FindProperty("label").objectReferenceValue = label;
            so.ApplyModifiedPropertiesWithoutUndo();
            if (Camera.main == null) Debug.LogWarning("[HubUI] 場景裡沒有 Main Camera，村長頭上的標記不會顯示。");
        }

        // 村長的對話框（T24）：畫面下方；說話者、內文、繼續記號；選項那一步換成任務資訊與「接受／再想想」
        static void BuildDialogue(Transform root, HubManager hub, VillageChief chief, UnityEngine.EventSystems.EventSystem eventSystem)
        {
            var size = data.dialogueSize;
            var box = Img("Dialogue", root, roundSprite, data.dialogueBoxColor);
            box.type = Image.Type.Sliced;
            box.raycastTarget = true;
            Place(box.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, data.dialogueY), size);
            var group = box.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;

            var speaker = Label("Speaker", box.transform, "", 36, FontStyle.Bold, data.speakerColor, TextAnchor.MiddleLeft);
            Place(speaker.rectTransform, new Vector2(0f, 1f), new Vector2(50f, -16f), new Vector2(size.x - 100f, 50f));
            var body = Label("Body", box.transform, "", 36, FontStyle.Normal, data.textColor, TextAnchor.UpperLeft);
            Place(body.rectTransform, new Vector2(0f, 1f), new Vector2(50f, -74f), new Vector2(size.x - 140f, size.y - 100f));
            body.horizontalOverflow = HorizontalWrapMode.Wrap;
            body.lineSpacing = 1.15f;
            var mark = Label("Continue", box.transform, data.dialogueContinueMark, 30, FontStyle.Normal, data.speakerColor, TextAnchor.MiddleCenter);
            Place(mark.rectTransform, new Vector2(1f, 0f), new Vector2(-50f, 34f), new Vector2(40f, 40f));

            var choice = Rect("Choice", box.transform);
            Stretch(choice);
            var quest = Label("Quest", choice, "", 32, FontStyle.Normal, data.textColor, TextAnchor.UpperLeft);
            Place(quest.rectTransform, new Vector2(0f, 1f), new Vector2(50f, -74f), new Vector2(size.x - 460f, size.y - 100f));
            quest.horizontalOverflow = HorizontalWrapMode.Wrap;
            quest.lineSpacing = 1.15f;
            var accept = MenuButton("Accept", choice, data.acceptQuestLabel, Vector2.zero, new Vector2(280f, 70f));
            Place(accept.GetComponent<RectTransform>(), new Vector2(1f, 0.5f), new Vector2(-190f, 5f), new Vector2(280f, 70f));
            var decline = MenuButton("Decline", choice, data.declineQuestLabel, Vector2.zero, new Vector2(280f, 70f));
            Place(decline.GetComponent<RectTransform>(), new Vector2(1f, 0.5f), new Vector2(-190f, -80f), new Vector2(280f, 70f));
            choice.gameObject.SetActive(false);

            var ui = box.gameObject.AddComponent<DialogueUI>();
            var so = new SerializedObject(ui);
            so.FindProperty("data").objectReferenceValue = data;
            so.FindProperty("hub").objectReferenceValue = hub;
            so.FindProperty("chief").objectReferenceValue = chief;
            so.FindProperty("eventSystem").objectReferenceValue = eventSystem;
            so.FindProperty("group").objectReferenceValue = group;
            so.FindProperty("speakerText").objectReferenceValue = speaker;
            so.FindProperty("bodyText").objectReferenceValue = body;
            so.FindProperty("continueMark").objectReferenceValue = mark;
            so.FindProperty("choiceGroup").objectReferenceValue = choice.gameObject;
            so.FindProperty("questText").objectReferenceValue = quest;
            so.FindProperty("acceptButton").objectReferenceValue = accept;
            so.FindProperty("declineButton").objectReferenceValue = decline;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // 營火面板（T21）：T23 起跟鐵匠鋪同樣大小與位置
        static void BuildCampfirePanel(Transform root, HubManager hub, Campfire campfire, UnityEngine.EventSystems.EventSystem eventSystem, InputHintDevice hintDevice)
        {
            var size = CampfirePanelSize;
            var panel = Img("CampfirePanel", root, roundSprite, data.panelColor);
            panel.type = Image.Type.Sliced;
            panel.raycastTarget = true;
            Place(panel.rectTransform, new Vector2(1f, 0.5f), CampfirePanelPosition, size);
            var group = panel.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
            var station = data.Get(HubStationKind.Campfire);
            var title = Label("Title", panel.transform, station != null ? station.title : "", 56, FontStyle.Bold, data.titleColor, TextAnchor.MiddleCenter);
            Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -25f), new Vector2(size.x - 80f, 80f));
            var charactersTab = MenuButton("Tab_Characters", panel.transform, data.campfireCharactersTab, Vector2.zero, TabSize);
            Place(charactersTab.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(-120f, -125f), TabSize);
            var skillTreeTab = MenuButton("Tab_SkillTree", panel.transform, data.campfireSkillTreeTab, Vector2.zero, TabSize);
            Place(skillTreeTab.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(120f, -125f), TabSize);
            var hint = Label("Hint", panel.transform, data.closeHintKeyboard, 24, FontStyle.Normal, data.hintColor, TextAnchor.MiddleCenter);
            Place(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 29f), new Vector2(size.x - 80f, 30f));
            var pageSize = new Vector2(size.x - 80f, size.y - 265f);

            // 角色頁：一列一個按鈕，由上往下排
            var charactersPage = Rect("CharactersPage", panel.transform);
            Place(charactersPage, new Vector2(0.5f, 1f), new Vector2(0f, -195f), pageSize);
            var rows = Rect("Rows", charactersPage, typeof(VerticalLayoutGroup));
            Stretch(rows);
            var layout = rows.GetComponent<VerticalLayoutGroup>();
            layout.spacing = data.characterRowSpacing;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = layout.childControlHeight = false;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            var template = MenuButton("RowTemplate", rows, "", Vector2.zero, RowSize);
            template.gameObject.SetActive(false);

            // 技能樹頁（T23）：上方一排（重置、Debug、剩餘點數），左邊節點，右邊說明；確認框蓋在整頁上面
            var skillTreePage = Rect("SkillTreePage", panel.transform);
            Place(skillTreePage, new Vector2(0.5f, 1f), new Vector2(0f, -195f), pageSize);
            var tree = Rect("Tree", skillTreePage, typeof(CanvasGroup));
            Stretch(tree);
            var reset = MenuButton("Reset", tree, data.resetSkillsLabel, Vector2.zero, SkillTopButtonSize);
            Place(reset.GetComponent<RectTransform>(), new Vector2(0f, 1f), Vector2.zero, SkillTopButtonSize);
            reset.GetComponentInChildren<Text>().fontSize = 28;
            var debug = MenuButton("Debug_GivePoints", tree, string.Format(data.debugGiveSkillPointsFormat, 10), Vector2.zero, SkillDebugButtonSize);
            Place(debug.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(SkillTopButtonSize.x + 16f, 0f), SkillDebugButtonSize);
            debug.GetComponentInChildren<Text>().fontSize = 22;
            debug.gameObject.SetActive(false);
            var points = Label("Points", tree, string.Format(data.skillPointsFormat, 0), 30, FontStyle.Bold, data.titleColor, TextAnchor.MiddleRight);
            Place(points.rectTransform, new Vector2(1f, 1f), Vector2.zero, new Vector2(360f, SkillTopButtonSize.y));

            var area = Rect("Area", tree);
            Place(area, new Vector2(0f, 1f), new Vector2(0f, SkillAreaTop), SkillAreaSize);
            var lineContainer = Rect("Lines", area);
            Stretch(lineContainer);
            var line = Img("LineTemplate", lineContainer, null, data.skillLineColor);
            CenterOnTopLeft(line.rectTransform, new Vector2(10f, data.skillLineWidth));
            line.gameObject.SetActive(false);
            var nodeContainer = Rect("Nodes", area);
            Stretch(nodeContainer);
            var nodeTemplate = SkillNodeCell("NodeTemplate", nodeContainer);
            var noTree = Label("NoTree", area, "", 30, FontStyle.Normal, data.hintColor, TextAnchor.MiddleCenter);
            Stretch(noTree.rectTransform);
            noTree.gameObject.SetActive(false);

            var detail = Img("Detail", tree, roundSprite, new Color(0f, 0f, 0f, 0.3f));
            detail.type = Image.Type.Sliced;
            Place(detail.rectTransform, new Vector2(1f, 1f), new Vector2(0f, SkillAreaTop), SkillDetailSize);
            float textWidth = SkillDetailSize.x - 48f;
            var detailName = Label("Name", detail.transform, "", 30, FontStyle.Bold, data.titleColor, TextAnchor.UpperLeft);
            Place(detailName.rectTransform, new Vector2(0f, 1f), new Vector2(24f, -16f), new Vector2(textWidth, 40f));
            var detailStatus = Label("Status", detail.transform, "", 24, FontStyle.Normal, data.textColor, TextAnchor.UpperLeft);
            Place(detailStatus.rectTransform, new Vector2(0f, 1f), new Vector2(24f, -62f), new Vector2(textWidth, 32f));
            detailStatus.horizontalOverflow = HorizontalWrapMode.Wrap;
            var detailBody = Label("Body", detail.transform, "", 24, FontStyle.Normal, data.textColor, TextAnchor.UpperLeft);
            Place(detailBody.rectTransform, new Vector2(0f, 1f), new Vector2(24f, -104f), new Vector2(textWidth, 90f));
            detailBody.horizontalOverflow = HorizontalWrapMode.Wrap;
            var detailDiff = Label("Diff", detail.transform, "", 24, FontStyle.Normal, data.textColor, TextAnchor.UpperLeft);
            Place(detailDiff.rectTransform, new Vector2(0f, 1f), new Vector2(24f, -204f), new Vector2(textWidth, 110f));
            detailDiff.horizontalOverflow = HorizontalWrapMode.Wrap;
            var learnSize = new Vector2(220f, 64f);
            var learn = MenuButton("Learn", detail.transform, data.learnButtonLabel, Vector2.zero, learnSize);
            Place(learn.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0f, 18f), learnSize);
            learn.gameObject.SetActive(false);

            var confirmColor = data.panelColor;
            confirmColor.a = 1f; // 不透明：蓋住底下的節點
            var confirm = Img("ResetConfirm", skillTreePage, roundSprite, confirmColor);
            confirm.type = Image.Type.Sliced;
            confirm.raycastTarget = true; // 擋住底下的節點
            Stretch(confirm.rectTransform);
            var confirmText = Label("Message", confirm.transform, "", 36, FontStyle.Normal, data.textColor, TextAnchor.MiddleCenter);
            Place(confirmText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(pageSize.x - 80f, 140f));
            confirmText.horizontalOverflow = HorizontalWrapMode.Wrap;
            var yes = MenuButton("Yes", confirm.transform, data.resetSkillsYes, new Vector2(-130f, -90f), new Vector2(220f, 76f));
            var no = MenuButton("No", confirm.transform, data.resetSkillsNo, new Vector2(130f, -90f), new Vector2(220f, 76f));
            confirm.gameObject.SetActive(false);
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
            so.FindProperty("treeGroup").objectReferenceValue = tree.GetComponent<CanvasGroup>();
            so.FindProperty("pointsText").objectReferenceValue = points;
            so.FindProperty("noTreeText").objectReferenceValue = noTree;
            so.FindProperty("nodeTemplate").objectReferenceValue = nodeTemplate;
            so.FindProperty("nodeContainer").objectReferenceValue = nodeContainer;
            so.FindProperty("lineTemplate").objectReferenceValue = line;
            so.FindProperty("lineContainer").objectReferenceValue = lineContainer;
            so.FindProperty("detailName").objectReferenceValue = detailName;
            so.FindProperty("detailStatus").objectReferenceValue = detailStatus;
            so.FindProperty("detailBody").objectReferenceValue = detailBody;
            so.FindProperty("detailDiff").objectReferenceValue = detailDiff;
            so.FindProperty("learnButton").objectReferenceValue = learn;
            so.FindProperty("resetButton").objectReferenceValue = reset;
            so.FindProperty("resetConfirm").objectReferenceValue = confirm.gameObject;
            so.FindProperty("resetConfirmText").objectReferenceValue = confirmText;
            so.FindProperty("resetYesButton").objectReferenceValue = yes;
            so.FindProperty("resetNoButton").objectReferenceValue = no;
            so.FindProperty("debugPointsButton").objectReferenceValue = debug;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // 技能樹的一個節點（停用的樣板）：底色、可學的外框、不能學時變暗的 CanvasGroup、左邊的圖示（有才顯示）、名稱與花費
        static SkillNodeCellUI SkillNodeCell(string name, Transform parent)
        {
            var image = Img(name, parent, roundSprite, Color.white);
            image.type = Image.Type.Sliced;
            image.raycastTarget = true;
            CenterOnTopLeft(image.rectTransform, data.skillNodeSize);
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.normalColor = data.buttonColor;
            colors.highlightedColor = colors.selectedColor = data.buttonHighlightColor;
            colors.pressedColor = data.buttonHighlightColor * 0.8f;
            button.colors = colors;
            var frame = image.gameObject.AddComponent<Outline>();
            frame.effectDistance = new Vector2(4f, 4f);
            frame.effectColor = data.skillAvailableFrameColor;
            frame.enabled = false;
            var canvasGroup = image.gameObject.AddComponent<CanvasGroup>();
            var icon = Img("Icon", image.transform, null, Color.white);
            icon.preserveAspect = true;
            Place(icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(10f, 0f), new Vector2(56f, 56f));
            icon.enabled = false;
            var label = Label("Label", image.transform, "", 26, FontStyle.Normal, data.textColor, TextAnchor.MiddleCenter);
            Stretch(label.rectTransform);
            label.rectTransform.offsetMin = new Vector2(6f, 0f);
            label.rectTransform.offsetMax = new Vector2(-6f, 0f);
            label.supportRichText = true;

            var cell = image.gameObject.AddComponent<SkillNodeCellUI>();
            var so = new SerializedObject(cell);
            so.FindProperty("button").objectReferenceValue = button;
            so.FindProperty("icon").objectReferenceValue = icon;
            so.FindProperty("label").objectReferenceValue = label;
            so.FindProperty("frame").objectReferenceValue = frame;
            so.FindProperty("group").objectReferenceValue = canvasGroup;
            so.ApplyModifiedPropertiesWithoutUndo();
            image.gameObject.SetActive(false);
            return cell;
        }

        // 錨點在父物件左上、中心當支點（CampfireUI 用 anchoredPosition 擺節點與連線的中心）
        static void CenterOnTopLeft(RectTransform rt, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = Vector2.zero;
        }

        // 鐵匠鋪面板（T22）：左邊欄位、右邊裝備格子、下方說明與差異
        static void BuildBlacksmithPanel(Transform root, HubManager hub, Blacksmith blacksmith, UnityEngine.EventSystems.EventSystem eventSystem, InputHintDevice hintDevice)
        {
            var panel = Img("BlacksmithPanel", root, roundSprite, data.panelColor);
            panel.type = Image.Type.Sliced;
            panel.raycastTarget = true;
            Place(panel.rectTransform, new Vector2(1f, 0.5f), SmithPanelPosition, SmithPanelSize);
            var group = panel.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
            var station = data.Get(HubStationKind.Blacksmith);
            var title = Label("Title", panel.transform, station != null ? station.title : "", 56, FontStyle.Bold, data.titleColor, TextAnchor.MiddleCenter);
            Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -25f), new Vector2(SmithPanelSize.x - 80f, 80f));
            var hint = Label("Hint", panel.transform, data.closeHintKeyboard, 24, FontStyle.Normal, data.hintColor, TextAnchor.MiddleCenter);
            Place(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 29f), new Vector2(SmithPanelSize.x - 80f, 30f));

            // Debug（開發者模式）：右上角
            var debugSize = new Vector2(300f, 48f);
            var debug = MenuButton("Debug_UnlockAll", panel.transform, data.debugUnlockAllLabel, Vector2.zero, debugSize);
            Place(debug.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(-24f, -24f), debugSize);
            debug.GetComponentInChildren<Text>().fontSize = 22;
            debug.gameObject.SetActive(false);

            // 左：目前角色的欄位
            var characterLabel = Label("Character", panel.transform, "", 28, FontStyle.Bold, data.titleColor, TextAnchor.MiddleLeft);
            Place(characterLabel.rectTransform, new Vector2(0f, 1f), new Vector2(40f, -115f), new Vector2(SlotCellSize.x, 40f));
            var slots = Rect("Slots", panel.transform, typeof(VerticalLayoutGroup));
            Place(slots, new Vector2(0f, 1f), new Vector2(40f, -165f), new Vector2(SlotCellSize.x, 2f * SlotCellSize.y + 12f));
            var slotLayout = slots.GetComponent<VerticalLayoutGroup>();
            slotLayout.spacing = 12f;
            slotLayout.childAlignment = TextAnchor.UpperLeft;
            slotLayout.childControlWidth = slotLayout.childControlHeight = false;
            slotLayout.childForceExpandWidth = slotLayout.childForceExpandHeight = false;
            var slotTemplate = Cell("SlotTemplate", slots, SlotCellSize, false);

            // 右：所有裝備（一列 4 格）
            var items = Rect("Items", panel.transform, typeof(GridLayoutGroup));
            Place(items, new Vector2(0f, 1f), new Vector2(370f, -115f), new Vector2(4f * ItemCellSize.x + 3f * 14f, 2f * ItemCellSize.y + 14f));
            var grid = items.GetComponent<GridLayoutGroup>();
            grid.cellSize = ItemCellSize;
            grid.spacing = new Vector2(14f, 14f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 4;
            var itemTemplate = Cell("ItemTemplate", items, ItemCellSize, true);

            // 下：說明與數值差異
            var detail = Img("Detail", panel.transform, roundSprite, new Color(0f, 0f, 0f, 0.3f));
            detail.type = Image.Type.Sliced;
            Place(detail.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 64f), new Vector2(SmithPanelSize.x - 80f, 250f));
            float textWidth = SmithPanelSize.x - 128f;
            var detailName = Label("Name", detail.transform, "", 30, FontStyle.Bold, data.titleColor, TextAnchor.UpperLeft);
            Place(detailName.rectTransform, new Vector2(0f, 1f), new Vector2(24f, -14f), new Vector2(textWidth, 40f));
            var detailBody = Label("Body", detail.transform, "", 24, FontStyle.Normal, data.textColor, TextAnchor.UpperLeft);
            Place(detailBody.rectTransform, new Vector2(0f, 1f), new Vector2(24f, -58f), new Vector2(textWidth, 60f));
            detailBody.horizontalOverflow = HorizontalWrapMode.Wrap;
            var detailDiff = Label("Diff", detail.transform, "", 24, FontStyle.Normal, data.textColor, TextAnchor.UpperLeft);
            Place(detailDiff.rectTransform, new Vector2(0f, 1f), new Vector2(24f, -118f), new Vector2(textWidth, 120f));
            detailDiff.horizontalOverflow = HorizontalWrapMode.Wrap;
            detailDiff.supportRichText = true;

            var ui = panel.gameObject.AddComponent<BlacksmithUI>();
            var so = new SerializedObject(ui);
            so.FindProperty("data").objectReferenceValue = data;
            so.FindProperty("hub").objectReferenceValue = hub;
            so.FindProperty("blacksmith").objectReferenceValue = blacksmith;
            so.FindProperty("eventSystem").objectReferenceValue = eventSystem;
            so.FindProperty("hintDevice").objectReferenceValue = hintDevice;
            so.FindProperty("group").objectReferenceValue = group;
            so.FindProperty("closeHint").objectReferenceValue = hint;
            so.FindProperty("characterLabel").objectReferenceValue = characterLabel;
            so.FindProperty("slotTemplate").objectReferenceValue = slotTemplate;
            so.FindProperty("slotContainer").objectReferenceValue = slots;
            so.FindProperty("itemTemplate").objectReferenceValue = itemTemplate;
            so.FindProperty("itemContainer").objectReferenceValue = items;
            so.FindProperty("detailName").objectReferenceValue = detailName;
            so.FindProperty("detailBody").objectReferenceValue = detailBody;
            so.FindProperty("detailDiff").objectReferenceValue = detailDiff;
            so.FindProperty("debugUnlockButton").objectReferenceValue = debug;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // 鐵匠鋪的一格（停用的樣板）：item = 右邊的裝備（圖示在上、名稱在下、「新」與「裝備中」），否則左邊的欄位（圖示在左、兩行字）
        static EquipmentCellUI Cell(string name, Transform parent, Vector2 size, bool item)
        {
            var image = Img(name, parent, roundSprite, Color.white);
            image.type = Image.Type.Sliced;
            image.raycastTarget = true;
            Place(image.rectTransform, new Vector2(0f, 1f), Vector2.zero, size);
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.normalColor = data.buttonColor;
            colors.highlightedColor = colors.selectedColor = data.buttonHighlightColor;
            colors.pressedColor = data.buttonHighlightColor * 0.8f;
            button.colors = colors;
            var frame = image.gameObject.AddComponent<Outline>();
            frame.effectDistance = new Vector2(4f, 4f);
            frame.effectColor = data.equippedColor;
            frame.enabled = false;

            var icon = Img("Icon", image.transform, null, Color.white);
            icon.preserveAspect = true;
            Text label;
            GameObject badge = null, equipped = null;
            if (item)
            {
                Place(icon.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -12f), new Vector2(88f, 88f));
                label = Label("Label", image.transform, "", 22, FontStyle.Normal, data.textColor, TextAnchor.MiddleCenter);
                Place(label.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 8f), new Vector2(size.x - 4f, 36f));
                var newBadge = Img("New", image.transform, roundSprite, data.newBadgeColor);
                newBadge.type = Image.Type.Sliced;
                Place(newBadge.rectTransform, new Vector2(1f, 1f), new Vector2(-4f, -4f), new Vector2(44f, 30f));
                var newText = Label("Text", newBadge.transform, data.newBadgeText, 20, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
                Stretch(newText.rectTransform);
                badge = newBadge.gameObject;
                var tag = Label("Equipped", image.transform, data.equippedTagText, 18, FontStyle.Bold, data.equippedColor, TextAnchor.MiddleLeft);
                Place(tag.rectTransform, new Vector2(0f, 1f), new Vector2(6f, -4f), new Vector2(80f, 26f));
                tag.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.8f);
                equipped = tag.gameObject;
                badge.SetActive(false);
                equipped.SetActive(false);
            }
            else
            {
                Place(icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(14f, 0f), new Vector2(72f, 72f));
                label = Label("Label", image.transform, "", 26, FontStyle.Normal, data.textColor, TextAnchor.MiddleLeft);
                Place(label.rectTransform, new Vector2(0f, 0.5f), new Vector2(100f, 0f), new Vector2(size.x - 110f, size.y - 10f));
            }

            var cell = image.gameObject.AddComponent<EquipmentCellUI>();
            var so = new SerializedObject(cell);
            so.FindProperty("button").objectReferenceValue = button;
            so.FindProperty("icon").objectReferenceValue = icon;
            so.FindProperty("label").objectReferenceValue = label;
            so.FindProperty("newBadge").objectReferenceValue = badge;
            so.FindProperty("equippedTag").objectReferenceValue = equipped;
            so.FindProperty("frame").objectReferenceValue = frame;
            so.ApplyModifiedPropertiesWithoutUndo();
            image.gameObject.SetActive(false);
            return cell;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
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
