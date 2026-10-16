using UnityEngine;
using UnityEngine.InputSystem;

namespace BossFight.Cameras
{
    /// <summary>
    /// 第三人稱環繞鏡頭。讀 Player/Look：滑鼠以像素位移計，搖桿以角速度計。
    /// 鏡頭被牆擋住時拉近。Esc 釋放游標，點擊畫面重新鎖定。
    /// </summary>
    [DisallowMultipleComponent]
    public class ThirdPersonCamera : MonoBehaviour
    {
        public Transform target;

        [Header("位置")]
        [Tooltip("繞著目標的這個點旋轉（目標的局部高度）")]
        public Vector3 pivotOffset = new Vector3(0f, 1.4f, 0f);
        [Min(0.5f)] public float distance = 5.5f;
        [Tooltip("跟隨的平滑度，越大越緊")]
        [Min(0f)] public float followSharpness = 18f;

        [Header("旋轉")]
        [Tooltip("滑鼠：度 / 像素")]
        [Min(0f)] public float mouseSensitivity = 0.12f;
        [Tooltip("搖桿：度 / 秒")]
        [Min(0f)] public float stickSensitivity = 200f;
        public bool invertY;
        public Vector2 pitchLimits = new Vector2(-30f, 70f);
        public float startPitch = 15f;

        [Header("碰撞")]
        [Min(0f)] public float collisionRadius = 0.25f;
        public LayerMask collisionMask = ~0;

        [Header("游標")]
        public bool lockCursor = true;

        InputAction lookAction;
        float yaw, pitch, currentDistance;
        Vector3 pivot;
        int skipPointerFrames;
        readonly RaycastHit[] hits = new RaycastHit[16];

        void Awake()
        {
            lookAction = InputSystem.actions?.FindAction("Player/Look");
        }

        void Start()
        {
            pitch = startPitch;
            currentDistance = distance;
            if (target != null)
            {
                yaw = target.eulerAngles.y;
                pivot = target.position + pivotOffset;
            }
            if (lockCursor) SetCursorLocked(true);
        }

        void Update()
        {
            if (!lockCursor) return;
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
                SetCursorLocked(false);
            else if (mouse != null && mouse.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked)
                SetCursorLocked(true);
        }

        void LateUpdate()
        {
            if (target == null) return;
            float dt = Time.deltaTime;

            if (lookAction != null)
            {
                Vector2 v = lookAction.ReadValue<Vector2>();
                bool fromPointer = lookAction.activeControl?.device is Pointer;
                if (fromPointer && lockCursor && Cursor.lockState != CursorLockMode.Locked) v = Vector2.zero;
                // 鎖定游標時會把游標瞬移到畫面中央，那一下的位移不能當成轉鏡頭
                if (skipPointerFrames > 0)
                {
                    skipPointerFrames--;
                    if (fromPointer) v = Vector2.zero;
                }
                Vector2 delta = fromPointer ? v * mouseSensitivity : v * (stickSensitivity * dt);
                yaw += delta.x;
                pitch = Mathf.Clamp(pitch + (invertY ? delta.y : -delta.y), pitchLimits.x, pitchLimits.y);
            }

            Vector3 desiredPivot = target.position + pivotOffset;
            pivot = followSharpness > 0f
                ? Vector3.Lerp(pivot, desiredPivot, 1f - Mathf.Exp(-followSharpness * dt))
                : desiredPivot;

            Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 back = rot * Vector3.back;

            // 被牆擋住就拉近；之後再慢慢退回
            float allowed = distance;
            int n = Physics.SphereCastNonAlloc(pivot, collisionRadius, back, hits, distance, collisionMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                if (hits[i].collider.transform.IsChildOf(target)) continue;
                allowed = Mathf.Min(allowed, hits[i].distance);
            }
            currentDistance = allowed < currentDistance
                ? allowed
                : Mathf.Lerp(currentDistance, allowed, 1f - Mathf.Exp(-6f * dt));

            transform.SetPositionAndRotation(pivot + back * currentDistance, rot);
        }

        void OnDisable()
        {
            if (lockCursor) SetCursorLocked(false);
        }

        void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
            if (locked) skipPointerFrames = 3;
        }
    }
}
