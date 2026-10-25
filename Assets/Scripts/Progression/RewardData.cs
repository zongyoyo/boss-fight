using System;
using UnityEngine;

namespace BossFight.Progression
{
    /// <summary>獎勵的種類（企劃書「獎勵」）。</summary>
    public enum RewardKind
    {
        /// <summary>技能點：amount 點加進全帳號共用的池子。</summary>
        SkillPoints,
        /// <summary>解鎖裝備（equipment），鐵匠鋪標「新」。</summary>
        Equipment,
        /// <summary>解鎖角色（character），營火清單多一列。</summary>
        Character,
    }

    /// <summary>
    /// 一項獎勵（T24）：種類＋數量或引用。Boss 的首通獎勵（BossDefinition.firstClearRewards）與任務獎勵（QuestData.rewards）都用它，
    /// 由 ProgressRecorder.Grant 發給存檔。
    /// </summary>
    [Serializable]
    public class RewardData
    {
        [Tooltip("種類")]
        public RewardKind kind;
        [Tooltip("技能點的數量（種類是技能點時）")]
        [Min(1)] public int amount = 1;
        [Tooltip("解鎖的裝備（種類是裝備時）")]
        public EquipmentData equipment;
        [Tooltip("解鎖的角色（種類是角色時）")]
        public CharacterDefinition character;

        /// <summary>技能點獎勵。</summary>
        public static RewardData SkillPoints(int amount) => new RewardData { kind = RewardKind.SkillPoints, amount = amount };

        /// <summary>解鎖裝備的獎勵。</summary>
        public static RewardData Unlock(EquipmentData equipment) => new RewardData { kind = RewardKind.Equipment, equipment = equipment };

        /// <summary>解鎖角色的獎勵。</summary>
        public static RewardData Unlock(CharacterDefinition character) => new RewardData { kind = RewardKind.Character, character = character };

        /// <summary>引用的資產有沒有接好（技能點不用）。</summary>
        public bool IsValid => kind switch
        {
            RewardKind.SkillPoints => amount > 0,
            RewardKind.Equipment => equipment != null,
            RewardKind.Character => character != null,
            _ => false,
        };

        /// <summary>顯示用的一段字：技能點照 skillPointsFormat（{0} = 點數），裝備與角色是名稱；沒接好時空字串。</summary>
        public string Describe(string skillPointsFormat)
        {
            switch (kind)
            {
                case RewardKind.SkillPoints: return string.Format(skillPointsFormat ?? "{0}", amount);
                case RewardKind.Equipment: return equipment != null ? equipment.displayName : string.Empty;
                case RewardKind.Character: return character != null ? character.displayName : string.Empty;
                default: return string.Empty;
            }
        }
    }
}
