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
        [Tooltip("可用盾擋下")] public bool canBlock = true;
        [Tooltip("可盾反")] public bool canParry = true;

        [Header("軀幹")]
        [Tooltip("軀幹上限；滿值進入永久頭暈")] [Min(1f)] public float staggerMax = 100f;
        [Tooltip("未增加多久後開始衰減（秒）")] [Min(0f)] public float staggerDecayDelay = 3f;
        [Tooltip("每秒衰減的軀幹值")] [Min(0f)] public float staggerDecayRate = 8f;
    }
}
