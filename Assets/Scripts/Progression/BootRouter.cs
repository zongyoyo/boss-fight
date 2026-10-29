using BossFight.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BossFight.Progression
{
    /// <summary>
    /// 開機分流（Boot 場景，Build Settings 第 0 個）：存檔還沒完成序章就進 Prologue，否則進休息區（Hub 的啟動出生點）；
    /// 開發者模式勾了「略過序章」（GameOptions.CanSkipPrologue）也直接進休息區，保留序章未完成。
    /// Boot 場景只有這個元件、GameOptions（讀玩家設定）與一台清成全黑的相機，載入後就換掉。
    /// </summary>
    [DisallowMultipleComponent]
    public class BootRouter : MonoBehaviour
    {
        [SerializeField] SceneFlowData data;
        [Tooltip("讀「開發者模式」與「略過序章」；同一個物件或場景裡的 GameOptions")]
        [SerializeField] GameOptions options;

        void Start()
        {
            if (data == null)
            {
                Debug.LogError("[Boot] 沒有接 SceneFlowData，不知道要進哪個場景。", this);
                return;
            }
            bool skip = ProfileStore.PrologueDone || (options != null && options.CanSkipPrologue);
            SceneManager.LoadScene(skip ? data.hubScene : data.prologueScene);
        }
    }
}
