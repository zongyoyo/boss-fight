using UnityEngine;

namespace BossFight.Boss
{
    /// <summary>
    /// 一招的共通數值（企劃書「第一階段招式」「第二階段招式」的表格）。BossController 用距離、冷卻、權重選招，
    /// BossMove 依預兆、破綻時間與傷害出招。招式自己特有的數值（衝刺速度、落雷數量…）寫在子類別。
    /// 執行期只讀不寫；資產放在 Assets/ScriptableObjects/Boss/Moves/。
    /// </summary>
    [CreateAssetMenu(fileName = "BossMove", menuName = "BossFight/Boss/Move Data")]
    public class BossMoveData : ScriptableObject
    {
        [Header("基本")]
        [Tooltip("招式名稱（Debug 面板與 Console 顯示）")]
        public string displayName = "新招式";
        [Tooltip("屬於哪個招式池：1 = 第一階段、2 = 第二階段（狂暴沿用第二階段）；對應 BossPhaseData.movePool")]
        [Range(1, 2)] public int phase = 1;

        [Header("選招")]
        [Tooltip("玩家在這個水平距離 (m) 以上才會選這招")]
        [Min(0f)] public float minDistance;
        [Tooltip("玩家在這個水平距離 (m) 以內才會選這招；Infinity = 不限")]
        [Min(0f)] public float maxDistance = Mathf.Infinity;
        [Tooltip("冷卻（秒），從招式結束（破綻結束或被打斷）開始算，乘上當下階段的冷卻倍率")]
        [Min(0f)] public float cooldown = 5f;
        [Tooltip("加權隨機的權重；0 = 不會被隨機選到（開場序列與 F3 指定仍可以出）")]
        [Min(0f)] public float weight = 1f;
        [Tooltip("使用距離分段權重；關閉時沿用 weight")] public bool distanceWeights;
        [Min(0f), Tooltip("近距離上限 (m)")] public float nearDistance;
        [Min(0f), Tooltip("近距離權重")] public float nearWeight;
        [Min(0f), Tooltip("遠距離權重")] public float farWeight;
        public float WeightAt(float distance) => distanceWeights ? (distance <= nearDistance ? nearWeight : farWeight) : weight;

        [Header("時間")]
        [Tooltip("預兆時間（秒）")]
        [Min(0f)] public float telegraphTime = 1f;
        [Tooltip("出招後的破綻時間（秒）")]
        [Min(0f)] public float recoveryTime = 1f;

        [Header("攻擊")]
        [Tooltip("每一下的傷害")]
        [Min(0f)] public float damage = 10f;
        [Tooltip("可以用盾擋下（預兆閃白光）；不可擋閃紅光")]
        public bool canBlock = true;
        [Tooltip("可以盾反")]
        public bool canParry = true;

        public bool InRange(float distance) => DistanceToRange(distance) <= 0f;

        /// <summary>水平距離離這招的距離範圍還差多少 (m)；在範圍內為 0。</summary>
        public float DistanceToRange(float distance)
        {
            if (distance < minDistance) return minDistance - distance;
            if (distance > maxDistance) return distance - maxDistance;
            return 0f;
        }
    }
}
