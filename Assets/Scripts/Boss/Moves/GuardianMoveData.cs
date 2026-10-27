using UnityEngine;
using BossFight.Hazards;

namespace BossFight.Boss.Moves
{
    /// <summary>守衛招式共用的範圍與判定數值；階段差異使用各自資產，執行期唯讀。</summary>
    public class GuardianMoveData : BossMoveData
    {
        [Header("招式表現")]
        [Tooltip("只決定藤蔓／落葉／樹根的可見效果，不參與傷害結算")]
        public AreaStrikeStyle visualStyle = AreaStrikeStyle.Root;
        [Header("判定")]
        [Min(0f), Tooltip("每段判定持續秒數")] public float hitWindow = 0.15f;
        [Min(0f), Tooltip("判定高度 (m)")] public float hitHeight = 2f;
        [Min(0f), Tooltip("半徑 (m)")] public float radius;
        [Min(0f), Tooltip("內半徑／起始半徑 (m)")] public float innerRadius;
        [Min(0f), Tooltip("扇形角度")] public float angle;
        [Min(0f), Tooltip("直線長度 (m)")] public float reach;
        [Min(0f), Tooltip("直線或環帶寬 (m)")] public float width;
        [Min(0f), Tooltip("左右線偏移 (m)")] public float sideOffset;
        [Min(1), Tooltip("段數／數量")] public int count = 1;
        [Min(0f), Tooltip("各段開始間隔 (s)")] public float interval;
        [Min(0f), Tooltip("中途每段地面預兆 (s)")] public float warning;
        [Min(0f), Tooltip("飛行／擴散速度 (m/s)")] public float speed;
        [Min(0f), Tooltip("玩家附近的取樣半徑 (m)")] public float scatter;
        [Min(0), Tooltip("玩家附近的數量")] public int nearCount;
        [Min(0f), Tooltip("葉刃中心高度 (m)")] public float centerHeight;
        [Min(0f), Tooltip("萬根第一波預兆 (s)")] public float outerWarning;
        [Min(0f), Tooltip("萬根第二波預兆 (s)")] public float innerWarning;
        [Min(0f), Tooltip("萬根第三波預兆 (s)")] public float crossWarning;
        [Min(0f), Tooltip("萬根追蹤鎖點提前量 (s)")] public float sampleLead;
        [Min(0f), Tooltip("萬根最後一波預兆 (s)")] public float finalWarning;
        [Min(1), Tooltip("最後一波安全圈數")] public int safeCount = 3;
        [Min(0f), Tooltip("最後一波安全圈半徑 (m)")] public float safeRadius;
        [Min(0f), Tooltip("可達安全圈搜尋距離 (m)")] public float reachableDistance;
    }
}
