using UnityEngine;
using UnityEngine.InputSystem;

namespace BossFight.Player
{
    /// <summary>
    /// 刀盾狗的操控。輸入讀 Project-wide Actions（Assets/InputSystem_Actions）的 Player map：
    /// Move 移動（相對鏡頭）、Sprint 衝刺、Jump 跳、Attack 揮刀（可接第二刀）、Block 舉盾。
    /// 動畫由 DaoDunDogAnimator 讀這裡的狀態來擺姿勢。
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [DisallowMultipleComponent]
    public class PlayerController : MonoBehaviour
    {
        [Tooltip("移動方向以這個鏡頭為準；留空則用 Camera.main")]
        public Transform cameraTransform;

        [Header("移動")]
        [Tooltip("跑步速度 (m/s)")]
        [Min(0f)] public float runSpeed = 6f;
        [Min(0f)] public float sprintSpeed = 9f;
        [Tooltip("加減速 (m/s²)")]
        [Min(0.1f)] public float acceleration = 40f;
        [Tooltip("轉身速度 (度/秒)")]
        [Min(0f)] public float turnSpeed = 720f;
        [Tooltip("舉盾時的移動速度倍率")]
        [Range(0f, 1f)] public float blockSpeedMultiplier = 0.4f;
        [Tooltip("揮刀時的移動速度倍率")]
        [Range(0f, 1f)] public float attackSpeedMultiplier = 0.2f;

        [Header("跳躍")]
        [Min(0f)] public float jumpHeight = 1.2f;
        public float gravity = -25f;

        [Header("揮刀")]
        [Min(0.1f)] public float attackDuration = 0.55f;
        [Tooltip("揮刀進度超過這個比例後按攻擊，會接著揮下一刀")]
        [Range(0f, 1f)] public float comboWindow = 0.45f;

        public bool IsGrounded { get; private set; } = true;
        public bool IsBlocking { get; private set; }
        public bool IsAttacking { get; private set; }
        /// <summary>目前這一刀的進度 0..1。</summary>
        public float AttackProgress => IsAttacking ? Mathf.Clamp01(attackTime / attackDuration) : 0f;
        /// <summary>0 = 斜劈，1 = 前刺，連段時交替。</summary>
        public int AttackIndex { get; private set; }
        public float PlanarSpeed { get; private set; }
        public float VerticalVelocity => verticalVelocity;

        CharacterController cc;
        InputAction moveAction, sprintAction, jumpAction, attackAction, blockAction;
        Vector3 planarVelocity;
        float verticalVelocity;
        float attackTime;
        bool comboQueued;

        void Awake()
        {
            cc = GetComponent<CharacterController>();

            var actions = InputSystem.actions;
            if (actions == null)
            {
                Debug.LogError("[Player] 找不到 Project-wide Input Actions，請在 Project Settings > Input System Package 指定 InputSystem_Actions。", this);
                enabled = false;
                return;
            }
            moveAction = actions.FindAction("Player/Move", true);
            sprintAction = actions.FindAction("Player/Sprint", true);
            jumpAction = actions.FindAction("Player/Jump", true);
            attackAction = actions.FindAction("Player/Attack", true);
            blockAction = actions.FindAction("Player/Block", true);
        }

        void OnEnable()
        {
            moveAction?.actionMap.Enable();
        }

        void Update()
        {
            float dt = Time.deltaTime;
            Vector2 input = Vector2.ClampMagnitude(moveAction.ReadValue<Vector2>(), 1f);

            // 鏡頭的水平前方與右方
            Transform cam = cameraTransform != null ? cameraTransform : (Camera.main != null ? Camera.main.transform : null);
            Vector3 camForward = cam != null ? Vector3.ProjectOnPlane(cam.forward, Vector3.up) : Vector3.zero;
            if (camForward.sqrMagnitude < 1e-4f) camForward = transform.forward;
            camForward.Normalize();
            Vector3 camRight = Vector3.Cross(Vector3.up, camForward);
            Vector3 wishDir = camForward * input.y + camRight * input.x;

            UpdateAttack(dt, wishDir);
            IsBlocking = blockAction.IsPressed() && !IsAttacking;

            // 水平移動
            bool sprinting = sprintAction.IsPressed() && !IsBlocking && !IsAttacking;
            float targetSpeed = sprinting ? sprintSpeed : runSpeed;
            if (IsBlocking) targetSpeed *= blockSpeedMultiplier;
            if (IsAttacking) targetSpeed *= attackSpeedMultiplier;
            planarVelocity = Vector3.MoveTowards(planarVelocity, wishDir * targetSpeed, acceleration * dt);

            // 轉向：舉盾時面向鏡頭前方（可以側移），其他時候面向移動方向
            Vector3 faceDir = IsBlocking ? camForward : wishDir;
            if (faceDir.sqrMagnitude > 1e-4f)
            {
                float turn = IsAttacking ? turnSpeed * 0.25f : turnSpeed;
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(faceDir), turn * dt);
            }

            // 重力與跳躍
            IsGrounded = cc.isGrounded;
            if (IsGrounded && verticalVelocity < 0f) verticalVelocity = -2f;
            if (IsGrounded && !IsAttacking && jumpAction.WasPressedThisFrame())
                verticalVelocity = Mathf.Sqrt(2f * jumpHeight * -gravity);
            verticalVelocity += gravity * dt;

            cc.Move((planarVelocity + Vector3.up * verticalVelocity) * dt);

            Vector3 v = cc.velocity;
            PlanarSpeed = new Vector2(v.x, v.z).magnitude;
        }

        void UpdateAttack(float dt, Vector3 wishDir)
        {
            if (attackAction.WasPressedThisFrame())
            {
                if (!IsAttacking) StartAttack(0, wishDir);
                else if (AttackProgress >= comboWindow) comboQueued = true;
            }

            if (!IsAttacking) return;
            attackTime += dt;
            if (attackTime < attackDuration) return;

            if (comboQueued) StartAttack(1 - AttackIndex, wishDir);
            else IsAttacking = false;
        }

        void StartAttack(int index, Vector3 wishDir)
        {
            IsAttacking = true;
            AttackIndex = index;
            attackTime = 0f;
            comboQueued = false;
            // 出刀瞬間轉向搖桿方向
            if (wishDir.sqrMagnitude > 1e-4f) transform.rotation = Quaternion.LookRotation(wishDir);
        }
    }
}
