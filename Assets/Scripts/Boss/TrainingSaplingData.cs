using UnityEngine;

namespace BossFight.Boss
{
    /// <summary>教學與呼喚孩子共用的樹精設定；執行期只讀，血量與計時存在各實例。</summary>
    [CreateAssetMenu(fileName = "TrainingSapling", menuName = "BossFight/Prologue/Training Sapling")]
    public class TrainingSaplingData : ScriptableObject
    {
        [Header("血量與初始模式")]
        [Tooltip("顯示名稱")] public string displayName = "小樹精";
        [Tooltip("血量上限")] [Min(1f)] public float maxHealth = 30f;
        [Tooltip("標靶模式的血量下限")] [Min(0f)] public float targetHealthFloor = 1f;
        [Tooltip("冒出後的模式")] public SaplingMode initialMode = SaplingMode.Idle;
        [Tooltip("冒出後是否無敵")] public bool initiallyInvulnerable = true;
        [Tooltip("是否累積玩家攻擊及盾反的失衡值；教學前四步關閉")] public bool initiallyAccumulateStagger;
        [Tooltip("存活時間（秒）；0 = 不自動消失")] [Min(0f)] public float lifetime;

        [Header("樹枝攻擊")]
        [Tooltip("原始傷害；教學為 0")] [Min(0f)] public float damage;
        [Tooltip("相鄰預兆開始的間隔，已包含預兆及判定窗（秒）")] [Min(0f)] public float attackPeriod = 2.5f;
        [Tooltip("拉枝、白光預兆（秒）")] [Min(0f)] public float telegraphTime = 1f;
        [Tooltip("判定持續時間（秒）")] [Min(0f)] public float activeTime = 0.15f;
        [Tooltip("前方攻擊長度 (m)")] [Min(0f)] public float reach = 2.5f;
        [Tooltip("攻擊寬度 (m)")] [Min(0f)] public float width = 1.5f;
        [Tooltip("攻擊高度 (m)")] [Min(0f)] public float height = 1.2f;
        [Tooltip("待機與預兆的轉向速度（度/秒）")] [Min(0f)] public float turnSpeed = 180f;
        [Tooltip("預兆開始後還會轉向追玩家的時間（秒）；之後鎖住方向，判定照鎖住的方向打。0 = 預兆一開始就鎖")] [Min(0f)] public float telegraphTrackTime = 0.5f;
        [Tooltip("可用盾擋下")] public bool canBlock = true;
        [Tooltip("可盾反")] public bool canParry = true;

        [Header("實心碰撞（PrologueActorsBuilder 寫進小樹精的 CapsuleCollider）")]
        [Tooltip("身體碰撞半徑 (m)：擋住玩家，攻擊與處決時不會重疊")] [Min(0.01f)] public float bodyRadius = 0.35f;
        [Tooltip("身體碰撞高度 (m)")] [Min(0.01f)] public float bodyHeight = 1.3f;

        [Header("鎖定鏡頭（純視覺，PrologueActorsBuilder 寫進小樹精的 LockOnTarget）")]
        [Tooltip("鏡頭看的點的高度 (m)：身體中心")] [Min(0f)] public float lockAimHeight = 0.45f;
        [Tooltip("鎖定鏡頭框住小樹精用的半徑 (m)")] [Min(0f)] public float lockFramingRadius = 0.6f;
        [Tooltip("鎖定小樹精時的俯視角度（度）；T27 驗收的 32°")] [Range(-10f, 60f)] public float lockOnPitch = 32f;
        [Tooltip("鎖定小樹精時鏡頭離玩家的距離 (m)；0 = 鎖定鏡頭原本的距離")] [Min(0f)] public float lockOnDistance;

        [Header("表情動作（純表現）")]
        [Tooltip("揮手、害怕等動作的長度（秒）")] [Min(0.01f)] public float gestureTime = 0.9f;

        [Header("軀幹")]
        [Tooltip("軀幹上限；滿值進入永久頭暈")] [Min(1f)] public float staggerMax = 100f;
        [Tooltip("未增加多久後開始衰減（秒）")] [Min(0f)] public float staggerDecayDelay = 3f;
        [Tooltip("每秒衰減的軀幹值")] [Min(0f)] public float staggerDecayRate = 8f;
    }
}
