using System;
using UnityEngine;

namespace BossFight.Progression
{
    /// <summary>StatModifier 的運算。</summary>
    public enum StatModifierOp
    {
        /// <summary>加在基礎值上（可以是負的，例如翻滾耐力消耗 −5）。</summary>
        Add,
        /// <summary>倍率：value 是增加的比例，0.2 = ×1.2；同一項的倍率先相加再一次乘（+0.1 與 +0.2 = ×1.3）。</summary>
        Multiply,
    }

    /// <summary>
    /// 一條數值修正（角色、裝備、技能共用）：哪一項、加值還是倍率、多少。由 PlayerLoadout 套用在 PlayerStats 的複本上，不寫入資產。
    /// </summary>
    [Serializable]
    public struct StatModifier
    {
        [Tooltip("要改的數值")]
        public PlayerStatId stat;
        [Tooltip("加值：加在基礎值上；倍率：增加的比例（0.2 = ×1.2），同一項的倍率相加後一次乘")]
        public StatModifierOp op;
        [Tooltip("加值的量，或倍率增加的比例")]
        public float value;

        public StatModifier(PlayerStatId stat, StatModifierOp op, float value)
        {
            this.stat = stat;
            this.op = op;
            this.value = value;
        }
    }
}
