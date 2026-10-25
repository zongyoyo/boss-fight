using System.Collections.Generic;
using BossFight.Core;

namespace BossFight.Progression
{
    /// <summary>
    /// 存檔的穿戴 → 數值管線（T22）：ProfileStore 依角色、欄位順序記裝備 id，這裡用 GameDatabase 換回 EquipmentData，
    /// 只算「有效」的那幾件（解鎖了、欄位種類相符、角色可以穿），存檔本身不改（之後資料修好了又會生效）。
    /// PlayerSpawner 生成或換角色時呼叫 Register，把所有欄位的修正與效果登記成 PlayerLoadout 的「equipment」來源；鐵匠鋪換裝後也是。
    /// </summary>
    public static class EquipmentLoadout
    {
        /// <summary>PlayerLoadout 裡裝備來源的 key。</summary>
        public const string SourceKey = "equipment";

        /// <summary>這件裝備能不能放進這個角色的第 slot 格（解鎖、欄位種類、可裝備角色）。</summary>
        public static bool Fits(EquipmentData item, CharacterDefinition character, int slot)
        {
            if (item == null || character == null || character.equipmentSlots == null) return false;
            if (slot < 0 || slot >= character.equipmentSlots.Length) return false;
            var type = character.equipmentSlots[slot];
            return type != null && item.slotType == type && item.IsUnlocked && item.AllowedFor(character);
        }

        /// <summary>這個角色有沒有放得下這件裝備的欄位（不看解鎖）。</summary>
        public static bool HasSlotFor(EquipmentData item, CharacterDefinition character)
        {
            if (item == null || character == null || character.equipmentSlots == null || !item.AllowedFor(character)) return false;
            foreach (var type in character.equipmentSlots)
            {
                if (type != null && type == item.slotType) return true;
            }
            return false;
        }

        /// <summary>這個角色第 slot 格實際有效的裝備；沒穿或存檔的那件現在不能穿時 null。</summary>
        public static EquipmentData GetEquipped(GameDatabase database, CharacterDefinition character, int slot)
        {
            if (database == null || character == null) return null;
            var item = database.FindEquipment(ProfileStore.GetEquipped(character.id, slot));
            return Fits(item, character, slot) ? item : null;
        }

        /// <summary>
        /// 收集這個角色所有欄位的修正與效果；overrideSlot ≥ 0 時那一格改看 overrideItem（null = 空著），給差異預覽用。
        /// </summary>
        public static void Collect(GameDatabase database, CharacterDefinition character, List<StatModifier> modifiers, List<PlayerEffect> effects,
                                   int overrideSlot = -1, EquipmentData overrideItem = null)
        {
            if (character == null || character.equipmentSlots == null) return;
            for (int slot = 0; slot < character.equipmentSlots.Length; slot++)
            {
                var item = slot == overrideSlot ? overrideItem : GetEquipped(database, character, slot);
                if (item == null) continue;
                if (modifiers != null && item.modifiers != null) modifiers.AddRange(item.modifiers);
                if (effects != null && item.effects != null)
                {
                    foreach (var effect in item.effects)
                    {
                        if (effect != null) effects.Add(effect);
                    }
                }
            }
        }

        /// <summary>把這個角色的穿戴登記成 loadout 的裝備來源（要再 Apply／ReapplyStats 才生效）。</summary>
        public static void Register(PlayerLoadout loadout, GameDatabase database, CharacterDefinition character)
        {
            if (loadout == null) return;
            var modifiers = new List<StatModifier>();
            var effects = new List<PlayerEffect>();
            Collect(database, character, modifiers, effects);
            if (modifiers.Count + effects.Count == 0) loadout.ClearSource(SourceKey);
            else loadout.SetSource(SourceKey, modifiers, effects);
        }
    }
}
