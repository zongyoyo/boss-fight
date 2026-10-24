using System.Collections.Generic;
using BossFight.Battle;
using BossFight.Core;
using BossFight.Hub;
using BossFight.Progression;
using BossFight.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace BossFight.EditorTools
{
    /// <summary>
    /// Tools > Progression > Build Scene Flow：在目前的場景建立換場景用的 SceneFlow（T17）。
    /// 1. 資產（沒有才建）：ScriptableObjects/Progression/SceneFlowData.asset（場景名稱、出生點 id、黑幕時間）。
    /// 2. SceneFlow 物件（有就沿用）：黑幕 Canvas（sortingOrder 1000，全螢幕黑色 Image＋CanvasGroup）每次重建。
    /// 3. 接線：SceneFlow 的 GamePause；BattleManager、PauseMenuUI、傳送門（Portal）的 sceneFlow。
    /// 出生點清單在 PlayerSpawner（T18，PlayerSpawnerBuilder），換場景進來時由它把玩家生成在出生點。
    /// PauseMenuBuilder.Wire 最後會呼叫這裡（HubBuilder 建休息區時也會走到）。
    /// </summary>
    public static class SceneFlowBuilder
    {
        public const string DataPath = "Assets/ScriptableObjects/Progression/SceneFlowData.asset";
        const string RootName = "SceneFlow";
        const string FadeName = "Fade";

        [MenuItem("Tools/Progression/Build Scene Flow")]
        public static void BuildFromMenu()
        {
            Selection.activeGameObject = Build().gameObject;
        }

        public static SceneFlow Build()
        {
            var data = AssetDatabase.LoadAssetAtPath<SceneFlowData>(DataPath);
            if (data == null)
            {
                DaoDunDogBuilder.EnsureFolder(DataPath.Substring(0, DataPath.LastIndexOf('/')));
                data = ScriptableObject.CreateInstance<SceneFlowData>();
                AssetDatabase.CreateAsset(data, DataPath);
                AssetDatabase.SaveAssets();
            }

            var flow = Object.FindAnyObjectByType<SceneFlow>();
            if (flow == null)
            {
                var go = new GameObject(RootName);
                Undo.RegisterCreatedObjectUndo(go, "Build Scene Flow");
                flow = go.AddComponent<SceneFlow>();
            }

            var oldFade = flow.transform.Find(FadeName);
            if (oldFade != null) Undo.DestroyObjectImmediate(oldFade.gameObject);
            var fade = new GameObject(FadeName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
            Undo.RegisterCreatedObjectUndo(fade, "Build Scene Flow");
            fade.transform.SetParent(flow.transform, false);
            var canvas = fade.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000; // 蓋過 ESC 選單（200）
            var group = fade.GetComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            var black = new GameObject("Black", typeof(RectTransform), typeof(Image));
            black.transform.SetParent(fade.transform, false);
            var image = black.GetComponent<Image>();
            image.color = Color.black;
            image.raycastTarget = true; // 淡出時擋住滑鼠
            IntroCutsceneBuilder.Stretch(image.rectTransform);

            var so = new SerializedObject(flow);
            so.FindProperty("data").objectReferenceValue = data;
            so.FindProperty("fade").objectReferenceValue = group;
            so.FindProperty("pause").objectReferenceValue = Object.FindAnyObjectByType<GamePause>();
            so.ApplyModifiedPropertiesWithoutUndo();

            var wired = new List<string>();
            var battle = Object.FindAnyObjectByType<BattleManager>();
            if (battle != null)
            {
                DaoDunDogBuilder.SetReference(battle, "sceneFlow", flow);
                wired.Add("BattleManager");
            }
            var menu = Object.FindAnyObjectByType<PauseMenuUI>(FindObjectsInactive.Include);
            if (menu != null)
            {
                DaoDunDogBuilder.SetReference(menu, "sceneFlow", flow);
                wired.Add("PauseMenu");
            }
            foreach (var portal in Object.FindObjectsByType<Portal>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                DaoDunDogBuilder.SetReference(portal, "sceneFlow", flow);
                wired.Add(portal.name);
            }

            EditorSceneManager.MarkSceneDirty(flow.gameObject.scene);
            Debug.Log($"[SceneFlow] 換場景已建立：{DataPath}；接上 {(wired.Count > 0 ? string.Join("、", wired) : "（沒有）")}");
            return flow;
        }
    }
}
