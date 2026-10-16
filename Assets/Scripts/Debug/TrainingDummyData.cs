#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;

namespace BossFight.DebugTools
{
    /// <summary>
    /// T2 測試木樁的數值：輪流放出的攻擊、出招節奏與範圍。執行期只讀不寫；
    /// 資產在 Assets/ScriptableObjects/Debug/TrainingDummyData.asset。
    /// </summary>
    [CreateAssetMenu(fileName = "TrainingDummyData", menuName = "BossFight/Debug/Training Dummy Data")]
    public class TrainingDummyData : ScriptableObject
    {
        [System.Serializable]
        public struct DummyAttack
        {
            [Tooltip("顯示在木樁上方的名稱")]
            public string label;
            [Min(0f)] public float damage;
            [Tooltip("可以用盾擋下（預兆白光）；不可擋為紅光")]
            public bool canBlock;
            public bool canParry;
            [Tooltip("命中時給玩家的失衡值（玩家沒有失衡，通常是 0）")]
            [Min(0f)] public float imbalance;
            [Tooltip("預兆時間（秒），結束的那一刻命中")]
            [Min(0f)] public float telegraphTime;
            [Tooltip("攻擊判定持續時間（秒）")]
            [Min(0.01f)] public float activeTime;
        }

        [Header("出招")]
        [Tooltip("依序輪流使用")]
        public DummyAttack[] attacks =
        {
            new DummyAttack
            {
                label = "白光：可擋、可盾反", damage = 15f, canBlock = true, canParry = true, telegraphTime = 1f, activeTime = 0.1f,
            },
            new DummyAttack
            {
                label = "紅光：不可擋，翻滾閃開", damage = 20f, canBlock = false, canParry = false, telegraphTime = 1f, activeTime = 0.1f,
            },
        };
        [Tooltip("進 Play 模式後到第一招預兆的等待（秒）")]
        [Min(0f)] public float startDelay = 2f;
        [Tooltip("一招結束到下一招預兆開始（秒）")]
        [Min(0f)] public float attackInterval = 2.5f;

        [Header("攻擊範圍")]
        [Tooltip("以木樁為中心的球形範圍半徑 (m)")]
        [Min(0f)] public float attackRadius = 2.5f;
        [Tooltip("球心離地高度 (m)")]
        [Min(0f)] public float attackCenterHeight = 0.8f;

        [Header("傷害顯示")]
        [Tooltip("兩下間隔超過這個時間（秒）就重新累計連擊傷害")]
        [Min(0f)] public float comboResetTime = 1.5f;
        [Tooltip("傷害數字停留時間（秒）")]
        [Min(0.1f)] public float popupLifetime = 1.2f;
    }
}
#endif
