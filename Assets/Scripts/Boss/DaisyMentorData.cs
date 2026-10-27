using UnityEngine;

namespace BossFight.Boss
{
    /// <summary>菊花的劇情攻擊與動作時間；不在執行期改寫。</summary>
    [CreateAssetMenu(fileName = "DaisyMentor", menuName = "BossFight/Prologue/Daisy Mentor")]
    public class DaisyMentorData : ScriptableObject
    {
        [Header("動作")]
        [Tooltip("冒出時間（秒）")] [Min(0f)] public float emergeTime = 0.5f;
        [Tooltip("鑽回時間（秒）")] [Min(0f)] public float burrowTime = 0.4f;
        [Tooltip("面向玩家的速度（度/秒）")] [Min(0f)] public float turnSpeed = 180f;
        [Tooltip("抽打動作總長（秒）")] [Min(0f)] public float whipTime = 0.7f;
        [Tooltip("抽打命中時刻（秒）")] [Min(0f)] public float whipImpactTime = 0.35f;
        [Tooltip("巴掌動作總長（秒）")] [Min(0f)] public float slapTime = 0.6f;
        [Tooltip("巴掌打滿軀幹的時刻（秒）")] [Min(0f)] public float slapImpactTime = 0.3f;

        [Header("藤蔓的劇情傷害")]
        [Tooltip("固定扣血，不受格擋或無敵影響")] [Min(0f)] public float whipDamage = 40f;
        [Tooltip("擊退距離 (m)")] [Min(0f)] public float pushDistance = 1f;
        [Tooltip("擊退持續（秒）")] [Min(0f)] public float pushDuration = 0.25f;
        [Tooltip("判定持續（秒）")] [Min(0f)] public float hitWindow = 0.15f;
        [Tooltip("判定球半徑 (m)")] [Min(0f)] public float hitRadius = 0.4f;
        [Tooltip("玩家根物件到受擊中心的高度 (m)")] [Min(0f)] public float targetHeight = 0.7f;
    }
}
