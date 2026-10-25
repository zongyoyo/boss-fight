using System;
using System.Collections.Generic;
using BossFight.Core;
using BossFight.Player;
using BossFight.Progression;
using UnityEngine;

namespace BossFight.Hub
{
    /// <summary>
    /// 休息區的鐵匠鋪（T22）：對鐵砧按 Player/Interact 後玩家走到站位點、鐵匠鋪站點鏡頭（越肩）切過去，介面是 BlacksmithUI。
    /// 介面內容用 OnViewChanged 送出：目前角色的欄位（CharacterDefinition.equipmentSlots 與存檔的穿戴）、GameDatabase 的所有裝備
    /// （解鎖了沒有、「新」、穿在哪一格、這個角色有沒有放得下的欄位）。
    /// Toggle 穿上或卸下一件（穿進第一個種類相符的空欄位，都滿了就換掉第一格）、Unequip 卸下一格：寫進存檔後
    /// PlayerSpawner.ReapplyEquipment 立即重新套用並補滿血量與耐力。Preview 算換裝前後的數值差異（不套用）。
    /// 「新」標記在關掉鐵匠鋪時清掉（打開過就算看過）。開發者模式開著時可以 DebugUnlockAll（沒有 Debug 程式的正式 build 沒有）。
    /// </summary>
    public class Blacksmith : Interactable
    {
        /// <summary>目前角色的一格。</summary>
        public readonly struct SlotEntry
        {
            public readonly EquipmentSlotType type;
            /// <summary>穿著的裝備；空著時 null。</summary>
            public readonly EquipmentData item;

            public SlotEntry(EquipmentSlotType type, EquipmentData item)
            {
                this.type = type;
                this.item = item;
            }
        }

        /// <summary>裝備清單的一件。</summary>
        public readonly struct ItemEntry
        {
            public readonly EquipmentData item;
            public readonly bool unlocked;
            /// <summary>「新」標記（解鎖後還沒在鐵匠鋪看過）。</summary>
            public readonly bool isNew;
            /// <summary>目前角色穿在第幾格；沒穿時 −1。</summary>
            public readonly int equippedSlot;
            /// <summary>目前角色有沒有放得下它的欄位（也看可裝備角色）。</summary>
            public readonly bool hasSlot;

            public ItemEntry(EquipmentData item, bool unlocked, bool isNew, int equippedSlot, bool hasSlot)
            {
                this.item = item;
                this.unlocked = unlocked;
                this.isNew = isNew;
                this.equippedSlot = equippedSlot;
                this.hasSlot = hasSlot;
            }
        }

        /// <summary>介面要顯示的全部內容（同一份物件重複使用，收到後不要留著）。</summary>
        public sealed class View
        {
            public CharacterDefinition character;
            public readonly List<SlotEntry> slots = new List<SlotEntry>();
            public readonly List<ItemEntry> items = new List<ItemEntry>();
            /// <summary>顯示 Debug 的「解鎖全部裝備」。</summary>
            public bool debugTools;
        }

        /// <summary>一項數值換裝前後的值。</summary>
        public readonly struct StatChange
        {
            public readonly PlayerStatId stat;
            public readonly float before, after;

            public StatChange(PlayerStatId stat, float before, float after)
            {
                this.stat = stat;
                this.before = before;
                this.after = after;
            }
        }

        /// <summary>差異預覽的結果。</summary>
        public sealed class Preview
        {
            /// <summary>true = 這次是卸下（穿著的那件、或點了有裝備的欄位）。</summary>
            public bool unequip;
            public readonly List<StatChange> stats = new List<StatChange>();
            public readonly List<PlayerEffect> addedEffects = new List<PlayerEffect>();
            public readonly List<PlayerEffect> removedEffects = new List<PlayerEffect>();
        }

        [Tooltip("目前角色、裝備資料與重新套用（ReapplyEquipment）")]
        [SerializeField] PlayerSpawner spawner;
        [Tooltip("開發者模式開著時才顯示 Debug 的「解鎖全部裝備」；留空時不顯示")]
        [SerializeField] GameOptions options;

        /// <summary>介面內容（打開、換裝、Debug 解鎖時送出）。</summary>
        public event Action<View> OnViewChanged;

        readonly View view = new View();
        bool open;

        GameDatabase Database => spawner != null ? spawner.Database : null;
        CharacterDefinition Character => spawner != null ? spawner.CurrentCharacter : null;

        public override void Opened(PlayerRig rig)
        {
            open = true;
            Refresh();
        }

        // 打開過就算看過：清掉「新」
        public override void Closed(PlayerRig rig)
        {
            open = false;
            ProfileStore.ClearNewEquipment();
        }

        /// <summary>穿上這件（已經穿著就卸下）；鐵匠鋪沒開著、沒解鎖或沒有放得下的欄位時不做事。</summary>
        public void Toggle(EquipmentData item)
        {
            var character = Character;
            if (!open || character == null || item == null) return;
            int worn = WornSlot(item);
            if (worn >= 0) ProfileStore.SetEquipped(character.id, worn, string.Empty);
            else
            {
                int slot = TargetSlot(item);
                if (slot < 0) return;
                ProfileStore.SetEquipped(character.id, slot, item.id);
            }
            Changed();
        }

        /// <summary>卸下第 slot 格；空著時不做事。</summary>
        public void Unequip(int slot)
        {
            var character = Character;
            if (!open || character == null || EquipmentLoadout.GetEquipped(Database, character, slot) == null) return;
            ProfileStore.SetEquipped(character.id, slot, string.Empty);
            Changed();
        }

