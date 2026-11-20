using System;
using System.Collections.Generic;
using System.Linq;
using BossFight.Core;
using BossFight.Hub;
using BossFight.Player;
using BossFight.Title;
using BossFight.UI;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace BossFight.EditorTools
{
    /// <summary>
    /// Tools > Title > Build Title：重建標題畫面 Assets/Scenes/Title.unity（T32）。
    /// 1. 背景：開新場景，把休息區（Hub.unity，附加開啟、不存檔）的主光（HubAtmosphere）、BaseVolume、Main Camera、正式佈景 Level/Scenery、
    ///    營火的火光（FireFlicker）複製過來，拿掉所有碰撞；佈景只留包圍盒跟標題鏡頭視野相交的（TitleLayoutData.cullMargin、cullDistance），場景檔不會整份翻倍。
    ///    不複製站點（村長、傳送門等的佔位與互動）與玩法物件（地面方塊、隱形牆、樹幹碰撞）；村長不放（使用者決定：會被選單擋到）。
    /// 2. 鏡頭：營火站點鏡頭的位置與朝向，再照 TitleLayoutData 移動、轉向，讓刀盾狗與火堆落在畫面右側；Main Camera 拿掉 CinemachineBrain，固定不動。
    /// 3. 刀盾狗：角色 prefab 的複本、完全拆開，只留外觀與 DaoDunDogAnimator（其他玩家元件、受擊範圍、刀的判定都刪掉），放在營火站位點；TitleBackdrop 在 Start 讓它坐好。
    /// 4. 系統：GameOptions（GameOptions＋GamePause＋InputHintDevice）、SceneFlow（黑幕）、EventSystem、TitleManager。
    /// 5. 介面（TitleUI Canvas）：左上遊戲名、左下主選單；文字與版面在 TitleUIData，顏色沿用 HubUIData。
    /// 休息區的佈景改了（Build Hub、Tools > Hub > Build Scenery）後，標題場景已存在時會跟著重建。
    /// </summary>
    public static class TitleBuilder
    {
        public const string ScenePath = "Assets/Scenes/Title.unity";
        const string DataDir = "Assets/ScriptableObjects/Title";
        const string LayoutPath = DataDir + "/TitleLayout.asset";
        const string UIDataPath = "Assets/ScriptableObjects/UI/TitleUIData.asset";
        const string HubUIDataPath = "Assets/ScriptableObjects/Hub/HubUIData.asset";
        const string OptionsDataPath = "Assets/ScriptableObjects/Options/GameOptionsData.asset";
        const string CanvasName = "TitleUI";
        const float ReferenceAspect = 16f / 9f;

        static Font font;
        static Sprite roundSprite;

        [MenuItem("Tools/Title/Build Title")]
        public static void BuildFromMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            string previous = SceneManager.GetActiveScene().path;
            Build();
            if (!string.IsNullOrEmpty(previous) && previous != ScenePath) EditorSceneManager.OpenScene(previous);
        }

        /// <summary>標題場景已存在時重建（休息區的佈景改了之後跑），最後重新開啟原本的場景。</summary>
        public static void RebuildIfExists()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null) return;
            string previous = SceneManager.GetActiveScene().path;
            Build();
            if (!string.IsNullOrEmpty(previous) && previous != ScenePath) EditorSceneManager.OpenScene(previous);
        }

        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("請先停止 Play。");
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(HubBuilder.ScenePath) == null)
            {
                Debug.LogError($"[Title] 找不到 {HubBuilder.ScenePath}（Tools > Hub > Build Hub）。");
                return;
            }

            // 先開新場景再載入資產：NewScene 會卸載沒被場景引用的資產
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            DaoDunDogBuilder.EnsureFolder(DataDir);
            var layout = GetOrCreate<TitleLayoutData>(LayoutPath);
            var uiData = GetOrCreate<TitleUIData>(UIDataPath);
            var hubStyle = AssetDatabase.LoadAssetAtPath<HubUIData>(HubUIDataPath);
            if (hubStyle == null) hubStyle = ScriptableObject.CreateInstance<HubUIData>(); // 只拿預設顏色

            var hub = EditorSceneManager.OpenScene(HubBuilder.ScenePath, OpenSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            Camera camera;
            Transform stand;
            int kept, culled;
            try
            {
                camera = CopyBackdrop(hub, scene, layout, out stand, out kept, out culled);
            }
            finally
            {
                EditorSceneManager.CloseScene(hub, true);
            }
            if (camera == null) return;

            var dog = BuildDog(stand);

            // ---------- 系統 ----------
            var optionsData = AssetDatabase.LoadAssetAtPath<GameOptionsData>(OptionsDataPath);
            var options = PauseMenuBuilder.EnsureOptions(optionsData, out _);
            DaoDunDogBuilder.GetOrAdd<InputHintDevice>(options.gameObject);
            var eventSystem = BattleUIBuilder.EnsureEventSystem();
            var flow = SceneFlowBuilder.Build();

            var titleGo = new GameObject("Title");
            var title = titleGo.AddComponent<TitleManager>();
            DaoDunDogBuilder.SetReference(title, "sceneFlow", flow);
            var backdrop = titleGo.AddComponent<TitleBackdrop>();
            DaoDunDogBuilder.SetReference(backdrop, "dog", dog);

            BuildUI(title, eventSystem, uiData, hubStyle);

            // 天色：漸層天空、環境光、霧（HubAtmosphere 在 Awake 也會再套一次）
            HubSceneryBuilder.ApplyAtmosphere(AssetDatabase.LoadAssetAtPath<HubAtmosphereData>(HubBuilder.AtmospherePath));

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Title] 標題畫面已建立：{ScenePath}（佈景留下 {kept} 件、視野外略過 {culled} 件）");
        }

        // ---------- 背景 ----------

        static Camera CopyBackdrop(Scene hub, Scene title, TitleLayoutData layout, out Transform stand, out int kept, out int culled)
        {
            stand = null;
            kept = culled = 0;
            var roots = hub.GetRootGameObjects();
            var all = roots.SelectMany(g => g.GetComponentsInChildren<Transform>(true)).ToList();
            var campfire = roots.SelectMany(g => g.GetComponentsInChildren<Campfire>(true)).FirstOrDefault();
            var scenery = all.FirstOrDefault(t => t.name == "Scenery" && t.parent != null && t.parent.name == "Level");
            var hubCamera = roots.SelectMany(g => g.GetComponentsInChildren<Camera>(true)).FirstOrDefault(c => c.CompareTag("MainCamera"));
            var sun = roots.SelectMany(g => g.GetComponentsInChildren<HubAtmosphere>(true)).FirstOrDefault();
            var volume = roots.SelectMany(g => g.GetComponentsInChildren<Volume>(true)).FirstOrDefault(v => v.name == "BaseVolume");
            if (campfire == null || scenery == null || hubCamera == null)
            {
                Debug.LogError("[Title] 休息區缺少營火、Level/Scenery 或 Main Camera，先跑 Tools > Hub > Build Hub。");
                return null;
            }
            var stationCam = campfire.transform.Find("StationCamera");
            stand = campfire.transform.Find("StandPoint");
            var fireLight = campfire.GetComponentsInChildren<FireFlicker>(true).FirstOrDefault();
            if (stationCam == null || stand == null)
            {
                Debug.LogError("[Title] 營火缺少 StationCamera 或 StandPoint。");
                return null;
            }
            // 站位點在關掉休息區場景後就不見了，先複製一份位置
            var standCopy = new GameObject("DogSpot").transform;
            SceneManager.MoveGameObjectToScene(standCopy.gameObject, title);
            standCopy.SetPositionAndRotation(stand.position, stand.rotation);
            stand = standCopy;

            // 鏡頭：Main Camera 的複本（URP 設定、後處理開關照舊），拿掉 Cinemachine
            var cam = Copy(hubCamera.gameObject, title, null).GetComponent<Camera>();
            var brain = cam.GetComponent<CinemachineBrain>();
            if (brain != null) Object.DestroyImmediate(brain);
            var lens = stationCam.GetComponent<CinemachineCamera>();
            float fov = layout.fieldOfView > 0f ? layout.fieldOfView : lens != null ? lens.Lens.FieldOfView : cam.fieldOfView;
            Vector3 pos = stationCam.position + stationCam.rotation * layout.cameraLocalOffset;
            var rot = Quaternion.AngleAxis(layout.cameraYawOffset, Vector3.up) * stationCam.rotation * Quaternion.AngleAxis(layout.cameraPitchOffset, Vector3.right);
            cam.transform.SetPositionAndRotation(pos, rot);
            cam.fieldOfView = fov;
            if (lens != null)
            {
                cam.nearClipPlane = lens.Lens.NearClipPlane;
                cam.farClipPlane = lens.Lens.FarClipPlane;
            }

            if (sun != null) Copy(sun.gameObject, title, null);
            if (volume != null) Copy(volume.gameObject, title, null);
            var backdrop = new GameObject("Backdrop").transform;
            SceneManager.MoveGameObjectToScene(backdrop.gameObject, title);
            if (fireLight != null) Copy(fireLight.gameObject, title, backdrop);

            // 佈景：整份複製，再刪掉視野外的件（第二層：Forest／Props／Village／Stations 的每一件；地面與草叢整區留著）
            var sceneryCopy = Copy(scenery.gameObject, title, backdrop).transform;
            var planes = FrustumPlanes(pos, rot, fov, cam.nearClipPlane, layout.cullDistance);
            foreach (Transform group in sceneryCopy)
            {
                if (group.GetComponentsInChildren<Renderer>(true).Length == 0 && group.childCount == 0) continue;
                if (group.GetComponent<Renderer>() != null || group.GetComponent<InstancedScatter>() != null) { kept++; continue; }
                var children = group.Cast<Transform>().ToList();
                foreach (var item in children)
                {
                    if (item.GetComponentInChildren<InstancedScatter>(true) != null || Visible(item, planes, layout.cullMargin)) kept++;
                    else
                    {
                        Object.DestroyImmediate(item.gameObject);
                        culled++;
                    }
                }
            }
            // 碰撞全部拿掉（標題畫面沒有玩法）
            foreach (var collider in sceneryCopy.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
            return cam;
        }

        static GameObject Copy(GameObject source, Scene scene, Transform parent)
        {
            var copy = Object.Instantiate(source);
            copy.name = source.name;
            if (copy.transform.parent == null) SceneManager.MoveGameObjectToScene(copy, scene);
            copy.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
            if (parent != null) copy.transform.SetParent(parent, true);
            return copy;
        }

        static Plane[] FrustumPlanes(Vector3 pos, Quaternion rot, float fov, float near, float far)
        {
            var temp = new GameObject("FrustumProbe").AddComponent<Camera>();
            try
            {
                temp.transform.SetPositionAndRotation(pos, rot);
                temp.fieldOfView = fov;
                temp.aspect = ReferenceAspect;
                temp.nearClipPlane = Mathf.Max(0.01f, near);
                temp.farClipPlane = far;
                return GeometryUtility.CalculateFrustumPlanes(temp);
            }
            finally
            {
                Object.DestroyImmediate(temp.gameObject);
            }
        }

        static bool Visible(Transform item, Plane[] planes, float margin)
        {
            var renderers = item.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return false;
            var bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            bounds.Expand(margin * 2f);
            return GeometryUtility.TestPlanesAABB(planes, bounds);
        }

        // ---------- 刀盾狗 ----------

        static DaoDunDogAnimator BuildDog(Transform spot)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DaoDunDogBuilder.PrefabPath);
            if (prefab == null)
            {
                Debug.LogError($"[Title] 找不到 {DaoDunDogBuilder.PrefabPath}（Tools > Player > Build DaoDun Dog）。");
                return null;
            }
            var dog = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            PrefabUtility.UnpackPrefabInstance(dog, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            dog.name = "DaoDunDog";
            dog.transform.SetPositionAndRotation(spot.position, spot.rotation);
            Object.DestroyImmediate(spot.gameObject);

            // 受擊範圍、刀的判定整個刪掉
            foreach (var t in dog.GetComponentsInChildren<Transform>(true).ToList())
            {
                if (t != null && t != dog.transform && (t.GetComponent<Hurtbox>() != null || t.GetComponent<Hitbox>() != null)) Object.DestroyImmediate(t.gameObject);
            }
            // 只留外觀與程序化姿勢；有 RequireComponent 依賴時多刪幾輪
            for (int pass = 0; pass < 6; pass++)
            {
                bool removed = false;
                foreach (var c in dog.GetComponentsInChildren<Component>(true))
                {
                    if (c == null || c is Transform || c is DaoDunDogAnimator || c is Renderer || c is MeshFilter) continue;
                    if (CanRemove(c))
                    {
                        Object.DestroyImmediate(c);
                        removed = true;
                    }
                }
                if (!removed) break;
            }
            return dog.GetComponent<DaoDunDogAnimator>();
        }

        // 沒有其他元件 RequireComponent 它時才刪（先刪依賴它的）
        static bool CanRemove(Component c)
        {
            var type = c.GetType();
            foreach (var other in c.GetComponents<Component>())
            {
                if (other == null || other == c) continue;
                foreach (RequireComponent req in other.GetType().GetCustomAttributes(typeof(RequireComponent), true))
                {
                    if ((req.m_Type0 != null && req.m_Type0.IsAssignableFrom(type)) ||
                        (req.m_Type1 != null && req.m_Type1.IsAssignableFrom(type)) ||
                        (req.m_Type2 != null && req.m_Type2.IsAssignableFrom(type))) return false;
                }
            }
            return true;
        }

        // ---------- 介面 ----------

        static void BuildUI(TitleManager title, UnityEngine.EventSystems.EventSystem eventSystem, TitleUIData data, HubUIData style)
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); // 內建字型會借用系統字型顯示中文
            roundSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

            var root = new GameObject(CanvasName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5; // 在 ESC 選單（200）與黑幕（1000）下面
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            // 左上：遊戲名
            var titleText = Label("GameTitle", root.transform, data.gameTitle, data.titleFontSize, FontStyle.Bold, data.titleColor, TextAnchor.UpperLeft);
            Place(titleText.rectTransform, new Vector2(0f, 1f), new Vector2(data.titleOffset.x, -data.titleOffset.y), new Vector2(1000f, data.titleFontSize * 1.4f));
            var shadow = titleText.gameObject.AddComponent<Shadow>();
            shadow.effectColor = data.titleShadowColor;
            shadow.effectDistance = new Vector2(4f, -4f);

            // 左下：主選單（由下往上：退出、設定、開始）
            var main = Rect("MainMenu", root.transform);
            main.anchorMin = Vector2.zero;
            main.anchorMax = Vector2.one;
            main.offsetMin = main.offsetMax = Vector2.zero;
            float step = data.buttonSize.y + data.buttonGap;
            var labels = new[] { data.startLabel, data.settingsLabel, data.quitLabel };
            var buttons = new Button[labels.Length];
            for (int i = 0; i < labels.Length; i++)
            {
                float y = data.buttonsOffset.y + step * (labels.Length - 1 - i);
                buttons[i] = MenuButton("Button_" + i, main, labels[i], new Vector2(data.buttonsOffset.x, y), data.buttonSize, data.buttonFontSize, style);
            }
            SetVerticalNavigation(buttons);

            var ui = root.AddComponent<TitleMenuUI>();
            var so = new SerializedObject(ui);
            so.FindProperty("data").objectReferenceValue = data;
            so.FindProperty("title").objectReferenceValue = title;
            so.FindProperty("eventSystem").objectReferenceValue = eventSystem;
            so.FindProperty("titleText").objectReferenceValue = titleText;
            so.FindProperty("mainPage").objectReferenceValue = main.gameObject;
            so.FindProperty("startButton").objectReferenceValue = buttons[0];
            so.FindProperty("settingsButton").objectReferenceValue = buttons[1];
            so.FindProperty("quitButton").objectReferenceValue = buttons[2];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        internal static void SetVerticalNavigation(IList<Button> buttons)
        {
            for (int i = 0; i < buttons.Count; i++)
            {
                var nav = new Navigation { mode = Navigation.Mode.Explicit };
                nav.selectOnUp = buttons[(i - 1 + buttons.Count) % buttons.Count];
                nav.selectOnDown = buttons[(i + 1) % buttons.Count];
                buttons[i].navigation = nav;
            }
        }

        static Button MenuButton(string name, Transform parent, string label, Vector2 pos, Vector2 size, int fontSize, HubUIData style)
        {
            var image = Img(name, parent, roundSprite, Color.white);
            image.type = Image.Type.Sliced;
            image.raycastTarget = true;
            Place(image.rectTransform, Vector2.zero, pos, size);
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.normalColor = style.buttonColor;
            colors.highlightedColor = colors.selectedColor = style.buttonHighlightColor;
            colors.pressedColor = style.buttonHighlightColor * 0.8f;
            colors.disabledColor = style.buttonColor * 0.6f;
            button.colors = colors;
            var text = Label("Label", image.transform, label, fontSize, FontStyle.Normal, style.textColor, TextAnchor.MiddleCenter);
            var rt = text.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return button;
        }

        static RectTransform Rect(string name, Transform parent, params Type[] components)
        {
            var types = new List<Type> { typeof(RectTransform) };
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

        // anchor 同時當 pivot：(0,0) = 左下角對齊、(0,1) = 左上角對齊
        static void Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
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
    }
}
