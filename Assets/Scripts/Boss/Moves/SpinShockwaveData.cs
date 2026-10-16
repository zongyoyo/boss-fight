using UnityEngine;

namespace BossFight.Boss.Moves
{
    /// <summary>
    /// 旋轉衝擊波：預兆原地高速旋轉，出招時放出環狀衝擊波（Hazards/ShockwaveRing），波自己往外擴散；
    /// Boss 放完波 spinOutTime 秒後進入破綻。不可擋，要翻滾穿過環。判定高度比跳躍高，跳不過。
    /// 資產在 Assets/ScriptableObjects/Boss/Moves/Phase1/。
    /// </summary>
    [CreateAssetMenu(fileName = "SpinShockwave", menuName = "BossFight/Boss/Moves/Spin Shockwave")]
    public class SpinShockwaveData : BossMoveData
    {
        [Header("旋轉衝擊波")]
        [Tooltip("放出波之後 Boss 還在收轉的時間（秒），結束後進入破綻")]
        [Min(0f)] public float spinOutTime = 0.2f;
        [Tooltip("波出現時的半徑 (m)")]
        [Min(0f)] public float startRadius = 1.2f;
        [Tooltip("波擴散到的半徑 (m)；0 = 擴散到外牆")]
        [Min(0f)] public float maxRadius;
        [Tooltip("擴散速度 (m/s)")]
        [Min(0.1f)] public float expandSpeed = 8f;
        [Tooltip("環的寬度 (m)")]
        [Min(0.05f)] public float bandWidth = 0.8f;
        [Tooltip("判定高度 (m)，從地面往上；比玩家跳躍高就跳不過")]
        [Min(0.1f)] public float hitHeight = 1.2f;
        [Tooltip("預兆時地上提示圈的半徑 (m)：填滿的那一刻放出波")]
        [Min(0.1f)] public float telegraphRadius = 2.8f;
    }
}
