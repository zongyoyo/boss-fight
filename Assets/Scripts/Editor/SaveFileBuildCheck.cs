using System.IO;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace BossFight.EditorTools
{
    /// <summary>
    /// 打包前檢查（T31）：存檔放在 Application.persistentDataPath，本來就不會進 build；這裡再擋一次，
    /// Assets 底下（含 StreamingAssets）有 save*.json 就停止打包，開發時的存檔不會被當成資源匯出。
    /// 編輯器的欄位是 save_editor_slot*.json、打包版是 save_slot*.json，同一台電腦上兩邊也不會互相讀到。
    /// </summary>
    public class SaveFileBuildCheck : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            var found = Directory.GetFiles(Application.dataPath, "save*.json", SearchOption.AllDirectories);
            if (found.Length == 0) return;
            foreach (var path in found) Debug.LogError($"[SaveFileBuildCheck] Assets 裡有存檔，不能跟著打包：{path}");
            throw new BuildFailedException($"[SaveFileBuildCheck] Assets 裡有 {found.Length} 個存檔檔案（save*.json），停止打包（Console 有清單）。");
        }
    }
}
