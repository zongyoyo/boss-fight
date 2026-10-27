using BossFight.Core;
using BossFight.Hazards;
using UnityEngine;

namespace BossFight.Boss
{
    /// <summary>
    /// 招式打到目標的那一刻，在目標身上爆一下火花，顏色看結算結果：命中紅、格擋藍、破防橘、盾反金、無敵閃過綠。
    /// 訂閱 BossController.OnMoveHit 或 TrainingSapling 的攻擊事件；顏色、大小都是純視覺參數。
    /// </summary>
    [DisallowMultipleComponent]
    public class BossHitFeedback : MonoBehaviour
    {
        [SerializeField] BossController boss;
        [Tooltip("小樹精使用同一套命中回饋；和 boss 擇一接線")]
        [SerializeField] TrainingSapling sapling;
        [SerializeField] TimedFx sparkPrefab;
        [Tooltip("火花大小倍率")]
        [Min(0f)] [SerializeField] float size = 1f;
        [Tooltip("火花從受擊範圍中心往攻擊來源偏移的距離 (m)，才不會被角色身體擋住")]
        [Min(0f)] [SerializeField] float towardSource = 0.6f;
        [Tooltip("翻滾移出判定範圍時，火花在玩家根位置上方的高度 (m)")]
        [Min(0f)] [SerializeField] float evadedHeight = 1f;

        [Header("顏色（HDR）")]
        [SerializeField] [ColorUsage(true, true)] Color hitColor = new Color(3f, 0.35f, 0.25f, 1f);
        [SerializeField] [ColorUsage(true, true)] Color blockedColor = new Color(0.6f, 1.4f, 3f, 1f);
        [SerializeField] [ColorUsage(true, true)] Color guardBreakColor = new Color(3f, 1.3f, 0.2f, 1f);
        [SerializeField] [ColorUsage(true, true)] Color parriedColor = new Color(3f, 2.6f, 0.6f, 1f);
        [SerializeField] [ColorUsage(true, true)] Color evadedColor = new Color(0.6f, 2.5f, 0.8f, 1f);

        void OnEnable()
        {
            if (boss != null) boss.OnMoveHit += HandleMoveHit;
            if (sapling != null)
            {
                sapling.OnAttackHit += HandleSaplingHit;
            }
        }

        void OnDisable()
        {
            if (boss != null) boss.OnMoveHit -= HandleMoveHit;
            if (sapling != null)
            {
                sapling.OnAttackHit -= HandleSaplingHit;
            }
        }

        void HandleMoveHit(BossMove move, HitReport report)
        {
            Vector3 pos = report.hurtbox != null ? report.hurtbox.Center : transform.position;
            PlayResult(report.result, pos, report.info.sourcePosition);
        }

        void HandleSaplingHit(int id, HitReport report)
        {
            // 翻滾是否真正成功由持有玩家引用的上層結算；不把強制／受擊無敵畫成成功。
            if (report.result != DamageResult.Evaded) HandleMoveHit(null, report);
        }

        public void PlaySaplingEvade(Vector3 targetPosition)
        {
            PlayResult(DamageResult.Evaded, targetPosition + Vector3.up * evadedHeight, transform.position);
        }

        void PlayResult(DamageResult result, Vector3 pos, Vector3 sourcePosition)
        {
            Color color;
            switch (result)
            {
                case DamageResult.Hit: color = hitColor; break;
                case DamageResult.Blocked: color = blockedColor; break;
                case DamageResult.GuardBroken: color = guardBreakColor; break;
                case DamageResult.Parried: color = parriedColor; break;
                case DamageResult.Evaded: color = evadedColor; break;
                default: return;
            }
            Vector3 toSource = sourcePosition - pos;
            toSource.y = 0f;
            if (toSource.sqrMagnitude > 1e-4f) pos += toSource.normalized * towardSource;
            TimedFx.Spawn(sparkPrefab, pos, Quaternion.identity, color, size);
        }
    }
}
