using BossFight.Battle;
using BossFight.Cutscene;
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
        [SerializeField] PrologueOutcome outcome;
        [SerializeField] PrologueCutsceneData presentation;
        RectTransform bar;
        Vector2 barHome;
        float slideAge;
        bool sliding;
        void Awake() { bossGroup.alpha = 0f; bossGroup.blocksRaycasts = false; hint.text = string.Empty; bar = bossGroup.GetComponent<RectTransform>(); if (bar != null) barHome = bar.anchoredPosition; }
        void OnEnable() { guardian.OnBattleStarted += Started; guardian.OnHintChanged += Hint; if (outcome != null) outcome.OnPresentationHint += Hint; }
        void OnDisable() { guardian.OnBattleStarted -= Started; guardian.OnHintChanged -= Hint; if (outcome != null) outcome.OnPresentationHint -= Hint; }
        void Started() { bossGroup.alpha = 1f; slideAge = 0f; sliding = presentation != null && bar != null; }
        void Update()
        {
            if (!sliding) return;
            slideAge += Time.deltaTime;
            float t = presentation.hudSlideTime > 0f ? Mathf.Clamp01(slideAge / presentation.hudSlideTime) : 1f;
            bar.anchoredPosition = barHome + Vector2.down * (180f * (1f - Mathf.SmoothStep(0f, 1f, t)));
            if (t >= 1f) sliding = false;
        }
        void Hint(string value) => hint.text = value;
    }
}
