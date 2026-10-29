using UnityEngine;

namespace BossFight.Core
{
    /// <summary>
    /// 攻擊預兆的分類色，所有 Boss 與戰鬥敵人共用一份（ScriptableObjects/Feedback/TelegraphPalette.asset）：
    /// 白色 = 可格擋（能否盾反看招式資料），紅色 = 不可格擋、不可盾反（要閃），綠色 = 安全區、安全指引。
    /// 地面範圍（GroundTelegraph）與模型閃光（BossTelegraphFlash、TrainingSaplingVisual）各一組；預兆元件都讀這一份，不自己存顏色。
    /// 純視覺；改了立刻生效，不用重建 prefab。
    /// </summary>
    [CreateAssetMenu(menuName = "BossFight/Feedback/Telegraph Palette", fileName = "TelegraphPalette")]
    public class TelegraphPaletteData : ScriptableObject
    {
        [Header("地面範圍（HDR，A 是整體不透明度）")]
        [Tooltip("可格擋的地面預兆")]
        [ColorUsage(true, true)] public Color groundBlockable = new Color(1f, 1f, 1f, 0.9f);
        [Tooltip("不可格擋的地面預兆")]
        [ColorUsage(true, true)] public Color groundUnblockable = new Color(1f, 0.12f, 0.08f, 0.95f);
        [Tooltip("安全區、安全指引（萬根齊發的安全圈與事前指引）")]
        [ColorUsage(true, true)] public Color groundSafe = new Color(0.2f, 2f, 0.6f, 0.7f);

        [Header("模型閃光（Emission，亮度由各元件乘上去）")]
        [Tooltip("可格擋的閃光")]
        [ColorUsage(false, true)] public Color flashBlockable = Color.white;
        [Tooltip("不可格擋的閃光")]
        [ColorUsage(false, true)] public Color flashUnblockable = new Color(1f, 0.15f, 0.1f);

        public Color Ground(bool blockable) => blockable ? groundBlockable : groundUnblockable;
        public Color Flash(bool blockable) => blockable ? flashBlockable : flashUnblockable;

        static TelegraphPaletteData fallback;

        /// <summary>沒有接資產時用的預設值（欄位的預設），並在 Console 提醒一次。</summary>
        public static TelegraphPaletteData Resolve(TelegraphPaletteData palette, Object context)
        {
            if (palette != null) return palette;
            if (fallback == null)
            {
                fallback = CreateInstance<TelegraphPaletteData>();
                fallback.hideFlags = HideFlags.DontSave;
                Debug.LogWarning("[Telegraph] 預兆元件沒有接 TelegraphPalette，先用預設顏色（Tools > Feedback > Build Combat Feedback 會接上）。", context);
            }
            return fallback;
        }
    }
}
