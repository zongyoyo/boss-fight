using UnityEngine;

namespace BossFight.Boss.Moves
{
    /// <summary>
    /// T3 驗證 Boss 循環用的假招式數值：共通欄位之外，加上判定持續時間與以 Boss 為中心的球形範圍。
    /// 資產在 Assets/ScriptableObjects/Boss/Moves/Test/。
    /// </summary>
    [CreateAssetMenu(fileName = "TestFlashMove", menuName = "BossFight/Boss/Moves/Test Flash Move")]
    public class TestFlashMoveData : BossMoveData
    {
        [Header("假招式")]
        [Tooltip("攻擊判定持續時間（秒）")]
        [Min(0.01f)] public float activeTime = 0.2f;
        [Tooltip("以 Boss 為中心的球形判定半徑 (m)")]
        [Min(0f)] public float hitRadius = 2.5f;
        [Tooltip("球心離地高度 (m)")]
        [Min(0f)] public float hitHeight = 0.8f;
    }
}
