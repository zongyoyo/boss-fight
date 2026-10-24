using BossFight.Player;
using UnityEngine;

namespace BossFight.Progression
{
    /// <summary>
    /// 裝備與技能的「效果」：數值以外的特殊規則（企劃書「數值套用管線」）。SO 只放參數，
    /// 執行時 PlayerLoadout 呼叫 Attach 在角色上掛一個 PlayerEffectBehaviour，由它訂閱玩家事件；重新套用時拆掉重掛，角色銷毀時一起消失。
    /// 新效果：繼承 PlayerEffect&lt;TBehaviour&gt;（參數寫在 SO 上），TBehaviour 繼承 PlayerEffectBehaviour。
    /// </summary>
    public abstract class PlayerEffect : ScriptableObject
    {
        [Tooltip("顯示名稱（裝備、技能說明用）")]
        public string displayName;

        /// <summary>在角色上掛這個效果的元件。</summary>
        public abstract PlayerEffectBehaviour Attach(PlayerRig rig);
    }

    /// <summary>掛 TBehaviour 的效果；子類別只要宣告參數。</summary>
    public abstract class PlayerEffect<TBehaviour> : PlayerEffect where TBehaviour : PlayerEffectBehaviour
    {
        public override PlayerEffectBehaviour Attach(PlayerRig rig)
        {
            if (rig == null) return null;
            var behaviour = rig.gameObject.AddComponent<TBehaviour>();
            behaviour.SetEffect(this);
            return behaviour;
        }
    }
}
