using System;
using System.Collections.Generic;
using BossFight.Core;
using BossFight.Hazards;
using BossFight.Player;
using BossFight.Progression;
using UnityEngine;

namespace BossFight.Hub
{
    /// <summary>
    /// 休息區的營火（T21）：對它按 Player/Interact 後，玩家走到站位點、坐在圓木上（DaoDunDogAnimator.SetSitting），營火站點鏡頭拉近，
    /// 介面（CampfireUI）分「角色」「技能樹」兩頁。
    /// 角色清單照名冊的順序：解鎖過的角色（CharacterDefinition.IsUnlocked），developerOnly 的只在開發者模式開著時列出
    /// （沒有 Debug 程式的 build 一律不列）；目前的角色一定列出。清單在打開、換角色、切換開發者模式時用 OnCharactersChanged 送出。
    /// SelectCharacter 換角色：存檔記住目前角色、PlayerSpawner.ReplaceCharacter 在原位換人（HubManager 重新鎖住新角色），換的那一刻冒一團煙遮住。
    /// 技能樹（T23）：目前角色的 SkillTreeData、每個節點的狀態與技能點池用 OnSkillTreeChanged 送出（打開、換角色、學習、重置、Debug 給點數、切換開發者模式時）。
    /// Learn 學一個節點、ResetSkills 免費重置目前角色（點數全退回池子）：規則在 SkillTreeLoadout，寫進存檔後 PlayerSpawner.ReapplySkills
    /// 立即重新套用並補滿血量與耐力。PreviewNode 算學了之後的數值差異（不套用）。開發者模式開著時可以 DebugGiveSkillPoints（沒有 Debug 程式的正式 build 沒有）。
    /// </summary>
    public class Campfire : Interactable
    {
        /// <summary>角色清單的一列。</summary>
        public readonly struct CharacterEntry
        {
            public readonly CharacterDefinition character;
            /// <summary>目前的角色。</summary>
            public readonly bool current;

            public CharacterEntry(CharacterDefinition character, bool current)
            {
                this.character = character;
                this.current = current;
            }
        }

        /// <summary>技能樹的一個節點與它對目前角色的狀態。</summary>
        public readonly struct SkillEntry
        {
            public readonly SkillNode node;
            public readonly SkillNodeState state;

            public SkillEntry(SkillNode node, SkillNodeState state)
            {
                this.node = node;
                this.state = state;
            }
        }

        /// <summary>技能樹頁要顯示的全部內容（同一份物件重複使用，收到後不要留著）。</summary>
        public sealed class SkillTreeView
        {
            public CharacterDefinition character;
            /// <summary>目前角色的樹；沒有時 null。</summary>
            public SkillTreeData tree;
            public readonly List<SkillEntry> nodes = new List<SkillEntry>();
            /// <summary>池子剩下的技能點。</summary>
            public int points;
            /// <summary>目前角色付過的技能點（重置會退回的量）。</summary>
            public int spent;
            /// <summary>有沒有學過任何節點（重置按鈕）。</summary>
            public bool anyLearned;
            /// <summary>顯示 Debug 的「給技能點」。</summary>
            public bool debugTools;
        }

        /// <summary>學一個節點前後的數值與效果（差異預覽）。</summary>
        public sealed class SkillPreview
        {
            public readonly List<Blacksmith.StatChange> stats = new List<Blacksmith.StatChange>();
            public readonly List<PlayerEffect> addedEffects = new List<PlayerEffect>();
        }

        [Tooltip("換角色（ReplaceCharacter）與角色名冊")]
        [SerializeField] PlayerSpawner spawner;
        [Tooltip("開發者模式開著時才列出 developerOnly 的角色；留空時不列")]
        [SerializeField] GameOptions options;

        [Header("Debug")]
        [Tooltip("開發者模式的「給技能點」一次給幾點")]
        [Min(1)] [SerializeField] int debugSkillPoints = 10;

        [Header("換角色的煙（視覺）")]
        [Tooltip("換角色那一刻在角色身上放的特效；留空時不放")]
        [SerializeField] TimedFx swapFx;
        [SerializeField] Color swapFxColor = new Color(1f, 0.9f, 0.75f, 0.6f);
        [Min(0f)] [SerializeField] float swapFxSize = 2.4f;
        [Tooltip("特效相對角色腳底的位置 (m)")]
        [SerializeField] Vector3 swapFxOffset = new Vector3(0f, 0.8f, 0f);

