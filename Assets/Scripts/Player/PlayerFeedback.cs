using BossFight.Core;
using BossFight.Hazards;
using UnityEngine;

namespace BossFight.Player
{
    /// <summary>
    /// 刀盾狗的打擊回饋（T11）：揮刀、命中（輕攻擊、第三段、重擊、處決分級）、被打中、格擋、破防、盾反、翻滾閃過、翻滾、跳躍、啃骨頭、湊滿一根骨頭的音效與鏡頭震動。
    /// 只訂閱同一物件上的 PlayerAttack、PlayerHealth、PlayerController 的事件；數值在 CombatFeedbackData，經 FeedbackEmitter 播放。
    /// 盾反的畫面停頓照舊由 PlayerHealth 處理（PlayerStats.parryHitStop），這裡不另外停頓。
    /// 可見回饋也在角色這一側（序章修正 A1–A5），三個場景、換角色都一樣：揮刀的刀光（輕攻擊每段換傾斜與大小、重擊與處決另一款），
    /// 成功閃避的綠色火花與閃避聲（只由 PlayerHealth.OnDodgeSucceeded 驅動，空滾、強制無敵、受擊後無敵都不播）。
    /// 被 Boss 打中、格擋、盾反的火花在攻擊方的 BossHitFeedback。
    /// </summary>
    [RequireComponent(typeof(FeedbackEmitter))]
    [DisallowMultipleComponent]
    public class PlayerFeedback : MonoBehaviour
    {
        [Tooltip("被打中、格擋的震動與音效位置：角色腳底往上的高度 (m)")]
        [SerializeField] float bodyHeight = 1f;

        [Header("刀光（純視覺）")]
        [Tooltip("輕攻擊的刀光")]
        [SerializeField] TimedFx slashPrefab;
        [Tooltip("重擊、處決的刀光")]
        [SerializeField] TimedFx heavySlashPrefab;
        [Tooltip("刀光中心離角色往前的距離 (m)")]
        [Min(0f)] [SerializeField] float slashForward = 0.5f;
        [Tooltip("輕攻擊每一段刀光沿前方軸的傾斜（度）；段數比這裡多時用最後一個")]
        [SerializeField] float[] comboSlashTilt = { 28f, -28f, 0f };
        [Tooltip("輕攻擊每一段刀光的大小倍率；段數比這裡多時用最後一個")]
        [SerializeField] float[] comboSlashSize = { 1f, 1f, 1.3f };
        [Tooltip("重擊、處決刀光的傾斜（度）：90 = 由上往下劈")]
        [SerializeField] float heavySlashTilt = 90f;
        [Tooltip("重擊、處決刀光的大小倍率")]
        [Min(0f)] [SerializeField] float heavySlashSize = 1.4f;
        [SerializeField] [ColorUsage(true, true)] Color slashColor = new Color(1.8f, 2f, 2.4f, 1f);

        [Header("成功閃避（純視覺）")]
        [SerializeField] TimedFx evadeSparkPrefab;
        [SerializeField] [ColorUsage(true, true)] Color evadeColor = new Color(0.6f, 2.5f, 0.8f, 1f);
        [Min(0f)] [SerializeField] float evadeSparkSize = 1f;

        FeedbackEmitter emitter;
        PlayerController controller;
        PlayerAttack attack;
        PlayerHealth health;
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
            if (health != null)
            {
                health.OnDamageTaken += HandleDamageTaken;
                health.OnBoneEarned += HandleBoneEarned;
                health.OnDodgeSucceeded += HandleDodgeSucceeded;
            }
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
            if (health != null)
            {
                health.OnDamageTaken -= HandleDamageTaken;
                health.OnBoneEarned -= HandleBoneEarned;
                health.OnDodgeSucceeded -= HandleDodgeSucceeded;
            }
            if (controller != null)
            {
                controller.OnRollStarted -= HandleRollStarted;
                controller.OnJumped -= HandleJumped;
                controller.OnActionChanged -= HandleActionChanged;
            }
        }

        void HandleSwing()
        {
            bool heavy = attack.IsHeavy || attack.IsExecution;
            if (Data != null) emitter.Play(heavy ? Data.heavySwing : Data.swing, Body);

            float tilt = heavy ? heavySlashTilt : Pick(comboSlashTilt, attack.ComboIndex, 0f);
            float size = heavy ? heavySlashSize : Pick(comboSlashSize, attack.ComboIndex, 1f);
            Quaternion rotation = transform.rotation * Quaternion.Euler(0f, 0f, tilt);
            TimedFx.Spawn(heavy ? heavySlashPrefab : slashPrefab, Body + transform.forward * slashForward, rotation, slashColor, size);
        }

        static float Pick(float[] values, int index, float fallback)
        {
            if (values == null || values.Length == 0) return fallback;
            return values[Mathf.Clamp(index, 0, values.Length - 1)];
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
            }
        }

        void HandleRollStarted(Vector3 direction)
        {
            if (Data != null) emitter.Play(Data.roll, transform.position);
        }

        // PlayerHealth 已經保證同一次翻滾只送一次
        void HandleDodgeSucceeded(DamageInfo info)
        {
            if (Data != null) emitter.Play(Data.evade, Body);
            TimedFx.Spawn(evadeSparkPrefab, Body, Quaternion.identity, evadeColor, evadeSparkSize);
        }

        void HandleJumped()
        {
            if (Data != null) emitter.Play(Data.jump, transform.position);
        }

        void HandleBoneEarned()
        {
            if (Data != null) emitter.Play(Data.boneEarned, Body);
        }

        void HandleActionChanged(PlayerAction action)
        {
            if (action == PlayerAction.Heal && Data != null) emitter.Play(Data.heal, Body);
        }
    }
}
