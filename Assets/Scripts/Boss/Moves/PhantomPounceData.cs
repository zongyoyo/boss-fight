using UnityEngine;

namespace BossFight.Boss.Moves
{
    /// <summary>
    /// 幻影分身（第二階段新招，取代扭屁股飛撲）：預兆 telegraphTime 秒法陣閃爍、召喚 phantomCount 隻半透明幻影，
    /// 之後本體與幻影依序飛撲（每隻伏低 pounceWindup 秒，相鄰兩隻起跳相隔 pounceInterval 秒），每隻 damage；
    /// 本體落地後破綻 recoveryTime 秒。飛撲本身的數值（滯空、高度、落點、判定）沿用 ButtPounceData。
    /// 資產在 Assets/ScriptableObjects/Boss/Moves/Phase2/。
    /// </summary>
    [CreateAssetMenu(fileName = "PhantomPounce", menuName = "BossFight/Boss/Moves/Phantom Pounce")]
    public class PhantomPounceData : ButtPounceData
    {
        [Header("幻影分身")]
        [Tooltip("幻影數量（本體另外算）")]
        [Range(0, 4)] public int phantomCount = 2;
        [Tooltip("幻影出現的位置離本體多遠 (m)，平均分佈在本體左右兩側")]
        [Min(0f)] public float spawnDistance = 3f;
        [Tooltip("每一隻伏低扭屁股的時間（秒）")]
        [Min(0.05f)] public float pounceWindup = 0.6f;
        [Tooltip("相鄰兩隻起跳的間隔（秒）")]
        [Min(0.05f)] public float pounceInterval = 0.8f;
        [Tooltip("本體固定最後一個飛撲（幻影的順序隨機）；關掉時本體也排進隨機順序")]
        public bool realPouncesLast = true;
    }
}