        /// <summary>角色清單（打開營火、換角色、切換開發者模式時送出）。</summary>
        public event Action<IReadOnlyList<CharacterEntry>> OnCharactersChanged;

        /// <summary>技能樹頁的內容（打開營火、換角色、學習、重置、Debug 給點數、切換開發者模式時送出）。</summary>
        public event Action<SkillTreeView> OnSkillTreeChanged;

        /// <summary>Debug「給技能點」一次給的點數（按鈕文字用）。</summary>
        public int DebugSkillPointAmount => debugSkillPoints;

        readonly List<CharacterEntry> entries = new List<CharacterEntry>();
        readonly SkillTreeView skillView = new SkillTreeView();
        readonly SkillPreview skillPreview = new SkillPreview();
        readonly List<StatModifier> modifiersBefore = new List<StatModifier>(), modifiersAfter = new List<StatModifier>();
        readonly List<PlayerEffect> effectsBefore = new List<PlayerEffect>(), effectsAfter = new List<PlayerEffect>();
        static readonly PlayerStatId[] StatIds = (PlayerStatId[])Enum.GetValues(typeof(PlayerStatId));
        bool open;

        void OnEnable()
        {
            if (options != null) options.OnDeveloperModeChanged += HandleDeveloperModeChanged;
            if (spawner != null) spawner.OnPlayerSpawned += HandlePlayerSpawned;
        }

        void OnDisable()
        {
            if (options != null) options.OnDeveloperModeChanged -= HandleDeveloperModeChanged;
            if (spawner != null) spawner.OnPlayerSpawned -= HandlePlayerSpawned;
        }

        public override void Opened(PlayerRig rig)
        {
            open = true;
            Refresh();
        }

        // 走到站位點才坐下
        public override void Tick(PlayerRig rig, bool arrived)
        {
            if (rig != null && rig.Animator != null) rig.Animator.SetSitting(arrived);
        }

        public override void Closed(PlayerRig rig)
        {
            open = false;
            if (rig != null && rig.Animator != null) rig.Animator.SetSitting(false);
        }

        /// <summary>換成這個角色（CampfireUI 點了一列）；營火沒開著、不在清單裡或已經是目前角色時不做事。</summary>
        public void SelectCharacter(CharacterDefinition character)
        {
            if (!open || spawner == null || character == null || character == spawner.CurrentCharacter || !Listed(character)) return;
            ProfileStore.SetCurrentCharacter(character.id);
            Vector3 position = spawner.Current != null ? spawner.Current.transform.position : StandPoint.position;
            spawner.ReplaceCharacter(character);
            TimedFx.Spawn(swapFx, position + swapFxOffset, Quaternion.identity, swapFxColor, swapFxSize);
        }

        // 換上來的角色直接坐好（煙遮住換人的那一刻）
        void HandlePlayerSpawned(PlayerRig rig)
        {
            if (!open) return;
            if (rig.Animator != null) rig.Animator.SetSitting(true, true);
            Refresh();
        }

        void HandleDeveloperModeChanged(bool on)
        {
            if (open) Refresh();
        }

        // ---------- 技能樹 ----------

        /// <summary>目前角色學這個節點（扣點數、存檔、重新套用並補滿）；營火沒開著或不能學時不做事。回傳 true = 學會了。</summary>
        public bool Learn(SkillNode node)
        {
            if (!open || spawner == null || !SkillTreeLoadout.Learn(spawner.CurrentCharacter, node)) return false;
            spawner.ReapplySkills();
            RefreshSkillTree();
            return true;
        }

        /// <summary>免費重置目前角色的技能（付過的點數全退回池子、重新套用並補滿）；營火沒開著或沒學過時不做事。回傳退回的點數。</summary>
        public int ResetSkills()
        {
            var character = spawner != null ? spawner.CurrentCharacter : null;
            if (!open || character == null || (!AnyLearned(character) && ProfileStore.SpentSkillPoints(character.id) == 0)) return 0;
            int refund = SkillTreeLoadout.Reset(character);
            spawner.ReapplySkills();
            RefreshSkillTree();
            return refund;
        }

