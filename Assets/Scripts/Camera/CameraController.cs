using BossFight.Core;
using BossFight.Player;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BossFight.Cameras
{
    /// <summary>
    /// Cinemachine 3 第三人稱鏡頭與鎖定 Boss。場景裡有兩台 CinemachineCamera，由 Main Camera 上的 CinemachineBrain 混合：
    /// 自由視角：Player/Look（滑鼠、右搖桿）直接推它 OrbitalFollow 的水平與垂直軸。
    /// 鎖定視角：Priority 比自由視角高時接手；每幀把水平軸轉到「玩家背後、朝向目標」，看向玩家與目標的 TargetGroup。
    /// 鎖定哪個目標由 PlayerController 決定（Player/LockOn），這裡只訂閱它的 OnLockTargetChanged 切換鏡頭。
    /// Esc 釋放游標，點擊畫面重新鎖住。重建鏡頭：Tools > Camera > Build Camera Rig。
    /// 演出期間 CutsceneDirector 呼叫 SetInputLocked 鎖住視角，Timeline 的 Cinemachine Track 接管畫面；
    /// 演出結束前先解除玩家的鎖定，再用 ResetBehind 把自由視角擺到玩家背後、面向 Boss，Timeline 混回來時就是這個角度。
    /// </summary>
    [DisallowMultipleComponent]
    public class CameraController : MonoBehaviour
    {
        [Tooltip("視角靈敏度")]
        [SerializeField] PlayerStats stats;
        [Tooltip("鏡頭跟隨的玩家；鎖定目標從它的 OnLockTargetChanged 來")]
        [SerializeField] PlayerController player;

        [Header("Cinemachine")]
        [SerializeField] CinemachineCamera freeLookCamera;
        [SerializeField] CinemachineOrbitalFollow freeLookOrbit;
        [SerializeField] CinemachineCamera lockOnCamera;
        [SerializeField] CinemachineOrbitalFollow lockOnOrbit;
        [Tooltip("鎖定鏡頭看向的群組：裡面放玩家，鎖定時由程式加入目標")]
        [SerializeField] CinemachineTargetGroup lockOnGroup;

        [Header("手感（純視覺）")]
        [Tooltip("鎖定時鏡頭轉向目標方向的平滑時間（秒）")]
        [Min(0f)] [SerializeField] float lockOnYawSmoothTime = 0.12f;

        [Header("游標")]
        [SerializeField] bool lockCursor = true;

        /// <summary>鏡頭目前鎖定的目標（跟著 PlayerController.OnLockTargetChanged）。</summary>
        public LockOnTarget CurrentTarget { get; private set; }
        /// <summary>演出中：不轉視角。</summary>
        public bool InputLocked { get; private set; }
        /// <summary>游標放開給 UI 用（勝敗畫面的按鈕）：點滑鼠不會再鎖回去。</summary>
        public bool CursorReleased { get; private set; }

        InputAction lookAction;
        float lockYaw, lockYawVelocity;
        Transform leavingMember; // 已解除鎖定、但鎖定鏡頭還在混出，暫時留在群組裡的目標
        int skipPointerFrames;

        void Awake()
        {
            var actions = InputSystem.actions;
            if (actions == null)
            {
                Debug.LogError("[Camera] 找不到 Project-wide Input Actions，請在 Project Settings > Input System Package 指定 InputSystem_Actions。", this);
                enabled = false;
                return;
            }
            lookAction = actions.FindAction("Player/Look", true);
            SetLockOnCameraLive(false);
        }

        void OnEnable()
        {
            lookAction?.actionMap.Enable();
            if (player != null) player.OnLockTargetChanged += SetTarget;
        }

        void Start()
        {
            if (lockCursor) SetCursorLocked(true);
        }

        void OnDisable()
        {
            if (player != null) player.OnLockTargetChanged -= SetTarget;
            if (lockCursor) SetCursorLocked(false);
        }

        void Update()
        {
            UpdateCursor();
            // 解除鎖定後，鎖定鏡頭混出完才把目標移出群組（見 SetTarget）
            if (leavingMember != null && !CinemachineCore.IsLive(lockOnCamera))
            {
                lockOnGroup.RemoveMember(leavingMember);
                leavingMember = null;
            }
            if (CurrentTarget != null) UpdateLockOnYaw();
            else if (!InputLocked) ApplyLook();
        }

        /// <summary>鎖住或解除視角的輸入（演出用；鎖定的輸入由 PlayerController.SetInputLocked 鎖）。</summary>
        public void SetInputLocked(bool locked)
        {
            InputLocked = locked;
            skipPointerFrames = locked ? 0 : 3; // 解除那幾幀累積的滑鼠位移不算
        }

        /// <summary>放開游標給 UI 點按鈕（勝敗畫面），期間點滑鼠不會鎖回去；false 時恢復（下次點畫面才鎖）。</summary>
        public void SetCursorReleased(bool released)
        {
            CursorReleased = released;
            if (released && lockCursor) SetCursorLocked(false);
        }

        /// <summary>把自由視角轉到玩家背後、面向 lookAt（水平），俯仰回到預設。鎖定中要先解除（PlayerController.ClearLockTarget）。</summary>
        public void ResetBehind(Vector3 lookAt)
        {
            Vector3 to = lookAt - player.transform.position;
            if (to.x * to.x + to.z * to.z > 1e-4f)
                freeLookOrbit.HorizontalAxis.Value = freeLookOrbit.HorizontalAxis.ClampValue(Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg);
            freeLookOrbit.VerticalAxis.Value = freeLookOrbit.VerticalAxis.Center;
        }

        void SetTarget(LockOnTarget target)
        {
            if (target == CurrentTarget) return;

            // 解除時先不移出群組：鎖定鏡頭還要混出一段時間，馬上移出的話群組只剩玩家（腳底），鏡頭會在混合中猛然往下看。
            // 等鎖定鏡頭完全不在畫面上（Update 裡）才移出
            if (CurrentTarget != null) leavingMember = CurrentTarget.AimPoint;
            if (target != null)
            {
                bool stillMember = leavingMember != null && leavingMember == target.AimPoint;
                if (leavingMember != null && !stillMember) lockOnGroup.RemoveMember(leavingMember);
                leavingMember = null;
                if (!stillMember) lockOnGroup.AddMember(target.AimPoint, 1f, target.FramingRadius);
                if (CurrentTarget == null)
                {
                    // 從自由視角目前的方向開始轉向目標，兩台鏡頭起點一致，混合時不會跳
                    lockYaw = freeLookOrbit.HorizontalAxis.Value;
                    lockYawVelocity = 0f;
                    lockOnOrbit.HorizontalAxis.Value = lockYaw;
                }
            }
            else
            {
                // 解除時自由視角從鎖定鏡頭目前的角度接手，不會跳回鎖定前的方向
                freeLookOrbit.HorizontalAxis.Value = lockOnOrbit.HorizontalAxis.Value;
                freeLookOrbit.VerticalAxis.Value = freeLookOrbit.VerticalAxis.ClampValue(lockOnOrbit.VerticalAxis.Value);
            }

            CurrentTarget = target;
            SetLockOnCameraLive(target != null);
        }

        void SetLockOnCameraLive(bool live)
        {
            lockOnCamera.Priority = freeLookCamera.Priority.Value + (live ? 1 : -1);
        }

        // 鏡頭轉到玩家背後、朝向目標
        void UpdateLockOnYaw()
        {
            Vector3 to = CurrentTarget.transform.position - player.transform.position;
            if (to.x * to.x + to.z * to.z < 1e-4f) return;
            float desired = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
            lockYaw = Mathf.SmoothDampAngle(lockYaw, desired, ref lockYawVelocity, lockOnYawSmoothTime);
            lockOnOrbit.HorizontalAxis.Value = lockOnOrbit.HorizontalAxis.ClampValue(lockYaw);
        }

        // 滑鼠以像素位移計（度/像素），搖桿以角速度計（度/秒）
        void ApplyLook()
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

            Vector2 delta = fromPointer ? v * stats.mouseSensitivity : v * (stats.stickSensitivity * Time.deltaTime);
            ref var h = ref freeLookOrbit.HorizontalAxis;
            ref var p = ref freeLookOrbit.VerticalAxis;
            h.Value = h.ClampValue(h.Value + delta.x);
            p.Value = p.ClampValue(p.Value + (stats.invertY ? delta.y : -delta.y));
        }

        void UpdateCursor()
        {
            if (!lockCursor || CursorReleased) return;
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
                SetCursorLocked(false);
            else if (mouse != null && mouse.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked)
                SetCursorLocked(true);
        }

        void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
            if (locked) skipPointerFrames = 3;
        }
    }
}
