using UnityEngine;

namespace BossFight.Cutscene
{
    /// <summary>
    /// 可以被 CurveTrack 驅動的演出物件（白屏、閃電、煙霧、鏡頭震動）。沒有片段在作用時收到 0。
    /// </summary>
    public abstract class CurveTarget : MonoBehaviour
    {
        /// <summary>套用這一幀的值；0 = 不顯示。</summary>
        public abstract void Apply(float value);
    }
}
