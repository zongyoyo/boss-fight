using UnityEngine;

namespace BossFight.Boss
{
    /// <summary>
    /// OIIA 貓不分階段的數值：血量上限、轉身速度、失衡值的衰減與頭暈的受傷倍率，以及依序排好的階段。
    /// BossHealth、BossController、BossStagger 都讀這一份；失衡值上限與頭暈時間每個階段不同，在 BossPhaseData。
    /// 執行期只讀不寫；調平衡改 Assets/ScriptableObjects/Boss/BossStats.asset。
    /// </summary>
    [CreateAssetMenu(fileName = "BossStats", menuName = "BossFight/Boss/Boss Stats")]
    public class BossStats : ScriptableObject
    {
        [Tooltip("血量上限")]
        [Min(1f)] public float maxHealth = 2000f;
        [Tooltip("轉向玩家的速度（度/秒）")]
        [Min(0f)] public float turnSpeed = 360f;

        [Header("失衡值")]
        [Tooltip("最後一次增加失衡值後，過了這麼多秒（沒有再增加）才開始衰減")]
        [Min(0f)] public float imbalanceDecayDelay = 3f;
        [Tooltip("衰減速度（每秒減少的失衡值）")]
        [Min(0f)] public float imbalanceDecayRate = 8f;
        [Tooltip("頭暈中受到的傷害倍率；處決是固定傷害，不吃這個倍率")]
        [Min(0f)] public float stunDamageMultiplier = 1.5f;

        [Header("階段")]
        [Tooltip("依血量門檻由高到低排列：第一階段、第二階段、狂暴；第一個是開打時的階段")]
        public BossPhaseData[] phases;
    }
}