        /// <summary>點這件會發生的數值變化（穿上，或穿著時卸下）；不能穿時 null。回傳的物件重複使用。</summary>
        public Preview PreviewItem(EquipmentData item)
        {
            if (item == null || Character == null) return null;
            int worn = WornSlot(item);
            if (worn >= 0) return Compare(worn, null);
            int slot = TargetSlot(item);
            return slot >= 0 ? Compare(slot, item) : null;
        }

        /// <summary>卸下第 slot 格會發生的數值變化；空著時 null。</summary>
        public Preview PreviewUnequip(int slot)
        {
            if (EquipmentLoadout.GetEquipped(Database, Character, slot) == null) return null;
            return Compare(slot, null);
        }

        /// <summary>Debug：解鎖 GameDatabase 的所有裝備（標成「新」）；開發者模式關著或沒有 Debug 程式時不做事。回傳這次解鎖了幾件。</summary>
        public int DebugUnlockAll()
        {
            if (!open || !DebugToolsShown || Database == null) return 0;
            int count = 0;
            foreach (var item in Database.equipment)
            {
                if (item != null && ProfileStore.UnlockEquipment(item.id)) count++;
            }
            if (count > 0) Refresh();
            return count;
        }

        void Changed()
        {
            if (spawner != null) spawner.ReapplyEquipment();
            Refresh();
        }

        void Refresh()
        {
            var character = Character;
            var database = Database;
            view.character = character;
            view.slots.Clear();
            view.items.Clear();
            view.debugTools = DebugToolsShown;
            if (character != null && character.equipmentSlots != null)
            {
                for (int slot = 0; slot < character.equipmentSlots.Length; slot++)
                    view.slots.Add(new SlotEntry(character.equipmentSlots[slot], EquipmentLoadout.GetEquipped(database, character, slot)));
            }
            if (database != null)
            {
                foreach (var item in database.equipment)
                {
                    if (item == null) continue;
                    view.items.Add(new ItemEntry(item, item.IsUnlocked, ProfileStore.IsEquipmentNew(item.id), WornSlot(item),
                                                 EquipmentLoadout.HasSlotFor(item, character)));
                }
            }
            OnViewChanged?.Invoke(view);
        }

        // 目前角色穿著這件的那一格；沒穿時 −1
        int WornSlot(EquipmentData item)
        {
            var character = Character;
            if (character == null || character.equipmentSlots == null) return -1;
            for (int slot = 0; slot < character.equipmentSlots.Length; slot++)
            {
                if (EquipmentLoadout.GetEquipped(Database, character, slot) == item) return slot;
            }
            return -1;
        }

        // 要穿進哪一格：第一個放得下的空欄位，都滿了就第一個放得下的；都放不下時 −1
        int TargetSlot(EquipmentData item)
        {
            var character = Character;
            if (character == null || character.equipmentSlots == null) return -1;
            int first = -1;
            for (int slot = 0; slot < character.equipmentSlots.Length; slot++)
            {
                if (!EquipmentLoadout.Fits(item, character, slot)) continue;
                if (EquipmentLoadout.GetEquipped(Database, character, slot) == null) return slot;
                if (first < 0) first = slot;
            }
            return first;
        }

        readonly Preview preview = new Preview();
        readonly List<StatModifier> modifiersBefore = new List<StatModifier>(), modifiersAfter = new List<StatModifier>();
        readonly List<PlayerEffect> effectsBefore = new List<PlayerEffect>(), effectsAfter = new List<PlayerEffect>();
        static readonly PlayerStatId[] StatIds = (PlayerStatId[])Enum.GetValues(typeof(PlayerStatId));

        // 第 slot 格換成 item（null = 卸下）前後的數值與效果
        Preview Compare(int slot, EquipmentData item)
        {
            preview.unequip = item == null;
            preview.stats.Clear();
            preview.addedEffects.Clear();
            preview.removedEffects.Clear();
            var loadout = spawner.Loadout;
            modifiersBefore.Clear();
            modifiersAfter.Clear();
            effectsBefore.Clear();
            effectsAfter.Clear();
            EquipmentLoadout.Collect(Database, Character, modifiersBefore, effectsBefore);
            EquipmentLoadout.Collect(Database, Character, modifiersAfter, effectsAfter, slot, item);
            var before = loadout.Preview(EquipmentLoadout.SourceKey, modifiersBefore);
            var after = loadout.Preview(EquipmentLoadout.SourceKey, modifiersAfter);
            if (before != null && after != null)
            {
                foreach (var stat in StatIds)
                {
                    float a = PlayerLoadout.Read(before, stat), b = PlayerLoadout.Read(after, stat);
                    if (!Mathf.Approximately(a, b)) preview.stats.Add(new StatChange(stat, a, b));
                }
            }
            if (before != null) Destroy(before);
            if (after != null) Destroy(after);
            foreach (var effect in effectsAfter)
            {
                if (!effectsBefore.Remove(effect)) preview.addedEffects.Add(effect);
            }
            preview.removedEffects.AddRange(effectsBefore); // 剩下的是拿掉的
            return preview;
        }

        bool DebugToolsShown
        {
            get
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD || BOSSFIGHT_DEV_TOOLS
                return options != null && options.DeveloperMode;
#else
                return false; // 沒有 Debug 程式的正式 build 沒有「解鎖全部裝備」
#endif
            }
        }
    }
}
