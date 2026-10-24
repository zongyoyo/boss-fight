#if UNITY_EDITOR || DEVELOPMENT_BUILD || BOSSFIGHT_DEV_TOOLS
using System.Collections.Generic;
using BossFight.Progression;
using UnityEngine;

namespace BossFight.DebugTools
{
    /// <summary>
    /// T19 驗證數值管線用的測試修正：F12 平衡面板「玩家」頁的開關把它登記成 PlayerLoadout 的一個來源，再重新套用。
    /// 資產在 Assets/ScriptableObjects/Debug/TestModifiers.asset（BalancePanelBuilder 在不存在時建立）。
    /// </summary>
    [CreateAssetMenu(fileName = "TestModifiers", menuName = "BossFight/Debug/Test Modifiers")]
    public class TestModifierData : ScriptableObject
    {
        [Tooltip("面板上開關的說明")]
        public string label = "血量 +20、跑速 ×1.2";
        [Tooltip("開關打開時套用的數值修正")]
        public List<StatModifier> modifiers = new List<StatModifier>
        {
            new StatModifier(PlayerStatId.MaxHealth, StatModifierOp.Add, 20f),
            new StatModifier(PlayerStatId.MoveSpeed, StatModifierOp.Multiply, 0.2f),
        };
        [Tooltip("開關打開時掛上的效果")]
        public List<PlayerEffect> effects = new List<PlayerEffect>();
    }
}
#endif
