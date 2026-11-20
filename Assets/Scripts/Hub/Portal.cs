using System;
using System.Collections.Generic;
using BossFight.Core;
using BossFight.Player;
using BossFight.Progression;
using UnityEngine;

namespace BossFight.Hub
{
    /// <summary>
    /// 休息區的傳送門（T20 起是正式的站點）：對它按 Player/Interact 打開挑戰清單（HubStationUI）。
    /// 清單（T24）照 GameDatabase.bosses 的順序，一隻 Boss 一列（名稱、傳送門說明、擊敗過沒有），打開時用 OnBossesChanged 送出；
    /// 確認後介面呼叫 Depart(boss)，經 SceneFlow.GoToBattle 換到那隻 Boss 的場景（黑幕淡出期間介面維持開著、輸入維持鎖住）。
    /// T34：BossDefinition.unlockQuest 還沒完成的那一列上鎖（不能出發）；解鎖後第一次打開時那一列帶 playUnlock（介面播鎖鏈碎裂），
    /// 同時就記進存檔（ProfileStore.MarkPortalUnlockSeen），中途關掉也只播這一次。出發時把 BossDefinition.portalArrival 交給 SceneFlow 當出生點。
    /// </summary>
    public class Portal : Interactable
    {
        /// <summary>挑戰清單的一列。</summary>
        public readonly struct BossEntry
        {
            public readonly BossDefinition boss;
            /// <summary>存檔裡擊敗過。</summary>
            public readonly bool defeated;
            /// <summary>解鎖任務還沒完成：不能出發。</summary>
            public readonly bool locked;
            /// <summary>這次打開要播解鎖演出（鎖與鎖鏈碎裂），播完才能選。</summary>
            public readonly bool playUnlock;

            public BossEntry(BossDefinition boss, bool defeated, bool locked, bool playUnlock)
            {
                this.boss = boss;
                this.defeated = defeated;
                this.locked = locked;
                this.playUnlock = playUnlock;
            }
        }

        [SerializeField] SceneFlow sceneFlow;
        [Tooltip("挑戰清單（GameDatabase.bosses）")]
        [SerializeField] GameDatabase database;

        /// <summary>挑戰清單（打開傳送門時送出）。</summary>
        public event Action<IReadOnlyList<BossEntry>> OnBossesChanged;

        readonly List<BossEntry> entries = new List<BossEntry>();

        public override void Opened(PlayerRig rig)
        {
            entries.Clear();
            if (database != null && database.bosses != null)
            {
                foreach (var boss in database.bosses)
                {
                    if (boss == null || !boss.showInPortal) continue;
                    bool unlock = boss.PortalUnlockPending;
                    if (unlock) ProfileStore.MarkPortalUnlockSeen(boss.id);
                    entries.Add(new BossEntry(boss, boss.IsDefeated, !boss.IsPortalUnlocked, unlock));
                }
            }
            OnBossesChanged?.Invoke(entries);
        }

        /// <summary>出發挑戰這隻 Boss（換到它的場景）。</summary>
        public void Depart(BossDefinition boss)
        {
            if (sceneFlow != null && boss != null && boss.showInPortal && boss.IsPortalUnlocked) sceneFlow.GoToBattle(boss.sceneName, boss.portalArrival);
        }
    }
}
