using System;
using BossFight.Cameras;
using BossFight.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BossFight.Player
{
    /// <summary>
    /// 刀盾狗的移動、翻滾與舉盾。數值全部讀 PlayerStats；輸入讀 Project-wide Actions（Assets/InputSystem_Actions）的 Player map：
    /// Move 移動（相對鏡頭）、Roll 翻滾、Block 舉盾。
    /// 鎖定目標來自 CameraController.OnLockTargetChanged：鎖定時角色面向目標，前後是靠近／遠離，左右是繞著目標走。
    /// 動畫由 DaoDunDogAnimator 讀這裡的狀態來擺姿勢。
    /// </summary>
    [RequireComponent(typeof(CharacterController), typeof(PlayerStamina))]
    [DisallowMultipleComponent]
    public class PlayerController : MonoBehaviour
    {
        [SerializeField] PlayerStats stats;
        [Tooltip("移動方向以這個鏡頭為準（Main Camera）")]
        [SerializeField] Transform cameraTransform;
        [Tooltip("鎖定目標的來源")]
        [SerializeField] CameraController cameraController;

        /// <summary>翻滾開始；參數是翻滾方向（水平單位向量）。</summary>
        public event Action<Vector3> OnRollStarted;
        public event Action OnRollEnded;

        public bool IsGrounded { get; private set; } = true;
        public bool IsBlocking { get; private set; }
        public bool IsRolling { get; private set; }
        /// <summary>翻滾開始後的 rollInvulnerableTime 秒內無敵。</summary>
        public bool IsInvulnerable => IsRolling && rollTime < stats.rollInvulnerableTime;
        /// <summary>目前這次翻滾的進度 0..1。</summary>
        public float RollProgress => IsRolling ? Mathf.Clamp01(rollTime / stats.rollDuration) : 0f;
        /// <summary>目前（或上一次）翻滾的方向，水平單位向量。</summary>
        public Vector3 RollDirection { get; private set; }
        public float MoveSpeed => stats.moveSpeed;
        public float PlanarSpeed { get; private set; }
        public LockOnTarget LockTarget { get; private set; }

        CharacterController cc;
        PlayerStamina stamina;
        InputAction moveAction, rollAction, blockAction;
        Vector3 planarVelocity;
        float verticalVelocity;
        float rollTime, rollTravelled;

        void Awake()
        {
            cc = GetComponent<CharacterController>();
            stamina = GetComponent<PlayerStamina>();

            var actions = InputSystem.actions;
            if (actions == null)
            {
                Debug.LogError("[Player] 找不到 Project-wide Input Actions，請在 Project Settings > Input System Package 指定 InputSystem_Actions。", this);
                enabled = false;
                return;
            }
            moveAction = actions.FindAction("Player/Move", true);
            rollAction = actions.FindAction("Player/Roll", true);
            blockAction = actions.FindAction("Player/Block", true);
        }

        void OnEnable()
        {
            moveAction?.actionMap.Enable();
            if (cameraController != null) cameraController.OnLockTargetChanged += HandleLockTargetChanged;
        }

        void OnDisable()
        {
            if (cameraController != null) cameraController.OnLockTargetChanged -= HandleLockTargetChanged;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            Vector2 input = Vector2.ClampMagnitude(moveAction.ReadValue<Vector2>(), 1f);
            GetMoveFrame(out Vector3 forward, out Vector3 right);
            Vector3 wishDir = forward * input.y + right * input.x;

            if (!IsRolling && rollAction.WasPressedThisFrame()) TryStartRoll(wishDir);

            IsBlocking = !IsRolling && blockAction.IsPressed();
            stamina.Regenerate(dt, IsBlocking);

            Vector3 delta;
            if (IsRolling)
            {
                delta = RollStep(dt);
            }
            else
            {
                float speed = stats.moveSpeed * (IsBlocking ? stats.blockMoveSpeedMultiplier : 1f);
                planarVelocity = Vector3.MoveTowards(planarVelocity, wishDir * speed, stats.acceleration * dt);
                delta = planarVelocity * dt;
                if (LockTarget != null) delta = OrbitAroundTarget(delta);
            }

            UpdateFacing(wishDir, dt);

            // 重力：著地時歸零再加一幀的量，讓 CharacterController 持續貼地
            IsGrounded = cc.isGrounded;
            if (IsGrounded && verticalVelocity < 0f) verticalVelocity = 0f;
            verticalVelocity += stats.gravity * dt;

            cc.Move(delta + Vector3.up * (verticalVelocity * dt));

            Vector3 v = cc.velocity;
            PlanarSpeed = new Vector2(v.x, v.z).magnitude;

            if (IsRolling && rollTime >= stats.rollDuration) EndRoll();
        }

        // 鎖定時以「玩家 → 目標」為前方，否則以鏡頭的水平前方為準
        void GetMoveFrame(out Vector3 forward, out Vector3 right)
        {
            if (LockTarget != null) forward = Flat(LockTarget.transform.position - transform.position);
            else forward = cameraTransform != null ? Flat(cameraTransform.forward) : Vector3.zero;
            if (forward.sqrMagnitude < 1e-4f) forward = Flat(transform.forward);
            forward.Normalize();
            right = Vector3.Cross(Vector3.up, forward);
        }

        void TryStartRoll(Vector3 wishDir)
        {
            if (!stamina.TrySpend(stats.rollStaminaCost)) return;

            // 沒有方向輸入時往角色面向翻滾
            Vector3 dir = wishDir.sqrMagnitude > 1e-4f ? Flat(wishDir) : Flat(transform.forward);
            RollDirection = dir.normalized;
            IsRolling = true;
            rollTime = 0f;
            rollTravelled = 0f;
            planarVelocity = Vector3.zero;

            // 沒鎖定時直接轉向翻滾方向；鎖定時保持面向目標（側滾、後滾）
            if (LockTarget == null) transform.rotation = Quaternion.LookRotation(RollDirection);
            OnRollStarted?.Invoke(RollDirection);
        }

        // 依位移曲線算出這一幀該走的距離，總和剛好是 rollDistance（沒撞到東西的話）
        Vector3 RollStep(float dt)
        {
            rollTime += dt;
            float t = Mathf.Clamp01(rollTime / stats.rollDuration);
            float travelled = stats.rollCurve.Evaluate(t) * stats.rollDistance;
            Vector3 delta = RollDirection * (travelled - rollTravelled);
            rollTravelled = travelled;
            return delta;
        }

        void EndRoll()
        {
            IsRolling = false;
            OnRollEnded?.Invoke();
        }

        // 鎖定時把切線方向的位移換成繞目標的圓弧，半徑只隨前後分量改變；不然一直往側邊走會越繞越遠
        Vector3 OrbitAroundTarget(Vector3 delta)
        {
            Vector3 offset = Flat(transform.position - LockTarget.transform.position);
            float r = offset.magnitude;
            if (r < 1e-3f) return delta;

            Vector3 radialDir = offset / r;
            float radial = Vector3.Dot(delta, radialDir);
            Vector3 tangential = delta - radialDir * radial;
            Vector3 newDir = (offset + tangential).normalized;
            return newDir * Mathf.Max(0f, r + radial) - offset;
        }

        // 鎖定：面向目標；翻滾：已在起滾時轉好；舉盾：面向鏡頭前方（可以側移）；其他：面向移動方向
        void UpdateFacing(Vector3 wishDir, float dt)
        {
            Vector3 face;
            if (LockTarget != null) face = Flat(LockTarget.transform.position - transform.position);
            else if (IsRolling) face = RollDirection;
            else if (IsBlocking && cameraTransform != null) face = Flat(cameraTransform.forward);
            else face = wishDir;

            if (face.sqrMagnitude > 1e-4f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(face), stats.turnSpeed * dt);
        }

        void HandleLockTargetChanged(LockOnTarget target)
        {
            LockTarget = target;
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
