using BossFight.Battle;
using BossFight.Boss;
using UnityEngine;
namespace BossFight.UI
{
    /// <summary>以事件更新名稱、血條色與自損區段；所有表現參數來自 SO。</summary>
    public class GuardianPhaseFeedbackUI : MonoBehaviour
    {
        [SerializeField] GuardianPhaseController guardian;
        [SerializeField] BossHealth health;
        [SerializeField] BossHudUI hud;
        void OnEnable() { guardian.OnStageChanged += Stage; health.OnSelfCost += Cost; }
        void OnDisable() { guardian.OnStageChanged -= Stage; health.OnSelfCost -= Cost; }
        void Stage(GuardianStage stage)
        { if (stage == GuardianStage.Phase2) hud.SetPresentation(guardian.Data.phase2Name, guardian.Data.phase2HealthColor); }
        void Cost(float before, float after, float max) => hud.FlashSelfCost(before, after, max, guardian.Data.selfCostFlashTime);
    }
}
