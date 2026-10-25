using BossFight.Core;
using BossFight.Player;

namespace BossFight.Progression
{
    /// <summary>ParryHealEffect 掛在角色上的元件：訂閱 PlayerHealth.OnParry，盾反成功時回血（PlayerHealth.Heal，倒下時不回）。</summary>
    public class ParryHealBehaviour : PlayerEffectBehaviour
    {
        PlayerHealth health;

        void OnEnable()
        {
            health = Rig != null ? Rig.Health : null;
            if (health != null) health.OnParry += HandleParry;
        }

        void OnDisable()
        {
            if (health != null) health.OnParry -= HandleParry;
            health = null;
        }

        void HandleParry(DamageInfo info, float imbalance)
        {
            if (Effect is ParryHealEffect effect && health != null) health.Heal(effect.healAmount);
        }
    }
}
