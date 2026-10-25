using BossFight.Progression;
using UnityEngine;

namespace BossFight.Hub
{
    /// <summary>
    /// 休息區的傳送門（T20 起是正式的站點）：對它按 Player/Interact 打開挑戰清單（HubStationUI，目前只有 OIIA 貓一列），
    /// 確認後介面呼叫 Depart，經 SceneFlow.GoToBattle 進 Boss 戰（黑幕淡出期間介面維持開著、輸入維持鎖住）。
    /// 之後加 Boss 時清單多一列，Depart 再帶上要挑戰哪一隻（T24 的 BossDefinition）。
    /// </summary>
    public class Portal : Interactable
    {
        [SerializeField] SceneFlow sceneFlow;

        /// <summary>出發挑戰（換到 Boss 戰場景）。</summary>
        public void Depart()
        {
            if (sceneFlow != null) sceneFlow.GoToBattle();
        }
    }
}
