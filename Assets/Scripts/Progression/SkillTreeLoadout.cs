using System.Collections.Generic;
using BossFight.Core;

namespace BossFight.Progression
{
    /// <summary>節點對目前角色的狀態（T23）。</summary>
    public enum SkillNodeState
    {
        /// <summary>學過了。</summary>
        Learned,
        /// <summary>前置都學了、點數也夠，可以學。</summary>
        Available,
        /// <summary>還有前置節點沒學。</summary>
        MissingPrerequisite,
        /// <summary>前置都學了，但技能點不夠。</summary>
        NotEnoughPoints,
    }

    /// <summary>
    /// 存檔的技能 → 數值管線（T23）：ProfileStore 依角色記學過的節點 id，這裡用角色的 SkillTreeData 換回節點，
    /// 只算目前樹裡還有的節點（存檔本身不改；前置不再檢查，學的時候檢查過了）。
    /// PlayerSpawner 生成或換角色時呼叫 Register，把學過的節點的修正與效果登記成 PlayerLoadout 的「skills」來源；營火學習、重置後也是。
    /// 學習與重置的規則（前置全部學會、點數夠才能學；重置退回付過的點數）也在這裡，營火只負責介面與重新套用。
    /// </summary>
    public static class SkillTreeLoadout
    {
        /// <summary>PlayerLoadout 裡技能來源的 key。</summary>
        public const string SourceKey = "skills";

        /// <summary>這個角色學過這個節點沒有。</summary>
        public static bool IsLearned(CharacterDefinition character, SkillNode node) =>
            character != null && node != null && ProfileStore.IsSkillLearned(character.id, node.id);

        /// <summary>這個節點對這個角色的狀態。</summary>
        public static SkillNodeState State(CharacterDefinition character, SkillNode node)
        {
            if (IsLearned(character, node)) return SkillNodeState.Learned;
            if (MissingPrerequisites(character, node, null) > 0) return SkillNodeState.MissingPrerequisite;
            return ProfileStore.SkillPoints >= node.cost ? SkillNodeState.Available : SkillNodeState.NotEnoughPoints;
        }

        /// <summary>還沒學的前置節點數；missing 不是 null 時把它們加進去（介面列「需要先學」）。前置 id 在樹裡找不到時也算沒學。</summary>
        public static int MissingPrerequisites(CharacterDefinition character, SkillNode node, List<SkillNode> missing)
        {
            if (character == null || node == null || node.prerequisites == null) return 0;
            var tree = character.skillTree;
            int count = 0;
            foreach (var id in node.prerequisites)
            {
                if (string.IsNullOrEmpty(id)) continue;
                var prerequisite = tree != null ? tree.Find(id) : null;
                if (prerequisite != null && IsLearned(character, prerequisite)) continue;
                count++;
                if (missing != null && prerequisite != null) missing.Add(prerequisite);
            }
            return count;
        }

        /// <summary>學這個節點（扣池子的點數並存檔）；不是 Available 或不在這個角色的樹裡時不做事、回傳 false。要再重新登記與套用才生效。</summary>
        public static bool Learn(CharacterDefinition character, SkillNode node)
        {
            if (character == null || node == null || character.skillTree == null || !character.skillTree.nodes.Contains(node)) return false;
            if (State(character, node) != SkillNodeState.Available) return false;
            return ProfileStore.LearnSkill(character.id, node.id, node.cost);
        }

        /// <summary>重置這個角色的技能：學過的全部清掉，付過的點數退回池子；回傳退回的點數。要再重新登記與套用才生效。</summary>
        public static int Reset(CharacterDefinition character) => character != null ? ProfileStore.ResetSkills(character.id) : 0;

        /// <summary>收集這個角色學過的節點的修正與效果；extra 不是 null 時把它也算成學過（差異預覽用）。</summary>
        public static void Collect(CharacterDefinition character, List<StatModifier> modifiers, List<PlayerEffect> effects, SkillNode extra = null)
        {
            var tree = character != null ? character.skillTree : null;
            if (tree == null || tree.nodes == null) return;
            foreach (var node in tree.nodes)
            {
                if (node == null || (node != extra && !IsLearned(character, node))) continue;
                if (modifiers != null && node.modifiers != null) modifiers.AddRange(node.modifiers);
                if (effects != null && node.effects != null)
                {
                    foreach (var effect in node.effects)
                    {
                        if (effect != null) effects.Add(effect);
                    }
                }
            }
        }

        /// <summary>把這個角色學過的技能登記成 loadout 的技能來源（要再 Apply／ReapplyStats 才生效）。</summary>
        public static void Register(PlayerLoadout loadout, CharacterDefinition character)
        {
            if (loadout == null) return;
            var modifiers = new List<StatModifier>();
            var effects = new List<PlayerEffect>();
            Collect(character, modifiers, effects);
            if (modifiers.Count + effects.Count == 0) loadout.ClearSource(SourceKey);
            else loadout.SetSource(SourceKey, modifiers, effects);
        }
    }
}
