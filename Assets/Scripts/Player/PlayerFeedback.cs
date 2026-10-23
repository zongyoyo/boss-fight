using BossFight.Core;
using UnityEngine;

namespace BossFight.Player
{
    /// <summary>
    /// 刀盾狗的打擊回饋（T11）：揮刀、命中（輕攻擊、第三段、重擊、處決分級）、被打中、格擋、破防、盾反、翻滾閃過、翻滾、跳躍、啃骨頭的音效與鏡頭震動。
    /// 只訂閱同一物件上的 PlayerAttack、PlayerHealth、PlayerController 的事件；數值在 CombatFeedbackData，經 FeedbackEmitter 播放。
    /// 盾反的畫面停頓照舊由 PlayerHealth 處理（PlayerStats.parryHitStop），這裡不另外停頓。
    /// </summary>
    [RequireComponent(typeof(FeedbackEmitter))]
    [DisallowMultipleComponent]
    public class PlayerFeedback : MonoBehaviour
    {
        [Tooltip("被打中、格擋的震動與音效位置：角色腳底往上的高度 (m)")]
        [SerializeField] float bodyHeight = 1f;

        FeedbackEmitter emitter;
        PlayerController controller;
        PlayerAttack attack;
        PlayerHealth health;
        // 穿過龍捲風這種每幀重試的判定時，一次翻滾只響一次
        bool evadedThisRoll;

        CombatFeedbackData Data => emitter.Data;
        Vector3 Body => transform.position + Vector3.up * bodyHeight;

        void Awake()
        {
            emitter = GetComponent<FeedbackEmitter>();
            controller = GetComponent<PlayerController>();
            attack = GetComponent<PlayerAttack>();
            health = GetComponent<PlayerHealth>();
        }

        void OnEnable()
        {
            if (attack != null)
            {
                attack.OnSwing += HandleSwing;
                attack.OnAttackHit += HandleAttackHit;
            }
            if (health != null) health.OnDamageTaken += HandleDamageTaken;
            if (controller != null)
            {
                controller.OnRollStarted += HandleRollStarted;
                controller.OnJumped += HandleJumped;
                controller.OnActionChanged += HandleActionChanged;
            }
        }

        void OnDisable()
        {
            if (attack != null)
            {
                attack.OnSwing -= HandleSwing;
                attack.OnAttackHit -= HandleAttackHit;
            }
            if (health != null) health.OnDamageTaken -= HandleDamageTaken;
            if (controller != null)
            {
                controller.OnRollStarted -= HandleRollStarted;
                controller.OnJumped -= HandleJumped;
                controller.OnActionChanged -= HandleActionChanged;
            }
        }

        void HandleSwing()
        {
            if (Data == null) return;
            emitter.Play(attack.IsHeavy || attack.IsExecution ? Data.heavySwing : Data.swing, Body);
        }

        void HandleAttackHit(HitReport report)
        {
            if (Data == null) return;
            Vector3 pos = report.hurtbox != null ? report.hurtbox.Center : Body;
            if (report.result == DamageResult.Evaded)
            {
                emitter.Play(Data.hitDeflected, pos);
                return;
            }
            if (report.result != DamageResult.Hit) return;

            if (report.info.isExecution) emitter.Play(Data.executionHit, pos);
            else if (attack.IsHeavy) emitter.Play(Data.heavyHit, pos);
            else if (attack.IsLastComboStep) emitter.Play(Data.comboFinisherHit, pos);
            else emitter.Play(Data.hit, pos);
        }

        void HandleDamageTaken(PlayerDamageReport report)
        {
            if (Data == null) return;
            switch (report.result)
            {
                case DamageResult.Hit: emitter.Play(Data.hurt, Body); break;
                case DamageResult.Blocked: emitter.Play(Data.block, Body); break;
                case DamageResult.GuardBroken: emitter.Play(Data.guardBreak, Body); break;
                case DamageResult.Parried: emitter.Play(Data.parry, Body); break;
                case DamageResult.Evaded:
                    // 只有翻滾閃過才響；受擊後無敵、Debug 無敵時不響
                    if (!controller.IsRolling || evadedThisRoll) break;
                    evadedThisRoll = true;
                    emitter.Play(Data.evade, Body);
                    break;
            }
        }

        void HandleRollStarted(Vector3 direction)
        {
            evadedThisRoll = false;
            if (Data != null) emitter.Play(Data.roll, transform.position);
        }

        void HandleJumped()
        {
            if (Data != null) emitter.Play(Data.jump, transform.position);
        }

        void HandleActionChanged(PlayerAction action)
        {
            if (action == PlayerAction.Heal && Data != null) emitter.Play(Data.heal, Body);
        }
    }
}
