using BossFight.Battle;
using UnityEngine;
using UnityEngine.UI;
namespace BossFight.UI
{
    /// <summary>只訂閱流程事件；玩家 HUD 留用教學版本，Boss 面板沿用既有量條與頭暈倒數。</summary>
    public class GuardianBattleUI : MonoBehaviour
    {
        [SerializeField] GuardianPhaseController guardian;
        [SerializeField] CanvasGroup bossGroup;
        [SerializeField] Text hint;
        void Awake() { bossGroup.alpha = 0f; bossGroup.blocksRaycasts = false; hint.text = string.Empty; }
        void OnEnable() { guardian.OnBattleStarted += Started; guardian.OnHintChanged += Hint; }
        void OnDisable() { guardian.OnBattleStarted -= Started; guardian.OnHintChanged -= Hint; }
        void Started() => bossGroup.alpha = 1f;
        void Hint(string value) => hint.text = value;
    }
}
