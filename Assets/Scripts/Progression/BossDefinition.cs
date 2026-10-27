using System.Collections.Generic;
using BossFight.Core;
using UnityEngine;

namespace BossFight.Progression
{
    /// <summary>
    /// 一隻 Boss（T24，企劃書「獎勵」）：id、名稱、場景、首通獎勵、傳送門清單的說明文字。
    /// 休息區的傳送門照 GameDatabase.bosses 列出挑戰清單、出發時換到 sceneName；Boss 戰的 BattleSettings 引用這一份，
    /// 勝利時 ProgressRecorder.RecordBossDefeat 用 id 記進存檔的擊敗紀錄，第一次擊敗就發 firstClearRewards。
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

        [Header("獎勵")]
        [Tooltip("第一次擊敗時發的獎勵（勝利畫面「首次擊敗獎勵」列出）")]
        public List<RewardData> firstClearRewards = new List<RewardData>();

        /// <summary>存檔裡擊敗過沒有。</summary>
        public bool IsDefeated => ProfileStore.HasDefeated(id);
    }
}
