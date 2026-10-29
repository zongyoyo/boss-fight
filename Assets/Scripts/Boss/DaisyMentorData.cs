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
        [Tooltip("丟骨頭：骨頭從花瓣飛到玩家身上的時間（秒）；飛到才給骨頭")] [Min(0f)] public float tossTime = 0.6f;
        [Tooltip("丟骨頭的拋物線高度 (m)")] [Min(0f)] public float tossHeight = 1.2f;
        [Tooltip("土堆動一下：冒出一點再縮回的時間（秒）")] [Min(0f)] public float peekTime = 0.35f;
        [Tooltip("土堆動一下時冒出的比例（0 = 不露出、1 = 整朵冒出）")] [Range(0f, 1f)] public float peekHeight = 0.25f;

        [Header("說話時的動作（純表現）")]
        [Tooltip("一般動作（歪頭、拍胸、點頭、拍手、竊笑、驚訝、揮手、指向）的長度（秒）")] [Min(0.01f)] public float gestureTime = 0.8f;
        [Tooltip("轉一圈的長度（秒）")] [Min(0.01f)] public float spinTime = 0.6f;
        [Tooltip("歪頭的角度（度）")] public float tiltAngle = 25f;
        [Tooltip("點頭的角度（度）")] public float nodAngle = 20f;
        [Tooltip("指向時花瓣手伸出的距離 (m，模型縮放前)")] [Min(0f)] public float pointReach = 0.2f;
        [Tooltip("驚訝時往上跳的高度 (m)")] [Min(0f)] public float surpriseJump = 0.15f;
        [Tooltip("笑臉咧開時笑臉的放大倍率")] [Min(1f)] public float grinScale = 1.35f;

        [Header("藤蔓的劇情傷害")]
        [Tooltip("固定扣血，不受格擋或無敵影響")] [Min(0f)] public float whipDamage = 40f;
        [Tooltip("擊退距離 (m)")] [Min(0f)] public float pushDistance = 1f;
        [Tooltip("擊退持續（秒）")] [Min(0f)] public float pushDuration = 0.25f;
        [Tooltip("判定持續（秒）")] [Min(0f)] public float hitWindow = 0.15f;
        [Tooltip("判定球半徑 (m)")] [Min(0f)] public float hitRadius = 0.4f;
        [Tooltip("玩家根物件到受擊中心的高度 (m)")] [Min(0f)] public float targetHeight = 0.7f;
    }
}
