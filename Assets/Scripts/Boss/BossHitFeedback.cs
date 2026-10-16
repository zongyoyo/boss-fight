using BossFight.Core;
using BossFight.Hazards;
using UnityEngine;

namespace BossFight.Boss
{
    /// <summary>
    /// 招式打到目標的那一刻，在目標身上爆一下火花，顏色看結算結果：命中紅、格擋藍、破防橘、盾反金、無敵閃過綠。
    /// 只訂閱 BossController.OnMoveHit；顏色、大小都是純視覺參數。
    /// </summary>
    [DisallowMultipleComponent]
    public class BossHitFeedback : MonoBehaviour
    {
        [SerializeField] BossController boss;
        [SerializeField] TimedFx sparkPrefab;
        [Tooltip("火花大小倍率")]
        [Min(0f)] [SerializeField] float size = 1f;
        [Tooltip("火花從受擊範圍中心往攻擊來源偏移的距離 (m)，才不會被角色身體擋住")]
        [Min(0f)] [SerializeField] float towardSource = 0.6f;

        [Header("顏色（HDR）")]
        [SerializeField] [ColorUsage(true, true)] Color hitColor = new Color(3f, 0.35f, 0.25f, 1f);
        [SerializeField] [ColorUsage(true, true)] Color blockedColor = new Color(0.6f, 1.4f, 3f, 1f);
        [SerializeField] [ColorUsage(true, true)] Color guardBreakColor = new Color(3f, 1.3f, 0.2f, 1f);
        [SerializeField] [ColorUsage(true, true)] Color parriedColor = new Color(3f, 2.6f, 0.6f, 1f);
        [SerializeField] [ColorUsage(true, true)] Color evadedColor = new Color(0.6f, 2.5f, 0.8f, 1f);

        void OnEnable()
        {
            if (boss != null) boss.OnMoveHit += HandleMoveHit;
        }

        void OnDisable()
        {
            if (boss != null) boss.OnMoveHit -= HandleMoveHit;
        }

        void HandleMoveHit(BossMove move, HitReport report)
        {
            Color color;
            switch (report.result)
            {
                case DamageResult.Hit: color = hitColor; break;
                case DamageResult.Blocked: color = blockedColor; break;
                case DamageResult.GuardBroken: color = guardBreakColor; break;
                case DamageResult.Parried: color = parriedColor; break;
                case DamageResult.Evaded: color = evadedColor; break;
                default: return;
            }
            Vector3 pos = report.hurtbox != null ? report.hurtbox.Center : transform.position;
            Vector3 toSource = report.info.sourcePosition - pos;
            toSource.y = 0f;
            if (toSource.sqrMagnitude > 1e-4f) pos += toSource.normalized * towardSource;
            TimedFx.Spawn(sparkPrefab, pos, Quaternion.identity, color, size);
        }
    }
}
