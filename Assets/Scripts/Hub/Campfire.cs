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
    /// 介面（CampfireUI）分「角色」「技能樹」兩頁（技能樹 T23）。
    /// 角色清單照名冊的順序：解鎖過的角色（CharacterDefinition.IsUnlocked），developerOnly 的只在開發者模式開著時列出
    /// （沒有 Debug 程式的 build 一律不列）；目前的角色一定列出。清單在打開、換角色、切換開發者模式時用 OnCharactersChanged 送出。
    /// SelectCharacter 換角色：存檔記住目前角色、PlayerSpawner.ReplaceCharacter 在原位換人（HubManager 重新鎖住新角色），換的那一刻冒一團煙遮住。
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

        [Tooltip("換角色（ReplaceCharacter）與角色名冊")]
        [SerializeField] PlayerSpawner spawner;
        [Tooltip("開發者模式開著時才列出 developerOnly 的角色；留空時不列")]
        [SerializeField] GameOptions options;

        [Header("換角色的煙（視覺）")]
        [Tooltip("換角色那一刻在角色身上放的特效；留空時不放")]
        [SerializeField] TimedFx swapFx;
        [SerializeField] Color swapFxColor = new Color(1f, 0.9f, 0.75f, 0.6f);
        [Min(0f)] [SerializeField] float swapFxSize = 2.4f;
        [Tooltip("特效相對角色腳底的位置 (m)")]
        [SerializeField] Vector3 swapFxOffset = new Vector3(0f, 0.8f, 0f);

        /// <summary>角色清單（打開營火、換角色、切換開發者模式時送出）。</summary>
        public event Action<IReadOnlyList<CharacterEntry>> OnCharactersChanged;

        readonly List<CharacterEntry> entries = new List<CharacterEntry>();
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

        void Refresh()
        {
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

        bool Listed(CharacterDefinition character) => character.IsUnlocked && (!character.developerOnly || DeveloperCharactersShown);

        bool DeveloperCharactersShown
        {
            get
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD || BOSSFIGHT_DEV_TOOLS
                return options != null && options.DeveloperMode;
#else
                return false; // 沒有 Debug 程式的正式 build 不列測試角色
#endif
            }
        }
    }
}
