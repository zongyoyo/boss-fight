using UnityEngine;

namespace BossFight.Core
{
    /// <summary>
    /// 可以被鏡頭鎖定的目標（Boss）。CameraController 在 Inspector 列出候選目標，按鎖定時選範圍內最近的一個。
    /// 物件停用或銷毀時視為不可鎖定，鏡頭會自動解除。
    /// 構圖資料跟著目標走（序章修正 B1–B3）：鏡頭看的點與框取半徑、鎖定鏡頭的俯視角度與距離；鎖定菱形與處決標記另外用 markerPoint。
    /// 大小差很多的目標（0.9 m 的小樹精、10 m 的大樹守衛）各自設定，不在鏡頭或場景上寫特例。全是純視覺參數。
    /// </summary>
    [DisallowMultipleComponent]
    public class LockOnTarget : MonoBehaviour
    {
        [Tooltip("鎖定鏡頭看的點（例如身體中心）；留空則用物件本身")]
        [SerializeField] Transform aimPoint;
        [Tooltip("鎖定菱形與處決標記的位置（例如大樹守衛的樹洞）；留空則用 aimPoint")]
        [SerializeField] Transform markerPoint;
        [Tooltip("鎖定鏡頭框住目標時用的半徑 (m)，純視覺")]
        [Min(0f)] [SerializeField] float framingRadius = 1f;
        [Tooltip("鎖定這個目標時鎖定鏡頭的俯視角度（度）；解除鎖定時自由視角回到鎖定前的角度")]
        [Range(-10f, 60f)] [SerializeField] float lockOnPitch = 12f;
        [Tooltip("鎖定這個目標時鎖定鏡頭離玩家的距離 (m)；0 = 用鎖定鏡頭原本的距離")]
        [Min(0f)] [SerializeField] float lockOnDistance;

        public Transform AimPoint => aimPoint != null ? aimPoint : transform;
        public Transform MarkerPoint => markerPoint != null ? markerPoint : AimPoint;
        public float FramingRadius => framingRadius;
        public float LockOnPitch => lockOnPitch;
        public float LockOnDistance => lockOnDistance;
        public bool IsLockable => isActiveAndEnabled;
    }
}
