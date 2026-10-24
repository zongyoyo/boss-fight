using System.Collections.Generic;
using BossFight.Battle;
using BossFight.Boss;
using BossFight.Cameras;
using BossFight.Core;
using BossFight.Cutscene;
using BossFight.DebugTools;
using BossFight.Player;
using BossFight.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace BossFight.EditorTools
{
    /// <summary>
    /// Tools > UI > Build Battle UI：在場景建立戰鬥 UI（T9）。
    /// 1. 數值資產（沒有才建）：ScriptableObjects/Battle/BattleSettings.asset（勝敗節奏）、ScriptableObjects/UI/BattleUIData.asset（刻度、動畫、顏色、文字）。
    /// 2. BattleUI Canvas（每次重建）：左下玩家 HUD（血條、耐力條、骨頭）、下方置中 Boss 名稱／血條（50%、20% 刻度）／失衡條、鎖定標記、勝敗畫面。
    ///    參考解析度 1920×1080；左上、右上留給 Debug 面板（PlayerDebugOverlay、BossDebugTools）。開場字卡與跳過提示是 IntroUI（IntroCutsceneBuilder），排在這個 Canvas 上面。
    /// 3. EventSystem（InputSystemUIInputModule，沒有才建）：勝敗畫面的按鈕用滑鼠、Enter、手把 A 都能按。
    /// 4. 接好 BattleManager 的 T9 欄位（settings、bossHealth、playerHealth、player、cameraController）與 BossDebugTools.battle，並重新接上 F12 平衡面板（BalancePanelBuilder）。
    /// 5. 重建 ESC 選單（PauseMenuBuilder，也會重接設定、暫停與按鍵名稱）。
    /// Tools > Cutscene > Build Intro 會重建 BattleManager，最後也會呼叫這裡。
    /// </summary>
    public static class BattleUIBuilder
    {
        const string SettingsPath = "Assets/ScriptableObjects/Battle/BattleSettings.asset";
        const string DataPath = "Assets/ScriptableObjects/UI/BattleUIData.asset";
        const string UiName = "BattleUI";
        const string EventSystemName = "EventSystem";
        const string RingSpritePath = "Assets/Models/UI/Ui_Ring.png";

        static Font font;
        static Sprite roundSprite, knobSprite;

        [MenuItem("Tools/UI/Build Battle UI")]
        public static void BuildFromMenu()
        {
            var go = Build();
            if (go != null) Selection.activeGameObject = go;
        }

        public static GameObject Build()
        {
            var battle = Object.FindAnyObjectByType<BattleManager>();
            var player = Object.FindAnyObjectByType<PlayerController>();
            var boss = Object.FindAnyObjectByType<BossController>();
            var cameraController = Object.FindAnyObjectByType<CameraController>();
            var mainCamera = Camera.main;
            if (battle == null || player == null || boss == null || cameraController == null || mainCamera == null)
            {
                Debug.LogError("[BattleUI] 需要場景裡的 BattleManager（Tools > Cutscene > Build Intro）、PlayerController、BossController、CameraController 與 Main Camera。");
                return null;
            }
            var playerHealth = player.GetComponent<PlayerHealth>();
            var stamina = player.GetComponent<PlayerStamina>();
            var bossHealth = boss.GetComponent<BossHealth>();
            var stagger = boss.GetComponent<BossStagger>();
            var intro = new SerializedObject(battle).FindProperty("intro").objectReferenceValue as CutsceneDirector;

            Undo.SetCurrentGroupName("Build Battle UI");
            int undoGroup = Undo.GetCurrentGroup();

            var settings = GetOrCreate<BattleSettings>(SettingsPath);
            var data = GetOrCreate<BattleUIData>(DataPath);

            var b = new SerializedObject(battle);
            b.FindProperty("settings").objectReferenceValue = settings;
            b.FindProperty("bossHealth").objectReferenceValue = bossHealth;
            b.FindProperty("playerHealth").objectReferenceValue = playerHealth;
            b.FindProperty("player").objectReferenceValue = player;
            b.FindProperty("cameraController").objectReferenceValue = cameraController;
            b.ApplyModifiedProperties();

            var tools = Object.FindAnyObjectByType<BossDebugTools>();
            if (tools != null)
            {
                var t = new SerializedObject(tools);
                t.FindProperty("battle").objectReferenceValue = battle;
                t.ApplyModifiedProperties();
            }

            var eventSystem = EnsureEventSystem();

            var old = GameObject.Find(UiName);
            if (old != null) Undo.DestroyObjectImmediate(old);

            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); // 內建字型會借用系統字型顯示中文
            roundSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            knobSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");

            var root = new GameObject(UiName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Undo.RegisterCreatedObjectUndo(root, "Build Battle UI");
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5; // IntroUI（字卡、跳過提示）是 10，蓋在上面
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            // ---------- HUD：玩家與鎖定標記 ----------
            var hud = Node("Hud", root.transform, typeof(CanvasGroup));
            IntroCutsceneBuilder.Stretch(hud);
            var marker = BuildLockMarker(hud, data, player, mainCamera);
            var execution = BuildExecutionMarker(hud, data, boss, mainCamera);
            DaoDunDogBuilder.SetReference(marker.parent.GetComponent<LockOnMarkerUI>(), "executionMarker", execution);
            BuildPlayerHud(hud, data, playerHealth, stamina);

            // ---------- Boss ----------
            var bossRoot = Node("Boss", root.transform, typeof(CanvasGroup));
            IntroCutsceneBuilder.Stretch(bossRoot);
            var bossPanel = BuildBossHud(bossRoot, data, bossHealth, stagger, intro);

            // ---------- 處決標記 ----------

        // 頭暈時 Boss 胸口的紅色圓環加中心點；外面一圈深色描邊，亮的背景上也看得到
        static ExecutionMarkerUI BuildExecutionMarker(RectTransform parent, BattleUIData data, BossController boss, Camera mainCamera)
        {
            var area = Node("Execution", parent);
            IntroCutsceneBuilder.Stretch(area);
            var marker = Node("Marker", area);
            marker.anchorMin = marker.anchorMax = marker.pivot = new Vector2(0.5f, 0.5f);
            marker.sizeDelta = new Vector2(72f, 72f);
            var ring = GetRingSprite();
            var outline = Img("Outline", marker, ring, new Color(0f, 0f, 0f, 0.6f));
            Place(outline.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(64f, 64f));
            var circle = Img("Ring", marker, ring, data.executionMarkerColor);
            Place(circle.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(56f, 56f));
            var dotOutline = Img("DotOutline", marker, knobSprite, new Color(0f, 0f, 0f, 0.6f));
            Place(dotOutline.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(18f, 18f));
            var dot = Img("Dot", marker, knobSprite, data.executionMarkerColor);
            Place(dot.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(12f, 12f));

            var lockOn = boss.GetComponent<LockOnTarget>();
            var ui = area.gameObject.AddComponent<ExecutionMarkerUI>();
            var so = new SerializedObject(ui);
            so.FindProperty("data").objectReferenceValue = data;
            so.FindProperty("target").objectReferenceValue = boss.GetComponent<ExecutionTarget>();
            so.FindProperty("aimPoint").objectReferenceValue = lockOn != null ? lockOn.AimPoint : boss.transform;
            so.FindProperty("viewCamera").objectReferenceValue = mainCamera;
            so.FindProperty("marker").objectReferenceValue = marker;
            IntroCutsceneBuilder.SetArray(so.FindProperty("tinted"), new List<Image> { circle, dot });
            so.ApplyModifiedPropertiesWithoutUndo();
            return ui;
        }

        // 圓環 sprite（白色、邊緣反鋸齒，UI 用 Image 的顏色上色）；只在不存在時產生
        static Sprite GetRingSprite()
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(RingSpritePath);
            if (sprite != null) return sprite;
            DaoDunDogBuilder.EnsureFolder("Assets/Models/UI");
            const int size = 128;
            const float outer = size * 0.5f - 1f, inner = outer - size * 0.12f;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size * 0.5f, size * 0.5f));
                    float a = Mathf.Clamp01(outer - d + 0.5f) * Mathf.Clamp01(d - inner + 0.5f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }
            tex.SetPixels32(pixels);
            System.IO.File.WriteAllBytes(RingSpritePath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(RingSpritePath);
            var importer = (TextureImporter)AssetImporter.GetAtPath(RingSpritePath);
            importer.textureType = TextureImporterType.Sprite;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(RingSpritePath);
        }

        // ---------- 勝敗畫面 ----------
            BuildResult(root.transform, data, battle, intro, eventSystem);

            var ui = root.AddComponent<BattleUI>();
            var so = new SerializedObject(ui);
            so.FindProperty("data").objectReferenceValue = data;
            so.FindProperty("battle").objectReferenceValue = battle;
            so.FindProperty("intro").objectReferenceValue = intro;
            so.FindProperty("hudGroup").objectReferenceValue = hud.GetComponent<CanvasGroup>();
            so.FindProperty("bossGroup").objectReferenceValue = bossRoot.GetComponent<CanvasGroup>();
            so.FindProperty("bossPanel").objectReferenceValue = bossPanel;
            so.ApplyModifiedPropertiesWithoutUndo();

            BalancePanelBuilder.Wire();
            PauseMenuBuilder.Build();

            Undo.CollapseUndoOperations(undoGroup);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(root.scene);
            Debug.Log($"[BattleUI] 戰鬥 UI 已建立：{SettingsPath}、{DataPath}，BattleManager{(tools != null ? "、BossDebugTools" : "")} 已接上；鎖定標記 {marker.name}");
            return root;
        }

        // ---------- 玩家 HUD（左下） ----------

        static void BuildPlayerHud(RectTransform parent, BattleUIData data, PlayerHealth health, PlayerStamina stamina)
        {
            var panel = Node("PlayerHud", parent);
            panel.anchorMin = panel.anchorMax = panel.pivot = Vector2.zero;
            panel.sizeDelta = new Vector2(420f, 130f);
            panel.anchoredPosition = new Vector2(48f, 44f);

            var staminaBar = Bar("StaminaBar", panel, data, new Vector2(0f, 0f), new Vector2(340f, 16f), false, data.staminaColor);
            var healthBar = Bar("HealthBar", panel, data, new Vector2(0f, 26f), new Vector2(420f, 30f), true, data.playerHealthColor);
            var healthText = Label("Value", healthBar.transform, "100 / 100", 22, FontStyle.Bold, Color.white, TextAnchor.MiddleRight);
            Stretch(healthText.rectTransform, new Vector2(0f, 0f), new Vector2(-10f, 0f));

            // 骨頭：啃骨頭鍵（執行時讀 Player/Heal 目前裝置的鍵）+ 每場次數的骨頭圖示（執行時依次數複製）
            var bones = Node("Bones", panel);
            bones.anchorMin = bones.anchorMax = bones.pivot = Vector2.zero;
            bones.sizeDelta = new Vector2(420f, 40f);
            bones.anchoredPosition = new Vector2(0f, 70f);
            var key = Img("Key", bones, roundSprite, new Color(0f, 0f, 0f, 0.6f));
            key.type = Image.Type.Sliced;
            Place(key.rectTransform, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(34f, 34f));
            var keyText = Label("R", key.transform, "R", 22, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            Stretch(keyText.rectTransform, Vector2.zero, Vector2.zero);
            var icons = Node("Icons", bones); // 只放骨頭：PlayerHudUI 重建時會清掉這裡除了範本以外的東西
            icons.anchorMin = new Vector2(0f, 0f);
            icons.anchorMax = new Vector2(1f, 1f);
            icons.offsetMin = new Vector2(48f, 0f);
            icons.offsetMax = Vector2.zero;
            var template = BuildBone(icons, data);
            Place(template, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(46f, 24f));
            // 下一根骨頭的累積進度：骨頭圖示下方的細條，長度約是 3 根骨頭排開的寬度
            var boneProgress = Bar("BoneProgress", bones, data, new Vector2(48f, -12f), new Vector2(158f, 10f), false, data.boneProgressColor);

            var hudUi = panel.gameObject.AddComponent<PlayerHudUI>();
            var so = new SerializedObject(hudUi);
            so.FindProperty("data").objectReferenceValue = data;
            so.FindProperty("health").objectReferenceValue = health;
            so.FindProperty("stamina").objectReferenceValue = stamina;
            so.FindProperty("healthBar").objectReferenceValue = healthBar;
            so.FindProperty("staminaBar").objectReferenceValue = staminaBar;
            so.FindProperty("boneTemplate").objectReferenceValue = template;
            so.FindProperty("healthText").objectReferenceValue = healthText;
            so.FindProperty("boneProgressBar").objectReferenceValue = boneProgress;
            so.FindProperty("healKeyText").objectReferenceValue = keyText;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // 佔位的骨頭圖示：中間一根棒子，兩端各兩顆圓
        static RectTransform BuildBone(RectTransform parent, BattleUIData data)
        {
            var bone = Node("BoneTemplate", parent);
            var shaft = Img("Shaft", bone, roundSprite, data.boneColor);
            shaft.type = Image.Type.Sliced;
            Place(shaft.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(30f, 9f));
            foreach (var (x, y) in new[] { (-16f, 5f), (-16f, -5f), (16f, 5f), (16f, -5f) })
            {
                var knob = Img("Knob", bone, knobSprite, data.boneColor);
                Place(knob.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(x, y), new Vector2(13f, 13f));
            }
            return bone;
        }

        // ---------- Boss（下方置中） ----------

        static RectTransform BuildBossHud(RectTransform parent, BattleUIData data, BossHealth health, BossStagger stagger, CutsceneDirector intro)
        {
            var panel = Node("BossHud", parent);
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 0f);
            panel.sizeDelta = new Vector2(860f, 110f);
            panel.anchoredPosition = new Vector2(0f, 44f);

            var staggerBar = Bar("StaggerBar", panel, data, new Vector2(0f, 0f), new Vector2(860f, 14f), false, data.staggerColor);
            ((RectTransform)staggerBar.transform).anchorMin = ((RectTransform)staggerBar.transform).anchorMax = new Vector2(0.5f, 0f);
            ((RectTransform)staggerBar.transform).pivot = new Vector2(0.5f, 0f);
            var stunned = Label("Stunned", staggerBar.transform, data.stunnedLabel, 26, FontStyle.Bold, data.stunnedColor, TextAnchor.LowerCenter);
            Place(stunned.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, -30f), new Vector2(860f, 30f));

            var healthBar = Bar("HealthBar", panel, data, new Vector2(0f, 22f), new Vector2(860f, 26f), true, data.bossHealthColor);
            ((RectTransform)healthBar.transform).anchorMin = ((RectTransform)healthBar.transform).anchorMax = new Vector2(0.5f, 0f);
            ((RectTransform)healthBar.transform).pivot = new Vector2(0.5f, 0f);
            var inner = (RectTransform)healthBar.transform.Find("Inner");
            var tick = Img("TickTemplate", inner, null, new Color(1f, 1f, 1f, 0.9f));
            tick.rectTransform.anchorMin = new Vector2(0.5f, 0f);
            tick.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            tick.rectTransform.sizeDelta = new Vector2(3f, 12f);
            tick.rectTransform.anchoredPosition = Vector2.zero;

            var nameText = Label("Name", panel, data.fallbackBossName, 34, FontStyle.Bold, new Color(1f, 0.93f, 0.85f), TextAnchor.LowerLeft);
            Place(nameText.rectTransform, Vector2.zero, new Vector2(4f, 54f), new Vector2(600f, 44f));

            var hudUi = panel.gameObject.AddComponent<BossHudUI>();
            var so = new SerializedObject(hudUi);
            so.FindProperty("data").objectReferenceValue = data;
            so.FindProperty("health").objectReferenceValue = health;
            so.FindProperty("stagger").objectReferenceValue = stagger;
            so.FindProperty("intro").objectReferenceValue = intro;
            so.FindProperty("nameText").objectReferenceValue = nameText;
            so.FindProperty("healthBar").objectReferenceValue = healthBar;
            so.FindProperty("tickTemplate").objectReferenceValue = tick.rectTransform;
            so.FindProperty("staggerBar").objectReferenceValue = staggerBar;
            so.FindProperty("stunnedText").objectReferenceValue = stunned;
            so.ApplyModifiedPropertiesWithoutUndo();
            return panel;
        }

        // ---------- 鎖定標記 ----------

        static RectTransform BuildLockMarker(RectTransform parent, BattleUIData data, PlayerController player, Camera mainCamera)
        {
            var area = Node("LockOn", parent);
            IntroCutsceneBuilder.Stretch(area);
            var marker = Node("Marker", area);
            marker.anchorMin = marker.anchorMax = marker.pivot = new Vector2(0.5f, 0.5f);
            marker.sizeDelta = new Vector2(40f, 40f);
            var border = Img("Border", marker, null, new Color(0f, 0f, 0f, 0.7f));
            Place(border.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(26f, 26f));
            border.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            var diamond = Img("Diamond", marker, null, new Color(1f, 0.95f, 0.8f));
            Place(diamond.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(18f, 18f));
            diamond.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            var dot = Img("Dot", marker, null, new Color(0.85f, 0.2f, 0.25f));
            Place(dot.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(7f, 7f));
            dot.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);

            var ui = area.gameObject.AddComponent<LockOnMarkerUI>();
            var so = new SerializedObject(ui);
            so.FindProperty("data").objectReferenceValue = data;
            so.FindProperty("player").objectReferenceValue = player;
            so.FindProperty("viewCamera").objectReferenceValue = mainCamera;
            so.FindProperty("marker").objectReferenceValue = marker;
            so.ApplyModifiedPropertiesWithoutUndo();
            return marker;
        }

        // ---------- 勝敗畫面 ----------

        static void BuildResult(Transform parent, BattleUIData data, BattleManager battle, CutsceneDirector intro, EventSystem eventSystem)
        {
            var result = Node("Result", parent, typeof(CanvasGroup));
            IntroCutsceneBuilder.Stretch(result);
            var dim = Img("Dim", result, null, new Color(0f, 0f, 0f, 0.65f));
            IntroCutsceneBuilder.Stretch(dim.rectTransform);
            dim.raycastTarget = true; // 擋住後面

            // 勝利：標題、副標、戰鬥資訊
            var victory = Node("Victory", result);
            IntroCutsceneBuilder.Stretch(victory);
            var title = Label("Title", victory, data.victoryTitle, 150, FontStyle.Bold, data.victoryColor, TextAnchor.MiddleCenter);
            Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 250f), new Vector2(1400f, 190f));
            var subtitle = Label("Subtitle", victory, "", 54, FontStyle.Bold, new Color(1f, 0.93f, 0.85f), TextAnchor.MiddleCenter);
            Place(subtitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 120f), new Vector2(1400f, 80f));
            var stats = Label("Stats", victory, "", 38, FontStyle.Normal, Color.white, TextAnchor.MiddleCenter);
            Place(stats.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -10f), new Vector2(1000f, 160f));
            stats.lineSpacing = 1.2f;

            // 戰敗：大字「菜」、中字、一行戰鬥資訊，按鈕提示下面一行灰色小字（字級、顏色、高度執行時由 BattleResultUI 照 BattleUIData 再套用一次）
            var defeat = Node("Defeat", result);
            IntroCutsceneBuilder.Stretch(defeat);
            var defeatTitle = Label("Title", defeat, data.defeatTitle, data.defeatTitleSize, FontStyle.Bold, data.defeatTitleStartColor, TextAnchor.MiddleCenter);
            Place(defeatTitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, data.defeatTitleY), new Vector2(1400f, 320f));
            var defeatSubtitle = Label("Subtitle", defeat, data.defeatSubtitle, data.defeatSubtitleSize, FontStyle.Bold, data.defeatTextColor, TextAnchor.MiddleCenter);
            Place(defeatSubtitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, data.defeatSubtitleY), new Vector2(1400f, 90f));
            var defeatStats = Label("Stats", defeat, "", data.defeatStatsSize, FontStyle.Normal, data.defeatTextColor, TextAnchor.MiddleCenter);
            Place(defeatStats.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, data.defeatStatsY), new Vector2(1400f, 40f));
            var defeatHint = Label("Hint", defeat, data.defeatHint, data.defeatHintSize, FontStyle.Normal, data.defeatHintColor, TextAnchor.MiddleCenter);
            Place(defeatHint.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, data.defeatHintY), new Vector2(1400f, 36f));

            var buttonImage = Img("RetryButton", result, roundSprite, Color.white);
            buttonImage.type = Image.Type.Sliced;
            buttonImage.raycastTarget = true;
            Place(buttonImage.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -170f), new Vector2(360f, 84f));
            var button = buttonImage.gameObject.AddComponent<Button>();
            button.targetGraphic = buttonImage;
            var colors = button.colors;
            colors.normalColor = new Color(0.2f, 0.2f, 0.24f, 0.95f);
            colors.highlightedColor = new Color(0.45f, 0.38f, 0.2f, 1f);
            colors.selectedColor = new Color(0.45f, 0.38f, 0.2f, 1f);
            colors.pressedColor = new Color(0.7f, 0.58f, 0.25f, 1f);
            button.colors = colors;
            var label = Label("Label", buttonImage.transform, data.defeatButton, 40, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            Stretch(label.rectTransform, Vector2.zero, Vector2.zero);
            var hint = Label("Hint", result, data.buttonHintKeyboard, 24, FontStyle.Normal, new Color(1f, 1f, 1f, 0.7f), TextAnchor.MiddleCenter);
            Place(hint.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -240f), new Vector2(600f, 36f));

            var ui = result.gameObject.AddComponent<BattleResultUI>();
            var so = new SerializedObject(ui);
            so.FindProperty("data").objectReferenceValue = data;
            so.FindProperty("battle").objectReferenceValue = battle;
            so.FindProperty("intro").objectReferenceValue = intro;
            so.FindProperty("group").objectReferenceValue = result.GetComponent<CanvasGroup>();
            so.FindProperty("titleText").objectReferenceValue = title;
            so.FindProperty("subtitleText").objectReferenceValue = subtitle;
            so.FindProperty("statsText").objectReferenceValue = stats;
            so.FindProperty("victoryGroup").objectReferenceValue = victory.gameObject;
            so.FindProperty("defeatGroup").objectReferenceValue = defeat.gameObject;
            so.FindProperty("defeatTitleText").objectReferenceValue = defeatTitle;
            so.FindProperty("defeatSubtitleText").objectReferenceValue = defeatSubtitle;
            so.FindProperty("defeatHintText").objectReferenceValue = defeatHint;
            so.FindProperty("defeatStatsText").objectReferenceValue = defeatStats;
            so.FindProperty("retryButton").objectReferenceValue = button;
            so.FindProperty("retryLabel").objectReferenceValue = label;
            so.FindProperty("hintText").objectReferenceValue = hint;
            so.FindProperty("eventSystem").objectReferenceValue = eventSystem;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------- 共用 ----------

        internal static EventSystem EnsureEventSystem()
        {
            var existing = Object.FindAnyObjectByType<EventSystem>();
            if (existing != null)
            {
                if (existing.GetComponent<BaseInputModule>() == null) Undo.AddComponent<InputSystemUIInputModule>(existing.gameObject);
                return existing;
            }
            var go = new GameObject(EventSystemName, typeof(EventSystem));
            Undo.RegisterCreatedObjectUndo(go, "Build Battle UI");
            var module = go.AddComponent<InputSystemUIInputModule>();
            module.AssignDefaultActions(); // 內建的 UI 動作（滑鼠、Enter、手把 A），不動玩家的 Player map
            return go.GetComponent<EventSystem>();
        }

        // 一條量條：深色底 → Inner（內縮 3）→ Trail（延遲扣血）、Fill
        static ResourceBar Bar(string name, RectTransform parent, BattleUIData data, Vector2 pos, Vector2 size, bool trail, Color color)
        {
            var bg = Img(name, parent, null, new Color(0f, 0f, 0f, 0.6f));
            var rt = bg.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = Vector2.zero;
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            var inner = Node("Inner", rt);
            Stretch(inner, new Vector2(3f, 3f), new Vector2(-3f, -3f));
            RectTransform trailRect = null;
            if (trail)
            {
                var trailImage = Img("Trail", inner, null, data.trailColor);
                trailRect = trailImage.rectTransform;
                Stretch(trailRect, Vector2.zero, Vector2.zero);
            }
            var fill = Img("Fill", inner, null, color);
            Stretch(fill.rectTransform, Vector2.zero, Vector2.zero);

            var bar = bg.gameObject.AddComponent<ResourceBar>();
            var so = new SerializedObject(bar);
            so.FindProperty("data").objectReferenceValue = data;
            so.FindProperty("fill").objectReferenceValue = fill.rectTransform;
            so.FindProperty("trail").objectReferenceValue = trailRect;
            so.FindProperty("useTrail").boolValue = trail;
            so.ApplyModifiedPropertiesWithoutUndo();
            return bar;
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

        static RectTransform Node(string name, Transform parent, params System.Type[] components)
        {
            var types = new List<System.Type> { typeof(RectTransform) };
            types.AddRange(components);
            var go = new GameObject(name, types.ToArray());
            go.transform.SetParent(parent, false);
            var group = go.GetComponent<CanvasGroup>();
            if (group != null)
            {
                group.alpha = 0f;
                group.interactable = false;
                group.blocksRaycasts = false;
            }
            return go.GetComponent<RectTransform>();
        }

        static Image Img(string name, Transform parent, Sprite sprite, Color color)
        {
            var rt = Node(name, parent, typeof(CanvasRenderer), typeof(Image));
            var image = rt.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        static Text Label(string name, Transform parent, string text, int size, FontStyle style, Color color, TextAnchor align)
        {
            var rt = Node(name, parent, typeof(CanvasRenderer), typeof(Text), typeof(Outline));
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
            outline.effectDistance = new Vector2(2f, -2f);
            return t;
        }

        static void Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
        }

        static void Stretch(RectTransform rt, Vector2 offsetMin, Vector2 offsetMax)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
        }
    }
}
