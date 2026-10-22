using UnityEngine;

namespace BossFight.Boss.Moves
{
    /// <summary>
    /// OIIA 迴旋衝撞：預兆原地加速旋轉，地上的衝刺路徑線前 trackTime 秒跟著玩家轉、之後鎖定；
    /// 出招時沿路徑直線衝到撞上外牆（ArenaData 的 ArenaWall Layer）為止，穿過玩家不停、整段只打一次。
    /// 撞牆後的硬直是 recoveryTime。格擋時耐力扣除量再乘上 blockStaminaMultiplier（可擋但耗耐力大）。
    /// chargeCount &gt; 1 是第二階段的連環迴旋衝撞：每次撞牆後蓄力 rechargeTime 秒、轉向玩家再衝，最後一次撞牆後才硬直。
    /// 資產在 Assets/ScriptableObjects/Boss/Moves/Phase1/、Phase2/。
    /// </summary>
    [CreateAssetMenu(fileName = "SpinCharge", menuName = "BossFight/Boss/Moves/Spin Charge")]
    public class SpinChargeData : BossMoveData
    {
        [Header("迴旋衝撞")]
        [Tooltip("預兆開始後幾秒內路徑會跟著玩家轉；之後鎖定方向，玩家可以看著鎖定的路徑側閃")]
        [Min(0f)] public float trackTime = 0.7f;
        [Tooltip("衝刺速度 (m/s)")]
        [Min(0.1f)] public float chargeSpeed = 14f;
        [Tooltip("衝刺最長時間（秒）；保險用，正常會先撞到外牆")]
        [Min(0.1f)] public float maxChargeTime = 3f;
        [Tooltip("衝刺判定：以 Boss 為中心的圓柱半徑 (m)")]
        [Min(0f)] public float hitRadius = 1.1f;
        [Tooltip("衝刺判定的高度 (m)，從腳底往上")]
        [Min(0.1f)] public float hitHeight = 2.2f;
        [Tooltip("地上衝刺路徑的寬度 (m)；和判定直徑一樣才不會誤導玩家")]
        [Min(0.1f)] public float pathWidth = 2.2f;
        [Tooltip("被格擋時耐力扣除量的倍率（一般攻擊是 1）")]
        [Min(0f)] public float blockStaminaMultiplier = 2f;

        [Header("連環衝撞（第二階段）")]
        [Tooltip("連撞幾次：撞牆後重新蓄力、轉向玩家再衝；recoveryTime 是最後一次撞牆後的硬直")]
        [Min(1)] public int chargeCount = 1;
        [Tooltip("撞牆後到下一次衝刺的蓄力時間（秒），期間地上重新出現衝刺路徑並閃預兆")]
        [Min(0f)] public float rechargeTime = 0.6f;
        [Tooltip("蓄力開始後幾秒內路徑跟著玩家轉，之後鎖定")]
        [Min(0f)] public float rechargeTrackTime = 0.3f;
    }
}
