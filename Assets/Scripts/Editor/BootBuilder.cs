using System;
using BossFight.Core;
using BossFight.Progression;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BossFight.EditorTools
{
    /// <summary>
    /// Tools > Progression > Build Boot Scene：重建開機分流的 Boot 場景（Assets/Scenes/Boot.unity，Build Settings 第 0 個）。
    /// 場景裡只有一台清成全黑的相機、GameOptions（讀玩家設定：開發者模式、略過序章）與 BootRouter（存檔沒完成序章就進 Prologue，否則進 Hub）。
    /// 最後跑 HubBuilder.SetBuildScenes：Boot 0、Hub 1、BossArena 2、Prologue 3。
    /// </summary>
    public static class BootBuilder
    {
        public const string ScenePath = "Assets/Scenes/Boot.unity";
        const string OptionsDataPath = "Assets/ScriptableObjects/Options/GameOptionsData.asset";

        [MenuItem("Tools/Progression/Build Boot Scene")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("請先停止 Play。");
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var previous = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camera = new GameObject("BlackCamera").AddComponent<UnityEngine.Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.cullingMask = 0;

            var go = new GameObject("Boot");
            var options = go.AddComponent<GameOptions>();
            DaoDunDogBuilder.SetReference(options, "data", AssetDatabase.LoadAssetAtPath<GameOptionsData>(OptionsDataPath));
            var router = go.AddComponent<BootRouter>();
            DaoDunDogBuilder.SetReference(router, "data", AssetDatabase.LoadAssetAtPath<SceneFlowData>(SceneFlowBuilder.DataPath));
            DaoDunDogBuilder.SetReference(router, "options", options);

            EditorSceneManager.SaveScene(scene, ScenePath);
            HubBuilder.SetBuildScenes();
            if (!string.IsNullOrEmpty(previous)) EditorSceneManager.OpenScene(previous);
            Debug.Log($"[Boot] 開機分流場景已建立：{ScenePath}；Build Settings：Boot 0、Hub 1、BossArena 2、Prologue 3");
        }
    }
}
