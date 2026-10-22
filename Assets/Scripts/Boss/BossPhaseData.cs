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
        [Tooltip("進入這個階段的轉階段時間（秒）：期間 Boss 無敵、不出招、打斷目前的招式；0 = 直接切換，目前的招照常打完。" +
                 "目前時間到自動結束，T7 改由轉階段演出呼叫 BossController.CompleteTransition")]
        [Min(0f)] public float transitionTime;

        [Header("移動與節奏")]
        [Tooltip("走位的移動速度 (m/s)")]
        [Min(0f)] public float moveSpeed = 3f;
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
        [Tooltip("OIIA 音樂的播放速度倍率（T7 的 AudioManager 讀）")]
        [Min(0.1f)] public float musicSpeed = 1f;

        [Header("開場序列")]
        [Tooltip("進入這個階段後依序固定出的招式：不看冷卻、權重與不連出，但要先走進那招的距離範圍。招式要掛在 Boss 的招式清單裡")]
        public BossMoveData[] openingSequence;

        /// <summary>這次的出招間隔（秒）。</summary>
        public float RandomInterval() => Random.Range(moveIntervalMin, Mathf.Max(moveIntervalMin, moveIntervalMax));
    }
}
