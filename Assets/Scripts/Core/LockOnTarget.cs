using UnityEngine;

namespace BossFight.Core
{
    /// <summary>
    /// 可以被鏡頭鎖定的目標（Boss）。CameraController 在 Inspector 列出候選目標，按鎖定時選範圍內最近的一個。
    /// 物件停用或銷毀時視為不可鎖定，鏡頭會自動解除。
    /// </summary>
    [DisallowMultipleComponent]
    public class LockOnTarget : MonoBehaviour
    {
        [Tooltip("鏡頭與鎖定標記對準的點（例如身體中心）；留空則用物件本身")]
        [SerializeField] Transform aimPoint;
        [Tooltip("鎖定鏡頭框住目標時用的半徑 (m)，純視覺")]
        [Min(0f)] [SerializeField] float framingRadius = 1f;

        public Transform AimPoint => aimPoint != null ? aimPoint : transform;
        public float FramingRadius => framingRadius;
        public bool IsLockable => isActiveAndEnabled;
    }
}
