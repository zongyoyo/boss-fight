using System;
using UnityEngine;

namespace BossFight.Core
{
    /// <summary>難度（ESC 選單「難度」，存在 GameOptions）。困難是企劃書原本的數值；簡單在讀取時加上 DifficultyData.easy 的修正。</summary>
    public enum Difficulty
    {
        Hard,
        Easy,
    }

    /// <summary>
    /// 一個難度對基礎數值（PlayerStats、BossStats、BossMoveData）的修正。不改也不複製 SO：BattleManager 開場前把這一份交給玩家與 Boss，
    /// 各元件在讀數值的時候才加上去，所以 F12 平衡面板調的仍是基礎值。全部是「加多少」或「乘多少」，預設值 = 不修正。
    /// </summary>
    [Serializable]
    public class DifficultyModifiers
    {
        /// <summary>不修正（困難，或沒有接 DifficultyData 時）。</summary>
        public static readonly DifficultyModifiers None = new DifficultyModifiers();

        [Header("刀盾狗")]
        [Tooltip("翻滾無敵時間加多少秒（PlayerStats.rollInvulnerableTime）")]
        public float rollInvulnerableBonus;
        [Tooltip("盾反判定時間加多少秒（PlayerStats.parryWindow）")]
        public float parryWindowBonus;

        [Header("OIIA 貓：失衡值")]
        [Tooltip("失衡值累積的倍率：攻擊、盾反、龍捲風結束等所有來源（BossStagger.AddImbalance）")]
        [Min(0f)] public float imbalanceGainMultiplier = 1f;
        [Tooltip("失衡值衰減速度的倍率（BossStats.imbalanceDecayRate）")]
        [Min(0f)] public float imbalanceDecayMultiplier = 1f;

        [Header("OIIA 貓：招式")]
        [Tooltip("每招開頭的預兆階段加多少秒（BossMoveData.telegraphTime；招式中段另外的預兆不加）")]
        public float telegraphBonus;
        [Tooltip("貓掌連拍、三連拍第一下的蓄力（預兆）加多少秒，取代上面的 telegraphBonus；第二下之後不變")]
        public float pawSwipeFirstStrikeBonus;
        [Tooltip("OIIA 龍捲風追蹤速度的倍率（TornadoData.chaseSpeed）")]
        [Min(0f)] public float tornadoSpeedMultiplier = 1f;
        [Tooltip("雙重衝擊波兩圈之間的間隔加多少秒（SpinShockwaveData.waveInterval）")]
        public float shockwaveIntervalBonus;
    }

    /// <summary>
    /// 各難度的修正值（企劃書「簡單模式」）。資產在 Assets/ScriptableObjects/Options/Difficulty.asset，BattleManager 引用。
    /// </summary>
    [CreateAssetMenu(menuName = "BossFight/Difficulty Data", fileName = "Difficulty")]
    public class DifficultyData : ScriptableObject
    {
        [Tooltip("困難：企劃書原本的數值，預設不修正")]
        public DifficultyModifiers hard = new DifficultyModifiers();
        [Tooltip("簡單")]
        public DifficultyModifiers easy = new DifficultyModifiers
        {
            rollInvulnerableBonus = 0.1f,
            parryWindowBonus = 0.05f,
            imbalanceGainMultiplier = 1.15f,
            imbalanceDecayMultiplier = 0.85f,
            telegraphBonus = 0.15f,
            pawSwipeFirstStrikeBonus = 0.2f,
            tornadoSpeedMultiplier = 0.9f,
            shockwaveIntervalBonus = 0.1f,
        };

        public DifficultyModifiers Get(Difficulty difficulty) => difficulty == Difficulty.Easy ? easy : hard;
    }
}
