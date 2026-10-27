using BossFight.Core;
using BossFight.Hazards;
using UnityEngine;

namespace BossFight.Boss
{
    /// <summary>T27 補齊教學角色的預兆、揮擊、受擊、巴掌與死亡回饋；不參與玩法。</summary>
    [RequireComponent(typeof(FeedbackEmitter))]
    public class TeachingActorFeedback : MonoBehaviour
    {
        [SerializeField] TrainingSapling sapling;
        [SerializeField] DaisyMentor mentor;
        [SerializeField] TimedFx sparkPrefab;
        [SerializeField] Transform impactAnchor;
        [ColorUsage(true, true), SerializeField] Color hitColor = new Color(3f, 1.5f, 0.5f);
        [ColorUsage(true, true), SerializeField] Color deflectColor = new Color(1.5f, 1.5f, 1.5f);
        [SerializeField] float sparkSize = 0.6f;
        FeedbackEmitter emitter;
        Vector3 Position => impactAnchor != null ? impactAnchor.position : transform.position;
        void Awake() => emitter = GetComponent<FeedbackEmitter>();
        void OnEnable()
        {
            if (sapling != null)
            {
                sapling.OnAttackWarning += Warning;
                sapling.OnAttackWindowOpened += Swing;
                sapling.OnDamaged += Damaged;
                sapling.OnStunned += Stunned;
                sapling.OnDefeated += Defeated;
            }
            if (mentor != null) { mentor.OnActionChanged += Action; mentor.OnPrankRequested += Prank; }
        }
        void OnDisable()
        {
            if (sapling != null)
            {
                sapling.OnAttackWarning -= Warning;
                sapling.OnAttackWindowOpened -= Swing;
                sapling.OnDamaged -= Damaged;
                sapling.OnStunned -= Stunned;
                sapling.OnDefeated -= Defeated;
            }
            if (mentor != null) { mentor.OnActionChanged -= Action; mentor.OnPrankRequested -= Prank; }
        }
        void Warning(SaplingTelegraph warning)
        { if (emitter.Data != null) emitter.Play(warning.canBlock ? emitter.Data.telegraphBlockable : emitter.Data.telegraphUnblockable, Position); }
        void Swing(int id) { if (emitter.Data != null) emitter.Play(emitter.Data.swing, Position); }
        void Damaged(SaplingDamageReport report)
        {
            if (report.result != DamageResult.Hit && report.result != DamageResult.Evaded) return;
            TimedFx.Spawn(sparkPrefab, Position, Quaternion.identity, report.result == DamageResult.Hit ? hitColor : deflectColor, sparkSize);
            // 玩家命中音效已由 PlayerFeedback 分級播放，避免一刀雙響。
        }
        void Stunned()
        {
            TimedFx.Spawn(sparkPrefab, Position, Quaternion.identity, hitColor, sparkSize);
            if (emitter.Data != null) emitter.Play(emitter.Data.stunned, Position);
        }
        void Defeated(SaplingDefeatReason reason)
        { if (emitter.Data != null) emitter.Play(emitter.Data.defeated, Position); }
        void Action(DaisyAction action)
        {
            if (emitter.Data == null) return;
            if (action == DaisyAction.Whip || action == DaisyAction.Slap) emitter.Play(emitter.Data.heavySwing, Position);
        }
        void Prank() => TimedFx.Spawn(sparkPrefab, Position, Quaternion.identity, deflectColor, sparkSize);
    }
}
