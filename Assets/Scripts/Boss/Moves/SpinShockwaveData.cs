using UnityEngine;

namespace BossFight.Boss.Moves
{
    /// <summary>
    /// 旋轉衝擊波：預兆原地高速旋轉，出招時放出環狀衝擊波（Hazards/ShockwaveRing），波自己往外擴散；
    /// Boss 放完波 spinOutTime 秒後進入破綻。不可擋，要翻滾穿過環，或抓準時機跳過（判定高度比跳躍高度低）。
    /// waveCount &gt; 1 是第二階段的雙重衝擊波：每隔 waveInterval 秒再放一圈，放完最後一圈才收轉。
    /// 資產在 Assets/ScriptableObjects/Boss/Moves/Phase1/、Phase2/。
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
        [Tooltip("判定高度 (m)，從地面往上；要比 PlayerStats.jumpHeight 低才跳得過，差越多越好跳（0.8 對跳躍 1.2：腳在波上方約 0.36 秒，波經過約 0.21 秒）")]
        [Min(0.1f)] public float hitHeight = 0.8f;
        [Tooltip("預兆時地上提示圈的半徑 (m)：填滿的那一刻放出波")]
        [Min(0.1f)] public float telegraphRadius = 2.8f;

        [Header("多重衝擊波（第二階段）")]
        [Tooltip("放幾圈")]
        [Min(1)] public int waveCount = 1;
        [Tooltip("兩圈之間相隔幾秒")]
        [Min(0.05f)] public float waveInterval = 0.6f;
    }
}
