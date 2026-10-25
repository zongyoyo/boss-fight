using System;
using System.Collections.Generic;
using BossFight.Player;
using BossFight.Progression;
using UnityEngine;

namespace BossFight.Hub
{
    /// <summary>
    /// 休息區的傳送門（T20 起是正式的站點）：對它按 Player/Interact 打開挑戰清單（HubStationUI）。
    /// 清單（T24）照 GameDatabase.bosses 的順序，一隻 Boss 一列（名稱、傳送門說明、擊敗過沒有），打開時用 OnBossesChanged 送出；
    /// 確認後介面呼叫 Depart(boss)，經 SceneFlow.GoToBattle 換到那隻 Boss 的場景（黑幕淡出期間介面維持開著、輸入維持鎖住）。
    /// </summary>
    public class Portal : Interactable
    {
        /// <summary>挑戰清單的一列。</summary>
        public readonly struct BossEntry
        {
            public readonly BossDefinition boss;
            /// <summary>存檔裡擊敗過。</summary>
            public readonly bool defeated;

            public BossEntry(BossDefinition boss, bool defeated)
            {
                this.boss = boss;
                this.defeated = defeated;
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
                    if (boss != null) entries.Add(new BossEntry(boss, boss.IsDefeated));
                }
            }
            OnBossesChanged?.Invoke(entries);
        }

        /// <summary>出發挑戰這隻 Boss（換到它的場景）。</summary>
        public void Depart(BossDefinition boss)
        {
            if (sceneFlow != null && boss != null) sceneFlow.GoToBattle(boss.sceneName);
        }
    }
}
