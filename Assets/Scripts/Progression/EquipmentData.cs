using System.Collections.Generic;
using BossFight.Core;
using UnityEngine;

namespace BossFight.Progression
{
    /// <summary>
    /// 一件裝備（企劃書「裝備系統」）：id、名稱、說明、圖示、欄位種類、數值修正（StatModifier）與效果（PlayerEffect）、可裝備的角色。
    /// 解鎖狀態與各角色的穿戴存在 ProfileStore（只存 id），GameDatabase.equipment 把 id 換回這份資產。
    /// 穿上後由 PlayerSpawner 把目前角色所有欄位的修正與效果登記成 PlayerLoadout 的「equipment」來源（EquipmentLoadout）。
    /// </summary>
    [CreateAssetMenu(menuName = "BossFight/Progression/Equipment", fileName = "Equipment")]
    public class EquipmentData : ScriptableObject
    {
        [Header("識別")]
        [Tooltip("存檔用的 id：不能空、不能跟其他裝備重複，存檔後不要改（Tools > Progression > Validate Game Database 檢查）")]
        public string id;
        [Tooltip("顯示名稱")]
        public string displayName;
        [Tooltip("說明（鐵匠鋪下方）")]
        [TextArea(2, 4)] public string description;
        [Tooltip("圖示；未解鎖時顯示成剪影")]
        public Sprite icon;
        [Tooltip("未解鎖時的來源提示（例如「擊敗 OIIA 貓」）")]
        public string sourceHint;

        [Header("裝備")]
        [Tooltip("放在哪一種欄位")]
        public EquipmentSlotType slotType;
        [Tooltip("可以裝備的角色；空的 = 全部")]
        public List<CharacterDefinition> allowedCharacters = new List<CharacterDefinition>();

        [Header("效果")]
        [Tooltip("數值修正（穿上時登記到數值管線）")]
        public List<StatModifier> modifiers = new List<StatModifier>();
        [Tooltip("數值以外的效果（例如盾反回血）")]
        public List<PlayerEffect> effects = new List<PlayerEffect>();

        /// <summary>存檔裡解鎖了沒有。</summary>
        public bool IsUnlocked => ProfileStore.IsEquipmentUnlocked(id);

        /// <summary>這個角色能不能穿（只看可裝備角色，不看有沒有對應的欄位）。</summary>
        public bool AllowedFor(CharacterDefinition character)
        {
            if (allowedCharacters == null || allowedCharacters.Count == 0) return true;
            return character != null && allowedCharacters.Contains(character);
        }
    }
}
