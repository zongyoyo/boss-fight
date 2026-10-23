using System.Collections.Generic;
using BossFight.Battle;
using BossFight.Core;
using BossFight.Cutscene;
using BossFight.DebugTools;
using BossFight.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BossFight.EditorTools
{
    /// <summary>
    /// Tools > UI > Build Pause Menu：建立 ESC 選單（T13）。
    /// 1. 數值資產（沒有才建）：ScriptableObjects/UI/PauseMenuData.asset（文字、顏色、改鍵清單；試聽音效空著時填 Sfx_Combat_Block）、
    ///    ScriptableObjects/UI/InputNameData.asset（按鍵顯示名稱）、ScriptableObjects/Options/GameOptionsData.asset（設定的預設值）。
    /// 2. GameOptions 物件（沒有才建）：GameOptions（設定存檔）與 GamePause（暫停）。
    /// 3. PauseMenu Canvas（每次重建，排在所有 UI 上面）：暗幕與 7 頁（主選單、設定、按鍵設定、鍵鼠、控制器、音量、確認）。
    ///    頁面上的固定文字、按鈕顏色、改鍵頁的大小依 PauseMenuData 建立，改了要重跑這裡。
    /// 4. 接線（Wire，Tools > UI > Wire Pause Menu 也可以單獨跑）：AudioManager（GameOptions、選單試聽的 Ui AudioSource、演出 Timeline 音軌的 AudioSource）、
    ///    BattleManager 的 GamePause、DebugTools 的 DeveloperMode（開發者模式開關 BossDebugTools、BalanceDebugPanel、PlayerDebugOverlay）、
    ///    Boss 血條與跳過提示的按鍵名稱。
    /// BattleUIBuilder（Build Intro 最後會跑）結束時會呼叫 Build；TransitionCutsceneBuilder、BossBuilder 結束時呼叫 Wire。
    /// </summary>
    public static class PauseMenuBuilder
    {
        const string DataPath = "Assets/ScriptableObjects/UI/PauseMenuData.asset";
        const string InputNamesPath = "Assets/ScriptableObjects/UI/InputNameData.asset";
        const string OptionsDataPath = "Assets/ScriptableObjects/Options/GameOptionsData.asset";
        const string PreviewClipPath = "Assets/Audio/Placeholder/Sfx_Combat_Block.wav";
        const string MenuName = "PauseMenu";
        const string OptionsName = "GameOptions";
        const string UiSourceName = "Ui";

        // 版面（Canvas 單位，參考解析度 1920×1080）
        static readonly Vector2 ButtonSize = new Vector2(440f, 76f);
        const float ButtonGap = 92f;
        const float RowHeight = 44f, RowGap = 6f;
        const float RowsTop = 165f;   // 改鍵頁：視窗頂端到第一列
        const float RowsBottom = 240f; // 改鍵頁：最後一列到視窗底部（固定操作、提示、按鈕、返回提示）

        static Font font;
        static Sprite roundSprite, knobSprite;
        static PauseMenuData data;

        [MenuItem("Tools/UI/Build Pause Menu")]
        public static void BuildFromMenu()
        {
            var go = Build();
            if (go != null) Selection.activeGameObject = go;
        }

        [MenuItem("Tools/UI/Wire Pause Menu")]
        public static void WireFromMenu() => Wire();

        public static GameObject Build()
        {
            var battle = Object.FindAnyObjectByType<BattleManager>();
            if (battle == null)
            {
                Debug.LogError("[PauseMenu] 需要場景裡的 BattleManager（Tools > Cutscene > Build Intro）。");
                return null;
            }

            Undo.SetCurrentGroupName("Build Pause Menu");
            int undoGroup = Undo.GetCurrentGroup();

            data = GetOrCreate<PauseMenuData>(DataPath);
            if (data.sfxPreview == null)
            {
                data.sfxPreview = AssetDatabase.LoadAssetAtPath<AudioClip>(PreviewClipPath);
                EditorUtility.SetDirty(data);
            }
            var inputNames = GetOrCreate<InputNameData>(InputNamesPath);
            var optionsData = GetOrCreate<GameOptionsData>(OptionsDataPath);
            var options = EnsureOptions(optionsData, out var pause);
            var eventSystem = BattleUIBuilder.EnsureEventSystem();

            var old = GameObject.Find(MenuName);
            if (old != null) Undo.DestroyObjectImmediate(old);

            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); // 內建字型會借用系統字型顯示中文
            roundSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            knobSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");

            var root = new GameObject(MenuName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
            Undo.RegisterCreatedObjectUndo(root, "Build Pause Menu");
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200; // 蓋過戰鬥 UI（5）、開場字卡（10）、白屏（100）
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            var group = root.GetComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;

            var dim = Img("Dim", root.transform, null, data.dimColor);
            Stretch(dim.rectTransform);
            dim.raycastTarget = true; // 擋住後面（勝敗畫面的按鈕）

            var ui = root.AddComponent<PauseMenuUI>();
            var so = new SerializedObject(ui);
            so.FindProperty("data").objectReferenceValue = data;
            so.FindProperty("options").objectReferenceValue = options;
            so.FindProperty("pause").objectReferenceValue = pause;
            so.FindProperty("battle").objectReferenceValue = battle;
            so.FindProperty("audioManager").objectReferenceValue = Object.FindAnyObjectByType<AudioManager>();
            so.FindProperty("eventSystem").objectReferenceValue = eventSystem;
            so.FindProperty("group").objectReferenceValue = group;

            // ---------- 主選單 ----------
            var main = Window("Main", root.transform, new Vector2(620f, 600f), data.pauseTitle);
            var mainButtons = ButtonColumn(main, 130f, data.resumeLabel, data.settingsLabel, data.retryLabel, data.quitLabel);
            so.FindProperty("mainPage").objectReferenceValue = main.gameObject;
            Set(so, "resumeButton", mainButtons[0]);
            Set(so, "settingsButton", mainButtons[1]);
            Set(so, "retryButton", mainButtons[2]);
            Set(so, "quitButton", mainButtons[3]);

            // ---------- 設定 ----------
            var settings = Window("Settings", root.transform, new Vector2(620f, 600f), data.settingsLabel);
            var settingButtons = ButtonColumn(settings, 130f, data.controlsLabel, data.volumeLabel,
                                              string.Format(data.developerModeFormat, data.onText), data.backLabel);
            so.FindProperty("settingsPage").objectReferenceValue = settings.gameObject;
            Set(so, "controlsButton", settingButtons[0]);
            Set(so, "volumeButton", settingButtons[1]);
            Set(so, "developerButton", settingButtons[2]);
            so.FindProperty("developerLabel").objectReferenceValue = settingButtons[2].GetComponentInChildren<Text>();
            Set(so, "settingsBackButton", settingButtons[3]);

            // ---------- 按鍵設定 ----------
            var controls = Window("Controls", root.transform, new Vector2(620f, 510f), data.controlsLabel);
            var controlButtons = ButtonColumn(controls, 100f, data.keyboardLabel, data.gamepadLabel, data.backLabel);
            so.FindProperty("controlsPage").objectReferenceValue = controls.gameObject;
            Set(so, "keyboardButton", controlButtons[0]);
            Set(so, "gamepadButton", controlButtons[1]);
            Set(so, "controlsBackButton", controlButtons[2]);
            so.FindProperty("keyboardPage").objectReferenceValue = BuildBindingPage("Keyboard", root.transform, false, inputNames, options, eventSystem);
            so.FindProperty("gamepadPage").objectReferenceValue = BuildBindingPage("Gamepad", root.transform, true, inputNames, options, eventSystem);

            // ---------- 音量設定 ----------
            var volume = Window("Volume", root.transform, new Vector2(900f, 560f), data.volumeLabel);
            so.FindProperty("volumePage").objectReferenceValue = volume.gameObject;
            VolumeRow(so, volume, 120f, "Master", data.masterLabel, "masterSlider", "masterValue");
            VolumeRow(so, volume, 30f, "Music", data.musicLabel, "musicSlider", "musicValue");
            VolumeRow(so, volume, -60f, "Sfx", data.sfxLabel, "sfxSlider", "sfxValue");
            Set(so, "volumeBackButton", MenuButton("Back", volume, data.backLabel, new Vector2(0f, -170f), ButtonSize));

            // ---------- 確認 ----------
            var confirm = Window("Confirm", root.transform, new Vector2(720f, 440f), "");
            var message = Label("Message", confirm, data.retryConfirm, 38, FontStyle.Normal, data.textColor, TextAnchor.MiddleCenter);
            Place(message.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(640f, 180f));
            message.lineSpacing = 1.2f;
            so.FindProperty("confirmPage").objectReferenceValue = confirm.gameObject;
            so.FindProperty("confirmText").objectReferenceValue = message;
            Set(so, "confirmYesButton", MenuButton("Yes", confirm, data.confirmYes, new Vector2(-130f, -100f), new Vector2(220f, 76f)));
            Set(so, "confirmNoButton", MenuButton("No", confirm, data.confirmNo, new Vector2(130f, -100f), new Vector2(220f, 76f)));

            so.ApplyModifiedPropertiesWithoutUndo();

            // 編輯時只看得到暗幕的話很難調，頁面先關掉；PauseMenuUI 執行時自己切換
            foreach (Transform page in root.transform)
            {
                if (page != dim.transform) page.gameObject.SetActive(false);
            }

            Wire();

            Undo.CollapseUndoOperations(undoGroup);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(root.scene);
            Debug.Log($"[PauseMenu] ESC 選單已建立：{DataPath}、{InputNamesPath}、{OptionsDataPath}");
            return root;
        }

        /// <summary>接好 ESC 選單以外會用到設定、暫停與按鍵名稱的元件；重建 AudioManager、演出、Boss、BattleManager 之後要再跑一次。</summary>
        public static void Wire()
        {
            var options = Object.FindAnyObjectByType<GameOptions>();
            var pause = Object.FindAnyObjectByType<GamePause>();
            if (options == null || pause == null)
            {
                Debug.Log("[PauseMenu] 場景裡還沒有 GameOptions（Tools > UI > Build Pause Menu 會建立），先略過設定與暫停的接線。");
                return;
            }
            var inputNames = AssetDatabase.LoadAssetAtPath<InputNameData>(InputNamesPath);
            var battle = Object.FindAnyObjectByType<BattleManager>();
            var audio = Object.FindAnyObjectByType<AudioManager>();

            // 音量：選單試聽的 AudioSource、演出 Timeline 音軌的 AudioSource
            if (audio != null)
            {
                var sources = new List<AudioSource>();
                foreach (var director in Object.FindObjectsByType<CutsceneDirector>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    sources.AddRange(director.GetComponentsInChildren<AudioSource>(true));
                var so = new SerializedObject(audio);
                so.FindProperty("options").objectReferenceValue = options;
                so.FindProperty("uiSource").objectReferenceValue = EnsureUiSource(audio);
                IntroCutsceneBuilder.SetArray(so.FindProperty("cutsceneSources"), sources);
                so.ApplyModifiedProperties();
            }

            if (battle != null) DaoDunDogBuilder.SetReference(battle, "pause", pause);

            // 開發者模式
            var bossTools = Object.FindAnyObjectByType<BossDebugTools>();
            if (bossTools != null)
            {
                var developer = bossTools.GetComponent<DeveloperMode>();
                if (developer == null) developer = Undo.AddComponent<DeveloperMode>(bossTools.gameObject);
                var tools = new List<Behaviour> { bossTools };
                var panel = Object.FindAnyObjectByType<BalanceDebugPanel>();
                if (panel != null) tools.Add(panel);
                var overlay = Object.FindAnyObjectByType<PlayerDebugOverlay>();
                if (overlay != null) tools.Add(overlay);
                var so = new SerializedObject(developer);
                so.FindProperty("options").objectReferenceValue = options;
                so.FindProperty("pause").objectReferenceValue = pause;
                so.FindProperty("bossTools").objectReferenceValue = bossTools;
                IntroCutsceneBuilder.SetArray(so.FindProperty("tools"), tools);
                so.ApplyModifiedProperties();
            }

            // 按鍵提示跟著改鍵
            foreach (var hud in Object.FindObjectsByType<BossHudUI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                DaoDunDogBuilder.SetReference(hud, "inputNames", inputNames);
            foreach (var skip in Object.FindObjectsByType<SkipPromptUI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var so = new SerializedObject(skip);
                so.FindProperty("inputNames").objectReferenceValue = inputNames;
                var label = so.FindProperty("label");
                if (label.objectReferenceValue == null)
                {
                    var child = skip.transform.Find("Label");
                    label.objectReferenceValue = child != null ? child.GetComponent<Text>() : null;
                }
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            // 選單本身引用的 BattleManager、AudioManager 也可能被重建
            var menu = Object.FindAnyObjectByType<PauseMenuUI>(FindObjectsInactive.Include);
            if (menu != null)
            {
                var so = new SerializedObject(menu);
                so.FindProperty("battle").objectReferenceValue = battle;
                so.FindProperty("audioManager").objectReferenceValue = audio;
                so.FindProperty("options").objectReferenceValue = options;
                so.FindProperty("pause").objectReferenceValue = pause;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            EditorSceneManager.MarkSceneDirty(options.gameObject.scene);
            string missing = (audio == null ? " AudioManager" : "") + (battle == null ? " BattleManager" : "") +
                             (bossTools == null ? " DebugTools" : "") + (menu == null ? " PauseMenu" : "");
            Debug.Log("[PauseMenu] 設定、暫停與按鍵名稱已接上" + (missing.Length > 0 ? $"；缺少：{missing}" : ""));
        }

        // ---------- 場景物件 ----------

        static GameOptions EnsureOptions(GameOptionsData optionsData, out GamePause pause)
        {
            var options = Object.FindAnyObjectByType<GameOptions>();
            GameObject go;
            if (options != null)
            {
                go = options.gameObject;
            }
            else
            {
                go = new GameObject(OptionsName);
                Undo.RegisterCreatedObjectUndo(go, "Build Pause Menu");
                options = go.AddComponent<GameOptions>();
            }
            DaoDunDogBuilder.SetReference(options, "data", optionsData);
            pause = Object.FindAnyObjectByType<GamePause>();
            if (pause == null) pause = Undo.AddComponent<GamePause>(go);
            return options;
        }

        // 選單試聽用：AudioManager 底下的 Ui（2D，執行時設成不受暫停影響）
        static AudioSource EnsureUiSource(AudioManager audio)
        {
            var child = audio.transform.Find(UiSourceName);
            if (child == null)
            {
                child = DaoDunDogBuilder.Node(audio.transform, UiSourceName, Vector3.zero);
                Undo.RegisterCreatedObjectUndo(child.gameObject, "Build Pause Menu");
            }
            var src = DaoDunDogBuilder.GetOrAdd<AudioSource>(child.gameObject);
            src.playOnAwake = false;
            src.loop = false;
            src.spatialBlend = 0f;
            return src;
        }

        // ---------- 改鍵頁 ----------

        static KeyBindingPageUI BuildBindingPage(string name, Transform parent, bool gamepad, InputNameData inputNames, GameOptions options, EventSystem eventSystem)
        {
            int count = 0;
            foreach (var entry in data.bindings)
            {
                if (gamepad ? entry.gamepad : entry.keyboard) count++;
            }
            int slots = gamepad ? 1 : 2;
            float labelWidth = gamepad ? 380f : 360f;
            float slotWidth = gamepad ? 360f : 300f;
            const float gap = 20f;
            float contentWidth = labelWidth + (gap + slotWidth) * slots;
            float rowsHeight = count * (RowHeight + RowGap);
            var size = new Vector2(contentWidth + 100f, RowsTop + rowsHeight + RowsBottom);

            var page = Window(name, parent, size, gamepad ? data.gamepadLabel : data.keyboardLabel);
            var group = page.gameObject.AddComponent<CanvasGroup>();

            // 表頭
            var header = Rect("Header", page);
            PlaceTop(header, -135f, new Vector2(contentWidth, 36f));
            HeaderCell(header, data.actionHeader, 0f, labelWidth, TextAnchor.MiddleLeft);
            for (int s = 0; s < slots; s++)
            {
                string text = gamepad ? data.gamepadHeader : s == 0 ? data.primaryHeader : data.secondaryHeader;
                HeaderCell(header, text, labelWidth + gap + s * (gap + slotWidth), slotWidth, TextAnchor.MiddleCenter);
            }

            // 各列：範本一列，執行時依 PauseMenuData.bindings 複製
            var rows = Rect("Rows", page);
            PlaceTop(rows, -RowsTop, new Vector2(contentWidth, rowsHeight));
            rows.pivot = new Vector2(0.5f, 1f);
            rows.anchoredPosition = new Vector2(0f, -RowsTop);
            var layout = rows.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = RowGap;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = layout.childControlHeight = false;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;

            var template = Rect("RowTemplate", rows);
            template.sizeDelta = new Vector2(contentWidth, RowHeight);
            var rowLabel = Label("Label", template, "動作", 30, FontStyle.Normal, data.textColor, TextAnchor.MiddleLeft);
            PlaceLeft(rowLabel.rectTransform, 0f, labelWidth, RowHeight);
            var slotButtons = new List<Button>();
            var slotTexts = new List<Text>();
            for (int s = 0; s < slots; s++)
            {
                var button = MenuButton($"Slot{s}", template, "—", Vector2.zero, new Vector2(slotWidth, RowHeight), 28);
                PlaceLeft((RectTransform)button.transform, labelWidth + gap + s * (gap + slotWidth), slotWidth, RowHeight);
                slotButtons.Add(button);
                slotTexts.Add(button.GetComponentInChildren<Text>());
            }
            var row = template.gameObject.AddComponent<KeyBindingRowUI>();
            var rso = new SerializedObject(row);
            rso.FindProperty("label").objectReferenceValue = rowLabel;
            IntroCutsceneBuilder.SetArray(rso.FindProperty("slots"), slotButtons);
            IntroCutsceneBuilder.SetArray(rso.FindProperty("slotTexts"), slotTexts);
            rso.ApplyModifiedPropertiesWithoutUndo();

            // 固定的操作、提示、按鈕
            float below = -RowsTop - rowsHeight;
            var note = Label("FixedNote", page, gamepad ? data.gamepadFixedNote : data.keyboardFixedNote, 26, FontStyle.Normal, data.hintColor, TextAnchor.MiddleCenter);
            PlaceTop(note.rectTransform, below - 28f, new Vector2(contentWidth, 32f));
            var status = Label("Status", page, gamepad ? data.gamepadHint : data.keyboardHint, 28, FontStyle.Normal, data.hintColor, TextAnchor.MiddleCenter);
            PlaceTop(status.rectTransform, below - 70f, new Vector2(contentWidth, 36f));
            var reset = MenuButton("Reset", page, data.resetLabel, Vector2.zero, new Vector2(260f, 68f));
            PlaceTop((RectTransform)reset.transform, below - 140f, new Vector2(260f, 68f));
            ((RectTransform)reset.transform).anchoredPosition += new Vector2(-150f, 0f);
            var back = MenuButton("Back", page, data.backLabel, Vector2.zero, new Vector2(260f, 68f));
            PlaceTop((RectTransform)back.transform, below - 140f, new Vector2(260f, 68f));
            ((RectTransform)back.transform).anchoredPosition += new Vector2(150f, 0f);

            var ui = page.gameObject.AddComponent<KeyBindingPageUI>();
            var so = new SerializedObject(ui);
            so.FindProperty("data").objectReferenceValue = data;
            so.FindProperty("inputNames").objectReferenceValue = inputNames;
            so.FindProperty("options").objectReferenceValue = options;
            so.FindProperty("gamepad").boolValue = gamepad;
            so.FindProperty("rowTemplate").objectReferenceValue = row;
            so.FindProperty("statusText").objectReferenceValue = status;
            so.FindProperty("resetButton").objectReferenceValue = reset;
            so.FindProperty("backButton").objectReferenceValue = back;
            so.FindProperty("group").objectReferenceValue = group;
            so.FindProperty("eventSystem").objectReferenceValue = eventSystem;
            so.ApplyModifiedPropertiesWithoutUndo();
            return ui;
        }

        static void HeaderCell(RectTransform header, string text, float x, float width, TextAnchor align)
        {
            var t = Label(text, header, text, 26, FontStyle.Bold, data.hintColor, align);
            PlaceLeft(t.rectTransform, x, width, 36f);
        }

        // ---------- 音量頁 ----------

        static void VolumeRow(SerializedObject so, RectTransform page, float y, string name, string label, string sliderField, string valueField)
        {
            var text = Label(name + "Label", page, label, 34, FontStyle.Normal, data.textColor, TextAnchor.MiddleLeft);
            Place(text.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-280f, y), new Vector2(200f, 50f));
            var slider = MakeSlider(name + "Slider", page, new Vector2(40f, y), new Vector2(420f, 36f));
            var value = Label(name + "Value", page, string.Format(data.percentFormat, 100), 32, FontStyle.Normal, data.textColor, TextAnchor.MiddleRight);
            Place(value.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(330f, y), new Vector2(120f, 50f));
            so.FindProperty(sliderField).objectReferenceValue = slider;
            so.FindProperty(valueField).objectReferenceValue = value;
        }

        static Slider MakeSlider(string name, RectTransform parent, Vector2 pos, Vector2 size)
        {
            var bg = Img(name, parent, roundSprite, new Color(0f, 0f, 0f, 0.6f));
            bg.type = Image.Type.Sliced;
            bg.raycastTarget = true;
            Place(bg.rectTransform, new Vector2(0.5f, 0.5f), pos, size);

            var fillArea = Rect("FillArea", bg.transform);
            Stretch(fillArea);
            fillArea.offsetMin = new Vector2(6f, 6f);
            fillArea.offsetMax = new Vector2(-6f, -6f);
            var fill = Img("Fill", fillArea, roundSprite, data.sliderFillColor);
            fill.type = Image.Type.Sliced;
            fill.rectTransform.sizeDelta = Vector2.zero;

            var handleArea = Rect("HandleArea", bg.transform);
            Stretch(handleArea);
            handleArea.offsetMin = new Vector2(16f, 0f);
            handleArea.offsetMax = new Vector2(-16f, 0f);
            var handle = Img("Handle", handleArea, knobSprite, Color.white);
            handle.rectTransform.sizeDelta = new Vector2(32f, 16f);

            var slider = bg.gameObject.AddComponent<Slider>();
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 1f;
            var colors = slider.colors;
            colors.normalColor = new Color(0.85f, 0.85f, 0.85f);
            colors.highlightedColor = colors.selectedColor = data.titleColor;
            colors.pressedColor = data.buttonPressedColor;
            slider.colors = colors;
            return slider;
        }

        // ---------- 共用 ----------

        // 一頁：置中的面板與標題，下方是操作提示
        static RectTransform Window(string name, Transform parent, Vector2 size, string title)
        {
            var panel = Img(name, parent, roundSprite, data.panelColor);
            panel.type = Image.Type.Sliced;
            panel.raycastTarget = true;
            var rt = panel.rectTransform;
            Place(rt, new Vector2(0.5f, 0.5f), Vector2.zero, size);
            if (!string.IsNullOrEmpty(title))
            {
                var t = Label("Title", rt, title, 56, FontStyle.Bold, data.titleColor, TextAnchor.MiddleCenter);
                PlaceTop(t.rectTransform, -65f, new Vector2(size.x - 80f, 80f));
            }
            var hint = Label("Hint", rt, data.navigationHint, 24, FontStyle.Normal, data.hintColor, TextAnchor.MiddleCenter);
            hint.rectTransform.anchorMin = hint.rectTransform.anchorMax = hint.rectTransform.pivot = new Vector2(0.5f, 0f);
            hint.rectTransform.sizeDelta = new Vector2(size.x - 80f, 30f);
            hint.rectTransform.anchoredPosition = new Vector2(0f, 14f);
            return rt;
        }

        // 一排置中的按鈕，從 firstY 往下
        static Button[] ButtonColumn(RectTransform parent, float firstY, params string[] labels)
        {
            var buttons = new Button[labels.Length];
            for (int i = 0; i < labels.Length; i++)
                buttons[i] = MenuButton($"Button{i}", parent, labels[i], new Vector2(0f, firstY - i * ButtonGap), ButtonSize);
            return buttons;
        }

        static Button MenuButton(string name, Transform parent, string label, Vector2 pos, Vector2 size, int fontSize = 34)
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
            colors.pressedColor = data.buttonPressedColor;
            colors.disabledColor = new Color(data.buttonColor.r, data.buttonColor.g, data.buttonColor.b, data.buttonColor.a * 0.4f);
            button.colors = colors;
            var text = Label("Label", image.transform, label, fontSize, FontStyle.Normal, data.textColor, TextAnchor.MiddleCenter);
            Stretch(text.rectTransform);
            return button;
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

        static void Set(SerializedObject so, string field, Object value) => so.FindProperty(field).objectReferenceValue = value;

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

        // 貼著父物件頂端置中，y 是中心到頂端的距離（負的）
        static void PlaceTop(RectTransform rt, float y, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = new Vector2(0f, y);
        }

        // 在一列裡從左邊 x 起算、寬 width
        static void PlaceLeft(RectTransform rt, float x, float width, float height)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.sizeDelta = new Vector2(width, height);
            rt.anchoredPosition = new Vector2(x, 0f);
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }
}
