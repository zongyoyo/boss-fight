using UnityEngine;

namespace BossFight.Progression
{
    /// <summary>
    /// 效果「盾反成功回血」（T22 反擊護符）：盾反成功（PlayerHealth.OnParry）時回 healAmount HP。
    /// 資產在 ScriptableObjects/Progression/Equipment/ParryHeal.asset。
    /// </summary>
    [CreateAssetMenu(menuName = "BossFight/Progression/Effects/Parry Heal", fileName = "ParryHeal")]
    public class ParryHealEffect : PlayerEffect<ParryHealBehaviour>
    {
        [Tooltip("每次盾反成功回復的 HP")]
        [Min(0f)] public float healAmount = 5f;
    }
}
