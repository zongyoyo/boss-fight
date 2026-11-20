using System.Collections.Generic;
using BossFight.Core;
using UnityEngine;

namespace BossFight.Progression
{
    /// <summary>
    /// 一隻 Boss（T24，企劃書「獎勵」）：id、名稱、場景、首通獎勵、傳送門清單的說明文字。
    /// 休息區的傳送門照 GameDatabase.bosses 列出挑戰清單、出發時換到 sceneName；Boss 戰的 BattleSettings 引用這一份，
    /// 勝利時 ProgressRecorder.RecordBossDefeat 用 id 記進存檔的擊敗紀錄，第一次擊敗就發 firstClearRewards。
    /// T34：unlockQuest 完成前傳送門清單上鎖（第一次看到解鎖時播鎖鏈碎裂，ProfileStore 記看過）；portalRowColor 是清單那一列的底色；
    /// portalArrival 是出發時的出生點 id（大樹守衛：序章看到它就跳過教學、直接播登場演出開戰，結局不寫序章）。
    /// </summary>
    [CreateAssetMenu(menuName = "BossFight/Progression/Boss Definition", fileName = "Boss")]
    public class BossDefinition : ScriptableObject
    {
        [Header("識別")]
        [Tooltip("存檔用的 id（擊敗紀錄）：不能空、不能跟其他 Boss 重複，存檔後不要改（Tools > Progression > Validate Game Database 檢查）")]
        public string id;
        [Tooltip("顯示名稱（傳送門清單、確認框）")]
        public string displayName;
        [Tooltip("Boss 戰的場景名稱（要在 Build Settings 裡）")]
        public string sceneName;
        [Tooltip("傳送門清單選到這一列時下方的說明")]
        [TextArea(2, 4)] public string portalDescription;
        [Tooltip("列入傳送門挑戰清單；一次性劇情 Boss 關閉")] public bool showInPortal = true;

        [Header("傳送門（T34）")]
        [Tooltip("完成這個任務才解鎖挑戰（空 = 一開始就能挑戰）；解鎖前清單裡這一列上鎖、不能選，完成後第一次打開傳送門播鎖鏈碎裂")]
        public QuestData unlockQuest;
        [Tooltip("清單裡這一列的底色；alpha 0 = 用 HubUIData 的一般按鈕色")]
        public Color portalRowColor = new Color(0f, 0f, 0f, 0f);
        [Tooltip("從傳送門出發時新場景的出生點 id（PlayerSpawner 的 spawnPoints；空 = 場景預設位置）。序章用它認出「從傳送門來的重複挑戰」")]
        public string portalArrival = "";

        [Header("獎勵")]
        [Tooltip("第一次擊敗時發的獎勵（勝利畫面「首次擊敗獎勵」列出）")]
        public List<RewardData> firstClearRewards = new List<RewardData>();

        /// <summary>存檔裡擊敗過沒有。</summary>
        public bool IsDefeated => ProfileStore.HasDefeated(id);

        /// <summary>傳送門可以挑戰了（沒有解鎖任務，或那個任務已完成）。</summary>
        public bool IsPortalUnlocked => unlockQuest == null || ProfileStore.IsQuestCompleted(unlockQuest.id);

        /// <summary>有解鎖任務、已經解鎖，但還沒在傳送門清單播過解鎖演出（存檔記）。</summary>
        public bool PortalUnlockPending => unlockQuest != null && IsPortalUnlocked && !ProfileStore.IsPortalUnlockSeen(id);
    }
}
