using UnityEngine;
namespace BossFight.Boss.Moves
{
    /// <summary>守衛RootRing的數值資產；執行期唯讀。</summary>
    [CreateAssetMenu(menuName = "BossFight/Boss/Guardian/RootRing", fileName = "RootRing")]
    public class RootRingData : GuardianMoveData
    {
        [Header("根刺（試玩回饋：一根刺一段判定，刺與刺之間可以站）")]
        [Min(1), Tooltip("每一圈的根刺數；整招角度固定，每根刺沿著從守衛往外的直線推出去")] public int spikeCount = 24;
        [Min(0.01f), Tooltip("每根刺的半徑 (m)：判定沿弧長的半寬，也是刺的外觀底部半徑")] public float spikeRadius = 0.3f;
        [Range(0f, 1f), Tooltip("相鄰兩圈錯開的比例（1 = 一整格）；0.5 = 下一圈的刺在上一圈兩根刺正中間")] public float ringStagger = 0.5f;
    }
}
