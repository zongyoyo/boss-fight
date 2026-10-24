using System;
using System.Collections.Generic;
using BossFight.Player;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BossFight.Progression
{
    /// <summary>
    /// 數值套用管線（企劃書「數值套用管線」）：
    /// CharacterDefinition.baseStats → Instantiate 成複本 → 所有加值 → 所有倍率（同一項相加後一次乘）→ 夾回合理範圍
    /// → PlayerRig.ApplyStats → 掛上效果（PlayerEffect）。不寫入任何資產。
    /// 修正依「來源」登記（SetSource：裝備、技能、Debug 的測試修正各一個 key），換角色時來源保留，由登記的一方自己換掉。
    /// PlayerSpawner 擁有一份：生成時套用，之後換裝、點技能、Debug 改修正時呼叫 PlayerSpawner.ReapplyStats。
    /// 舊的複本與效果元件在重新套用時銷毀（換掉的複本沒有人再引用）。
    /// </summary>
    public class PlayerLoadout
    {
        sealed class Source
        {
            public readonly List<StatModifier> modifiers = new List<StatModifier>();
            public readonly List<PlayerEffect> effects = new List<PlayerEffect>();
        }

        readonly Dictionary<string, Source> sources = new Dictionary<string, Source>();
        readonly List<string> order = new List<string>(); // 來源登記的順序（效果照這個順序掛）
        readonly List<PlayerEffectBehaviour> attached = new List<PlayerEffectBehaviour>();
        PlayerStats applied;

        /// <summary>目前的角色（PlayerSpawner 生成時設定）。</summary>
        public CharacterDefinition Character { get; set; }

        /// <summary>
        /// 取得基礎數值的方式；null = 直接用 Character.baseStats。給 Debug 的平衡面板（F12）把基礎值換成它的複本，
        /// 面板調的是基礎值，修正照樣疊在上面（跟難度修正一樣）。
        /// </summary>
        public Func<PlayerStats, PlayerStats> BaseStatsSource { get; set; }

        /// <summary>這次套用的基礎數值（BaseStatsSource 換過的話是它給的那份）。</summary>
        public PlayerStats BaseStats
        {
            get
            {
                var original = Character != null ? Character.baseStats : null;
                if (original == null || BaseStatsSource == null) return original;
                var replaced = BaseStatsSource(original);
                return replaced != null ? replaced : original;
            }
        }

        /// <summary>最近一次套用的結果（角色身上正在用的那份）；還沒套用時 null。</summary>
        public PlayerStats Applied => applied;

        /// <summary>目前所有來源的修正條數（F12 面板提醒用）。</summary>
        public int ModifierCount
        {
            get
            {
                int count = 0;
                foreach (var source in sources.Values) count += source.modifiers.Count;
                return count;
            }
        }

        /// <summary>目前所有來源的效果數量。</summary>
        public int EffectCount
        {
            get
            {
                int count = 0;
                foreach (var source in sources.Values) count += source.effects.Count;
                return count;
            }
        }

        /// <summary>登記（或取代）一個來源的修正與效果；要呼叫 PlayerSpawner.ReapplyStats 才會生效。</summary>
        public void SetSource(string key, IEnumerable<StatModifier> modifiers, IEnumerable<PlayerEffect> effects = null)
        {
            if (string.IsNullOrEmpty(key)) return;
            if (!sources.TryGetValue(key, out var source))
            {
                source = new Source();
                sources.Add(key, source);
                order.Add(key);
            }
            source.modifiers.Clear();
            source.effects.Clear();
            if (modifiers != null) source.modifiers.AddRange(modifiers);
            if (effects != null)
            {
                foreach (var effect in effects)
                {
                    if (effect != null) source.effects.Add(effect);
                }
            }
        }

        /// <summary>拿掉一個來源；要呼叫 PlayerSpawner.ReapplyStats 才會生效。</summary>
        public void ClearSource(string key)
        {
            if (key != null && sources.Remove(key)) order.Remove(key);
        }

        /// <summary>這個來源有沒有登記。</summary>
        public bool HasSource(string key) => key != null && sources.ContainsKey(key);

        /// <summary>
        /// 建出數值複本、交給角色（refill：補滿血量、耐力與骨頭），再把效果元件拆掉重掛。
        /// 沒有角色定義或基礎數值時不做事並回傳 false（角色繼續用 prefab 上接的資產）。
        /// </summary>
        public bool Apply(PlayerRig rig, bool refill)
        {
            var baseStats = BaseStats;
            if (rig == null || baseStats == null) return false;

            var modifiers = new List<StatModifier>();
            foreach (var key in order) modifiers.AddRange(sources[key].modifiers);
            var previous = applied;
            applied = BuildStats(baseStats, modifiers);
            rig.ApplyStats(applied, refill);
            if (previous != null) Object.Destroy(previous);

            DetachEffects();
            foreach (var key in order)
            {
                foreach (var effect in sources[key].effects)
                {
                    var behaviour = effect.Attach(rig);
                    if (behaviour != null) attached.Add(behaviour);
                }
            }
            return true;
        }

        /// <summary>角色要被銷毀（換角色）時：拆掉效果、丟掉複本（新角色會重新套用）。</summary>
        public void Release()
        {
            DetachEffects();
            if (applied != null) Object.Destroy(applied);
            applied = null;
        }

        void DetachEffects()
        {
            foreach (var behaviour in attached)
            {
                if (behaviour == null) continue;
                behaviour.enabled = false; // 先退訂事件；Destroy 要等到這一幀結束
                Object.Destroy(behaviour);
            }
            attached.Clear();
        }

        // ---------- 計算 ----------

        /// <summary>複製 baseStats，套上 modifiers（先加值、再乘相加後的倍率）並夾回合理範圍。回傳的複本不存檔（HideFlags.DontSave）。</summary>
        public static PlayerStats BuildStats(PlayerStats baseStats, IEnumerable<StatModifier> modifiers)
        {
            var stats = Object.Instantiate(baseStats);
            stats.name = baseStats.name + " (Loadout)";
            stats.hideFlags = HideFlags.DontSave;

            int count = Enum.GetValues(typeof(PlayerStatId)).Length;
            var add = new float[count];
            var multiply = new float[count];
            var touched = new bool[count];
            if (modifiers != null)
            {
                foreach (var m in modifiers)
                {
                    int i = (int)m.stat;
                    if (i < 0 || i >= count) continue;
                    if (m.op == StatModifierOp.Multiply) multiply[i] += m.value;
                    else add[i] += m.value;
                    touched[i] = true;
                }
            }
            for (int i = 0; i < count; i++)
            {
                if (touched[i]) Modify(stats, (PlayerStatId)i, add[i], multiply[i]);
            }
            return stats;
        }

        static float Calc(float value, float add, float multiply) => (value + add) * (1f + multiply);

        /// <summary>PlayerStatId → PlayerStats 欄位；套用後夾回合理範圍。新增 PlayerStatId 時在這裡加一行。</summary>
        static void Modify(PlayerStats s, PlayerStatId stat, float add, float multiply)
        {
            switch (stat)
            {
                case PlayerStatId.MaxHealth: s.maxHealth = Mathf.Max(1f, Calc(s.maxHealth, add, multiply)); break;
                case PlayerStatId.MaxStamina: s.maxStamina = Mathf.Max(1f, Calc(s.maxStamina, add, multiply)); break;
                case PlayerStatId.StaminaRegen: s.staminaRegen = Mathf.Max(0f, Calc(s.staminaRegen, add, multiply)); break;
                case PlayerStatId.MoveSpeed: s.moveSpeed = Mathf.Max(0f, Calc(s.moveSpeed, add, multiply)); break;
                case PlayerStatId.RollStaminaCost: s.rollStaminaCost = Mathf.Max(0f, Calc(s.rollStaminaCost, add, multiply)); break;
                case PlayerStatId.BlockDamageReduction: s.blockDamageReduction = Mathf.Clamp01(Calc(s.blockDamageReduction, add, multiply)); break;
                case PlayerStatId.ParryWindow: s.parryWindow = Mathf.Max(0f, Calc(s.parryWindow, add, multiply)); break;
                case PlayerStatId.ParryImbalance: s.parryImbalance = Mathf.Max(0f, Calc(s.parryImbalance, add, multiply)); break;
                case PlayerStatId.BoneCapacity: s.boneCount = Mathf.Max(0, Mathf.RoundToInt(Calc(s.boneCount, add, multiply))); break;
                case PlayerStatId.BoneHealAmount: s.boneHealAmount = Mathf.Max(0f, Calc(s.boneHealAmount, add, multiply)); break;
                case PlayerStatId.LightAttackDamage:
                    for (int i = 0; i < s.lightCombo.Length; i++) s.lightCombo[i].damage = Mathf.Max(0f, Calc(s.lightCombo[i].damage, add, multiply));
                    break;
                case PlayerStatId.HeavyAttackDamage: s.heavyAttack.damage = Mathf.Max(0f, Calc(s.heavyAttack.damage, add, multiply)); break;
                case PlayerStatId.ExecutionDamage: s.executeAttack.damage = Mathf.Max(0f, Calc(s.executeAttack.damage, add, multiply)); break;
                default: Debug.LogWarning($"[PlayerLoadout] PlayerStatId.{stat} 還沒有對應的欄位。"); break;
            }
        }
    }
}
