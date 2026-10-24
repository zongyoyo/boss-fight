using BossFight.Player;
using UnityEngine;

namespace BossFight.Progression
{
    /// <summary>
    /// PlayerEffect 掛在角色根物件上的小元件（數值以外的特殊規則，例如盾反成功回血）。
    /// 由 PlayerEffect.Attach 加上，PlayerLoadout 重新套用時拆掉，角色銷毀時一起消失。
    /// 子類別照一般規則在 OnEnable 訂閱玩家事件（Rig 在 Awake 就拿到）、OnDisable 退訂；參數從 Effect 讀（Attach 之後、第一個事件之前就設好）。
    /// </summary>
    public abstract class PlayerEffectBehaviour : MonoBehaviour
    {
        /// <summary>產生這個元件的效果（參數在這裡）。</summary>
        public PlayerEffect Effect { get; private set; }
        /// <summary>掛在哪一隻角色上。</summary>
        protected PlayerRig Rig { get; private set; }

        protected virtual void Awake()
        {
            Rig = GetComponent<PlayerRig>();
        }

        internal void SetEffect(PlayerEffect effect) => Effect = effect;
    }
}
