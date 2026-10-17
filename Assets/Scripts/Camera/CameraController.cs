using System;
using System.Collections.Generic;
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
    /// Player/LockOn（滑鼠中鍵）在 lockOnRange 內選最近的目標，再按一次解除；結果用 OnLockTargetChanged 通知。
    /// Esc 釋放游標，點擊畫面重新鎖住。重建鏡頭：Tools > Camera > Build Camera Rig。
    /// 演出期間 CutsceneDirector 呼叫 SetInputLocked 鎖住視角與鎖定，Timeline 的 Cinemachine Track 接管畫面；
    /// 演出結束前用 ResetBehind 把自由視角擺到玩家背後、面向 Boss，Timeline 混回來時就是這個角度。
    /// </summary>
    [DisallowMultipleComponent]
    public class CameraController : MonoBehaviour
    {
        [Tooltip("鎖定範圍與視角靈敏度")]
        [SerializeField] PlayerStats stats;
        [Tooltip("鏡頭跟隨的玩家；鎖定範圍從這裡量")]
        [SerializeField] Transform player;

        [Header("Cinemachine")]
        [SerializeField] CinemachineCamera freeLookCamera;
        [SerializeField] CinemachineOrbitalFollow freeLookOrbit;
        [SerializeField] CinemachineCamera lockOnCamera;
        [SerializeField] CinemachineOrbitalFollow lockOnOrbit;
        [Tooltip("鎖定鏡頭看向的群組：裡面放玩家，鎖定時由程式加入目標")]
        [SerializeField] CinemachineTargetGroup lockOnGroup;

        [Header("鎖定目標")]
        [Tooltip("可以被鎖定的目標（Boss）")]
        [SerializeField] List<LockOnTarget> lockTargets = new List<LockOnTarget>();

        [Header("手感（純視覺）")]
        [Tooltip("鎖定時鏡頭轉向目標方向的平滑時間（秒）")]
        [Min(0f)] [SerializeField] float lockOnYawSmoothTime = 0.12f;

        [Header("游標")]
        [SerializeField] bool lockCursor = true;

        /// <summary>鎖定目標改變；null 表示解除鎖定。</summary>
        public event Action<LockOnTarget> OnLockTargetChanged;

        public LockOnTarget CurrentTarget { get; private set; }
        /// <summary>演出中：不轉視角、不能鎖定。</summary>
        public bool InputLocked { get; private set; }

        InputAction lookAction, lockOnAction;
        float lockYaw, lockYawVelocity;
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
            lockOnAction = actions.FindAction("Player/LockOn", true);
            SetLockOnCameraLive(false);
        }

        void OnEnable()
        {
            lookAction?.actionMap.Enable();
        }

        void Start()
        {
            if (lockCursor) SetCursorLocked(true);
        }

        void OnDisable()
        {
            if (lockCursor) SetCursorLocked(false);
        }

        void Update()
        {
            UpdateCursor();

            // 目標被停用或銷毀（例如 Boss 倒下）就自動解除
            if (CurrentTarget != null && !CurrentTarget.IsLockable) SetTarget(null);
            if (!InputLocked && lockOnAction.WasPressedThisFrame()) SetTarget(CurrentTarget != null ? null : FindNearestTarget());

            if (CurrentTarget != null) UpdateLockOnYaw();
            else if (!InputLocked) ApplyLook();
        }

        /// <summary>鎖住或解除視角與鎖定的輸入（演出用）。</summary>
        public void SetInputLocked(bool locked)
        {
            InputLocked = locked;
            skipPointerFrames = locked ? 0 : 3; // 解除那幾幀累積的滑鼠位移不算
        }

        /// <summary>解除鎖定，把自由視角轉到玩家背後、面向 lookAt（水平），俯仰回到預設。</summary>
        public void ResetBehind(Vector3 lookAt)
        {
            if (CurrentTarget != null) SetTarget(null);
            Vector3 to = lookAt - player.position;
            if (to.x * to.x + to.z * to.z > 1e-4f)
                freeLookOrbit.HorizontalAxis.Value = freeLookOrbit.HorizontalAxis.ClampValue(Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg);
            freeLookOrbit.VerticalAxis.Value = freeLookOrbit.VerticalAxis.Center;
        }

        /// <summary>水平距離 lockOnRange 內最近的可鎖定目標；沒有則為 null。</summary>
        LockOnTarget FindNearestTarget()
        {
            LockOnTarget best = null;
            float bestSqr = stats.lockOnRange * stats.lockOnRange;
            foreach (var t in lockTargets)
            {
                if (t == null || !t.IsLockable) continue;
                Vector3 d = t.transform.position - player.position;
                float sqr = d.x * d.x + d.z * d.z;
                if (sqr > bestSqr) continue;
                best = t;
                bestSqr = sqr;
            }
            return best;
        }

        void SetTarget(LockOnTarget target)
        {
            if (target == CurrentTarget) return;

            if (CurrentTarget != null) lockOnGroup.RemoveMember(CurrentTarget.AimPoint);
            if (target != null)
            {
                lockOnGroup.AddMember(target.AimPoint, 1f, target.FramingRadius);
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
            OnLockTargetChanged?.Invoke(target);
        }

        void SetLockOnCameraLive(bool live)
        {
            lockOnCamera.Priority = freeLookCamera.Priority.Value + (live ? 1 : -1);
        }

        // 鏡頭轉到玩家背後、朝向目標
        void UpdateLockOnYaw()
        {
            Vector3 to = CurrentTarget.transform.position - player.position;
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
            if (!lockCursor) return;
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
