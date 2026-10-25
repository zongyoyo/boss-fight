using System.Collections.Generic;
using BossFight.Core;

namespace BossFight.Progression
{
    /// <summary>
    /// 進度的規則（T24，企劃書「村長與任務」「獎勵」）：Boss 擊敗紀錄與首通獎勵、任務狀態、接任務與回報、發獎勵。
    /// 沒有自己的狀態，只照規則讀寫 ProfileStore（存檔只存 id；資產由呼叫方從 GameDatabase 或 Inspector 拿）。
    /// Boss 戰勝利時 BattleManager 呼叫 RecordBossDefeat；休息區的村長（VillageChief）呼叫任務的部分。
    /// </summary>
    public static class ProgressRecorder
    {
        static readonly RewardData[] NoRewards = new RewardData[0];

        // ---------- Boss ----------

        /// <summary>記錄擊敗這隻 Boss 並存檔；第一次擊敗時發首通獎勵，回傳發了哪些（不是首通或沒有 Boss 時是空的）。</summary>
        public static IReadOnlyList<RewardData> RecordBossDefeat(BossDefinition boss)
        {
            if (boss == null || !ProfileStore.RecordBossDefeated(boss.id)) return NoRewards;
            var granted = new List<RewardData>();
            if (boss.firstClearRewards == null) return granted;
            foreach (var reward in boss.firstClearRewards)
            {
                if (Grant(reward)) granted.Add(reward);
            }
            return granted;
        }

        // ---------- 任務 ----------

        /// <summary>這個任務目前的狀態（由存檔算出）。</summary>
        public static QuestState GetState(QuestData quest)
        {
            if (quest == null) return QuestState.Locked;
            if (ProfileStore.IsQuestCompleted(quest.id)) return QuestState.Completed;
            if (ProfileStore.IsQuestAccepted(quest.id)) return ObjectiveMet(quest) ? QuestState.ReadyToTurnIn : QuestState.Active;
            return IsOpen(quest) ? QuestState.Available : QuestState.Locked;
        }

        /// <summary>目標達成了沒有：擊敗 Boss 看存檔的擊敗紀錄（接任務前就打贏也算）。</summary>
        public static bool ObjectiveMet(QuestData quest)
        {
            var objective = quest != null ? quest.objective : null;
            if (objective == null) return false;
            switch (objective.kind)
            {
                case QuestObjectiveKind.DefeatBoss: return objective.boss != null && objective.boss.IsDefeated;
                default: return false;
            }
        }

        /// <summary>接任務並存檔；不是「可接」時不做事、回傳 false。</summary>
        public static bool Accept(QuestData quest)
        {
            if (GetState(quest) != QuestState.Available) return false;
            return ProfileStore.AcceptQuest(quest.id);
        }

        /// <summary>回報任務：標成已完成、發獎勵並存檔，回傳發了哪些；不是「可回報」時不做事、回傳空的。</summary>
        public static IReadOnlyList<RewardData> TurnIn(QuestData quest)
        {
            if (GetState(quest) != QuestState.ReadyToTurnIn || !ProfileStore.CompleteQuest(quest.id)) return NoRewards;
            var granted = new List<RewardData>();
            if (quest.rewards == null) return granted;
            foreach (var reward in quest.rewards)
            {
                if (Grant(reward)) granted.Add(reward);
            }
            return granted;
        }

        // 開放：看過休息區的開場劇情，而且前置任務都完成
        static bool IsOpen(QuestData quest)
        {
            if (!ProfileStore.StoryIntroSeen) return false;
            if (quest.prerequisites == null) return true;
            foreach (var prerequisite in quest.prerequisites)
            {
                if (prerequisite != null && !ProfileStore.IsQuestCompleted(prerequisite.id)) return false;
            }
            return true;
        }

        // ---------- 獎勵 ----------

        /// <summary>發一項獎勵並存檔（技能點加進池子、裝備解鎖並標「新」、角色解鎖）；沒接好時回傳 false。已經解鎖的裝備或角色照樣算發了。</summary>
        public static bool Grant(RewardData reward)
        {
            if (reward == null || !reward.IsValid) return false;
            switch (reward.kind)
            {
                case RewardKind.SkillPoints:
                    ProfileStore.AddSkillPoints(reward.amount);
                    return true;
                case RewardKind.Equipment:
                    ProfileStore.UnlockEquipment(reward.equipment.id);
                    return true;
                case RewardKind.Character:
                    ProfileStore.UnlockCharacter(reward.character.id);
                    return true;
                default:
                    return false;
            }
        }
    }
}