        /// <summary>學了這個節點會發生的數值與效果變化；已經學過或沒有角色時 null。回傳的物件重複使用。</summary>
        public SkillPreview PreviewNode(SkillNode node)
        {
            var character = spawner != null ? spawner.CurrentCharacter : null;
            if (character == null || node == null || SkillTreeLoadout.IsLearned(character, node)) return null;
            skillPreview.stats.Clear();
            skillPreview.addedEffects.Clear();
            modifiersBefore.Clear();
            modifiersAfter.Clear();
            effectsBefore.Clear();
            effectsAfter.Clear();
            SkillTreeLoadout.Collect(character, modifiersBefore, effectsBefore);
            SkillTreeLoadout.Collect(character, modifiersAfter, effectsAfter, node);
            var loadout = spawner.Loadout;
            var before = loadout.Preview(SkillTreeLoadout.SourceKey, modifiersBefore);
            var after = loadout.Preview(SkillTreeLoadout.SourceKey, modifiersAfter);
            if (before != null && after != null)
            {
                foreach (var stat in StatIds)
                {
                    float a = PlayerLoadout.Read(before, stat), b = PlayerLoadout.Read(after, stat);
                    if (!Mathf.Approximately(a, b)) skillPreview.stats.Add(new Blacksmith.StatChange(stat, a, b));
                }
            }
            if (before != null) Destroy(before);
            if (after != null) Destroy(after);
            foreach (var effect in effectsAfter)
            {
                if (!effectsBefore.Remove(effect)) skillPreview.addedEffects.Add(effect);
            }
            return skillPreview;
        }

        /// <summary>Debug：技能點池加 debugSkillPoints 點；開發者模式關著或沒有 Debug 程式時不做事。回傳給了幾點。</summary>
        public int DebugGiveSkillPoints()
        {
            if (!open || !DeveloperModeOn) return 0;
            ProfileStore.AddSkillPoints(debugSkillPoints);
            RefreshSkillTree();
            return debugSkillPoints;
        }

        static bool AnyLearned(CharacterDefinition character)
        {
            if (character.skillTree == null || character.skillTree.nodes == null) return false;
            foreach (var node in character.skillTree.nodes)
            {
                if (SkillTreeLoadout.IsLearned(character, node)) return true;
            }
            return false;
        }

        void RefreshSkillTree()
        {
            var character = spawner != null ? spawner.CurrentCharacter : null;
            skillView.character = character;
            skillView.tree = character != null ? character.skillTree : null;
            skillView.nodes.Clear();
            skillView.points = ProfileStore.SkillPoints;
            skillView.spent = character != null ? ProfileStore.SpentSkillPoints(character.id) : 0;
            skillView.anyLearned = character != null && (AnyLearned(character) || skillView.spent > 0);
            skillView.debugTools = DeveloperModeOn;
            if (skillView.tree != null && skillView.tree.nodes != null)
            {
                foreach (var node in skillView.tree.nodes)
                {
                    if (node != null) skillView.nodes.Add(new SkillEntry(node, SkillTreeLoadout.State(character, node)));
                }
            }
            OnSkillTreeChanged?.Invoke(skillView);
        }

        // ---------- 角色清單 ----------

        void Refresh()
        {
            RefreshSkillTree();
            entries.Clear();
            var roster = spawner != null && spawner.Database != null ? spawner.Database.characters : null;
            var current = spawner != null ? spawner.CurrentCharacter : null;
            if (roster != null)
            {
                foreach (var character in roster.characters)
                {
                    if (character != null && (character == current || Listed(character)))
                        entries.Add(new CharacterEntry(character, character == current));
                }
            }
            OnCharactersChanged?.Invoke(entries);
        }

        bool Listed(CharacterDefinition character) => character.IsUnlocked && (!character.developerOnly || DeveloperModeOn);

        // 開發者模式開著（測試角色、Debug 的「給技能點」）
        bool DeveloperModeOn
        {
            get
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD || BOSSFIGHT_DEV_TOOLS
                return options != null && options.DeveloperMode;
#else
                return false; // 沒有 Debug 程式的正式 build 不列測試角色、沒有「給技能點」
#endif
            }
        }
    }
}
