using UnityEngine;

namespace BossFight.Boss
{
    /// <summary>
    /// OIIA 貓不分階段的數值：血量上限、轉身速度，以及依序排好的階段。BossHealth 與 BossController 都讀這一份。
    /// 執行期只讀不寫；調平衡改 Assets/ScriptableObjects/Boss/BossStats.asset。
    /// </summary>
    [CreateAssetMenu(fileName = "BossStats", menuName = "BossFight/Boss/Boss Stats")]
    public class BossStats : ScriptableObject
    {
        [Tooltip("血量上限")]
        [Min(1f)] public float maxHealth = 2000f;
        [Tooltip("轉向玩家的速度（度/秒）")]
        [Min(0f)] public float turnSpeed = 360f;
        [Tooltip("依血量門檻由高到低排列：第一階段、第二階段、狂暴；第一個是開打時的階段")]
        public BossPhaseData[] phases;
    }
}
