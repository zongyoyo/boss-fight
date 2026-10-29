using System;
using System.Collections.Generic;
using UnityEngine;

namespace BossFight.Progression
{
    /// <summary>任務目標的種類；目前只有擊敗 Boss（企劃書「村長與任務」）。</summary>
    public enum QuestObjectiveKind
    {
        /// <summary>擊敗 boss：看存檔的擊敗紀錄，接任務前就打贏也算。</summary>
        DefeatBoss,
    }

    /// <summary>任務的狀態（ProgressRecorder.GetState 由存檔算出來，不另外存）。</summary>
    public enum QuestState
    {
        /// <summary>未開放：還沒看過休息區的開場劇情，或前置任務還沒完成。</summary>
        Locked,
        /// <summary>可接：村長頭上「!」。</summary>
        Available,
        /// <summary>進行中：接了、目標還沒達成。</summary>
        Active,
        /// <summary>可回報：接了、目標達成，村長頭上「?」。</summary>
        ReadyToTurnIn,
        /// <summary>已完成：回報過、拿了獎勵。</summary>
        Completed,
    }

    /// <summary>任務目標。</summary>
    [Serializable]
    public class QuestObjective
    {
        [Tooltip("種類")]
        public QuestObjectiveKind kind;
        [Tooltip("要擊敗的 Boss（種類是擊敗 Boss 時）")]
        public BossDefinition boss;
    }

    /// <summary>
    /// 一個村長任務（T24，企劃書「村長與任務」）：id、標題、說明、三段對白（接任務前、進行中、回報時）、目標、獎勵、前置任務。
    /// 存檔只記「已接」「已完成」（id），狀態由 ProgressRecorder 依存檔算；村長（VillageChief）照 GameDatabase.quests 的順序挑要講哪個任務。
    /// 對白每一行是一句（說話者是村長，名稱在 HubStoryData），對話框依序逐字顯示。
    /// </summary>
    [CreateAssetMenu(menuName = "BossFight/Progression/Quest", fileName = "Quest")]
    public class QuestData : ScriptableObject
    {
        [Header("識別")]
        [Tooltip("存檔用的 id：不能空、不能跟其他任務重複，存檔後不要改（Tools > Progression > Validate Game Database 檢查）")]
        public string id;
        [Tooltip("任務名稱")]
        public string title;
        [Tooltip("任務說明（接任務的選項上方）")]
        [TextArea(2, 4)] public string description;

        [Header("對白（一行一句）")]
        [Tooltip("可接時：講完出現「接受／再想想」")]
        [TextArea(1, 3)] public List<string> offerLines = new List<string>();
        [Tooltip("進行中（接了、目標還沒達成）")]
        [TextArea(1, 3)] public List<string> activeLines = new List<string>();
        [Tooltip("接下任務時村長補的話（接在 HubStoryData.questAcceptedFormat 那句後面），例如傳送門在哪裡")]
        [TextArea(1, 3)] public List<string> acceptedLines = new List<string>();
        [Tooltip("回報時（獎勵已經發了，最後村長再講一句拿到什麼）")]
        [TextArea(1, 3)] public List<string> turnInLines = new List<string>();

        [Header("條件與獎勵")]
        [Tooltip("目標")]
        public QuestObjective objective = new QuestObjective();
        [Tooltip("回報時發的獎勵")]
        public List<RewardData> rewards = new List<RewardData>();
        [Tooltip("前置任務：全部完成才開放（另外都要先看過休息區的開場劇情）")]
        public List<QuestData> prerequisites = new List<QuestData>();
    }
}
