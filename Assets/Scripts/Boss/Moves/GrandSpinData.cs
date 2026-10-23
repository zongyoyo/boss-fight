using UnityEngine;

namespace BossFight.Boss.Moves
{
    /// <summary>
    /// OIIA 大迴旋（終極技，狂暴 BossPhaseData.entryMove）：預兆 telegraphTime 秒浮到法陣（場地中央）上空 floatHeight，
    /// 倒數 countdownTime 秒，期間場上出現 safeZoneCount 個安全區；倒數結束落地，安全區以外全場 damage（不可擋），
    /// 落地後直接頭暈 landingStunTime 秒。浮空到落地期間 Boss 無敵。資產在 Assets/ScriptableObjects/Boss/Moves/Phase2/。
    /// </summary>
    [CreateAssetMenu(fileName = "GrandSpin", menuName = "BossFight/Boss/Moves/Grand Spin")]
    public class GrandSpinData : BossMoveData
    {
        [Header("OIIA 大迴旋")]
        [Tooltip("浮空高度 (m)，腳底離地")]
        [Min(0f)] public float floatHeight = 4f;
        [Tooltip("倒數（秒）：從安全區出現到落地判定")]
        [Min(0.1f)] public float countdownTime = 4f;
        [Tooltip("倒數的最後幾秒從空中砸下來（秒）")]
        [Min(0.01f)] public float fallTime = 0.25f;
        [Tooltip("全場判定的高度 (m)，從地面往上；跳起來也躲不掉")]
        [Min(0.1f)] public float hitHeight = 6f;
        [Tooltip("落地判定的持續時間（秒）")]
        [Min(0.01f)] public float hitWindow = 0.1f;
        [Tooltip("落地後的頭暈時間（秒）：受傷 ×1.5、可以處決")]
        [Min(0f)] public float landingStunTime = 3f;

        [Header("安全區")]
        [Tooltip("安全區數量")]
        [Min(1)] public int safeZoneCount = 2;
        [Tooltip("安全區半徑 (m)；玩家的中心站在圈內才安全")]
        [Min(0.1f)] public float safeZoneRadius = 2f;
        [Tooltip("安全區圓心離場地中心最近幾公尺")]
        [Min(0f)] public float safeZoneMinRadius = 4f;
        [Tooltip("安全區圓心離場地中心最遠幾公尺")]
        [Min(0f)] public float safeZoneMaxRadius = 8.5f;
        [Tooltip("兩個安全區圓心盡量相隔的距離 (m)")]
        [Min(0f)] public float safeZoneMinSpacing = 8f;
    }
}
