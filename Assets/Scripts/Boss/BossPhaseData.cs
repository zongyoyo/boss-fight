using UnityEngine;

namespace BossFight.Boss
{
    /// <summary>
    /// 一個階段的 Boss 數值（企劃書「Boss 數值」表格的一欄）：第一階段、第二階段、狂暴各一份。
    /// BossController 持有目前的階段；血量降到下一個階段的 healthThreshold 時切換。
    /// 執行期只讀不寫；資產放在 Assets/ScriptableObjects/Boss/Phases/。
    /// </summary>
    [CreateAssetMenu(fileName = "BossPhase", menuName = "BossFight/Boss/Phase Data")]
    public class BossPhaseData : ScriptableObject
    {
        [Header("階段")]
        [Tooltip("階段名稱（Debug 面板與 Console 顯示）")]
        public string displayName = "第一階段";
        [Tooltip("Boss 血量降到這個值（含）以下時進入這個階段；第一階段填血量上限")]
        [Min(0f)] public float healthThreshold = 2000f;
        [Tooltip("使用哪個招式池（BossMoveData.phase）")]
        [Range(1, 2)] public int movePool = 1;
        [Tooltip("進入這個階段要不要轉階段（> 0）：期間 Boss 無敵、不出招、打斷目前的招式、清掉場上的 Hazard；0 = 直接切換，目前的招照常打完。" +
                 "有轉階段演出時由演出結束呼叫 BossController.CompleteTransition；沒有演出（BossController.AutoCompleteTransition）時這個秒數到就自動結束")]
        [Min(0f)] public float transitionTime;
        [Tooltip("進場招式：血量首次降到這個階段的門檻時先出這招一次（狂暴的 OIIA 大迴旋）——打斷目前的動作（頭暈也一樣）、清掉場上的 Hazard 後立刻出，" +
                 "這招結束（它讓 Boss 頭暈的話，等頭暈結束）才套用這個階段。招式要掛在 Boss 的招式清單裡；留空 = 照 transitionTime 換階段")]
        public BossMoveData entryMove;

        [Header("移動與節奏")]
        [Tooltip("走位的移動速度 (m/s)")]
        [Min(0f)] public float moveSpeed = 3f;
        [Tooltip("固定站樁：無距離內招式時等待，不走位或逾時強制出招")] public bool stationary;
        [Tooltip("階段轉速；0 沿用 BossStats.turnSpeed"), Min(0f)] public float turnSpeedOverride;
        [Tooltip("破綻結束後略過一般等待、直接再選招的機率"), Range(0f, 1f)] public float chainChance;
        [Tooltip("一招結束到下一次選招的間隔（秒），在最小與最大之間隨機")]
        [Min(0f)] public float moveIntervalMin = 0.8f;
        [Min(0f)] public float moveIntervalMax = 1.5f;
        [Tooltip("招式冷卻倍率")]
        [Min(0f)] public float cooldownMultiplier = 1f;
        [Tooltip("走位逾時（秒）：為了走進某招的距離範圍連續走位這麼久還走不到（玩家一直拉開或貼身）就不等了——" +
                 "開場序列跳過這一招，一般選招直接出正在走向的那一招。0 = 不限，一直走")]
        [Min(0f)] public float repositionTimeout = 3f;

        [Header("失衡與頭暈")]
        [Tooltip("失衡值上限，滿了打斷目前的招式、進入頭暈（衰減與受傷倍率在 BossStats）")]
        [Min(1f)] public float imbalanceMax = 100f;
        [Tooltip("失衡值滿了的頭暈時間（秒）")]
        [Min(0f)] public float stunDuration = 3f;

        [Header("其他")]
        [Tooltip("碰到 Boss 本體的傷害（規則待定，尚未使用）")]
        [Min(0f)] public float contactDamage = 5f;
        [Tooltip("OIIA 音樂的播放速度倍率（BattleManager 換階段時交給 AudioManager）")]
        [Min(0.1f)] public float musicSpeed = 1f;
        [Tooltip("強化外觀：身上纏繞紅色電光、移動時拖出殘影（BossPhaseVisual 讀）；第二階段與狂暴開啟")]
        public bool electrified;

        [Header("開場序列")]
        [Tooltip("進入這個階段後依序固定出的招式：不看冷卻、權重與不連出，但要先走進那招的距離範圍。招式要掛在 Boss 的招式清單裡")]
        public BossMoveData[] openingSequence;

        /// <summary>這次的出招間隔（秒）。</summary>
        public float RandomInterval() => Random.Range(moveIntervalMin, Mathf.Max(moveIntervalMin, moveIntervalMax));
    }
}
