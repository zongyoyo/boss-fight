using System;
using System.Collections.Generic;
using UnityEngine;

namespace BossFight.Progression
{
    /// <summary>技能樹的一個節點（T23）：學會後把修正與效果登記到數值管線（SkillTreeLoadout）。</summary>
    [Serializable]
    public class SkillNode
    {
        [Header("識別")]
        [Tooltip("存檔用的 id：不能空、同一棵樹裡不能重複，存檔後不要改（Tools > Progression > Validate Game Database 檢查）")]
        public string id;
        [Tooltip("顯示名稱")]
        public string displayName;
        [Tooltip("說明（營火技能樹頁下方）")]
        [TextArea(2, 4)] public string description;
        [Tooltip("圖示；留空時節點只顯示名稱")]
        public Sprite icon;

        [Header("學習")]
        [Tooltip("花費的技能點")]
        [Min(0)] public int cost = 1;
        [Tooltip("前置節點（同一棵樹的節點 id）：全部學會才能學")]
        public List<string> prerequisites = new List<string>();

        [Header("效果")]
        [Tooltip("數值修正（學會時登記到數值管線）")]
        public List<StatModifier> modifiers = new List<StatModifier>();
        [Tooltip("數值以外的效果")]
        public List<PlayerEffect> effects = new List<PlayerEffect>();

        [Header("介面")]
        [Tooltip("技能樹頁上的格子座標：x = 欄（往右）、y = 列（0 在最上面）")]
        public Vector2Int gridPosition;
    }

    /// <summary>
    /// 一個角色的技能樹（企劃書「技能樹」，T23）：節點清單，CharacterDefinition.skillTree 引用。
    /// 技能點池與各角色學過的節點存在 ProfileStore（只存 id）；學會的節點由 SkillTreeLoadout 登記成 PlayerLoadout 的「skills」來源。
    /// 節點 id 不重複、前置存在且不成環、格子座標不重複由 GameDatabaseValidator 檢查。
    /// </summary>
    [CreateAssetMenu(menuName = "BossFight/Progression/Skill Tree", fileName = "SkillTree")]
    public class SkillTreeData : ScriptableObject
    {
        [Tooltip("節點（介面照 gridPosition 排，前置用線連起來）")]
        public List<SkillNode> nodes = new List<SkillNode>();

        /// <summary>依 id 找節點；找不到（或 id 是空的）時 null。</summary>
        public SkillNode Find(string id)
        {
            if (string.IsNullOrEmpty(id) || nodes == null) return null;
            foreach (var node in nodes)
            {
                if (node != null && node.id == id) return node;
            }
            return null;
        }
    }
}
