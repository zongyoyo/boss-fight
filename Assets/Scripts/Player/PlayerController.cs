using System;
using BossFight.Cameras;
using BossFight.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BossFight.Player
{
    /// <summary>玩家目前在做的動作，同一時間只有一個。舉盾不算動作：沒有動作、站在地上時按住右鍵就是舉盾。</summary>
    public enum PlayerAction
    {
        None,
        Roll,
        /// <summary>揮刀（輕攻擊或重擊），細節在 PlayerAttack。</summary>
        Attack,
        /// <summary>按住左鍵蓄力，滿 heavyChargeTime 劈出重擊。</summary>
        Charge,
        /// <summary>啃骨頭回血。</summary>
        Heal,
        GuardBreak,
        Dead,
    }

    /// <summary>
    /// 刀盾狗的移動、跳躍、翻滾、舉盾、攻擊與啃骨頭。數值全部讀 PlayerStats；輸入讀 Project-wide Actions（Assets/InputSystem_Actions）的 Player map：
    /// Move 移動（相對鏡頭）、Jump 跳躍、Roll 翻滾、Block 舉盾、Attack 攻擊（點擊輕攻擊、長按重擊）、Heal 啃骨頭。
    /// 鎖定目標來自 CameraController.OnLockTargetChanged：鎖定時角色面向目標，前後是靠近／遠離，左右是繞著目標走。
    /// 揮刀交給 PlayerAttack；受傷結算在 PlayerHealth，這裡訂閱它的事件處理蓄力中斷、破防與倒地。
    /// 動畫由 DaoDunDogAnimator 讀這裡的狀態來擺姿勢。
    /// </summary>
    [RequireComponent(typeof(CharacterController), typeof(PlayerStamina))]
    [RequireComponent(typeof(PlayerHealth), typeof(PlayerAttack))]
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
        public event Action OnJumped;
        /// <summary>動作改變；參數是新的動作。連擊接下一段不算改變（看 PlayerAttack.OnAttackStarted）。</summary>
        public event Action<PlayerAction> OnActionChanged;

        public PlayerAction Action { get; private set; }
        public bool IsGrounded { get; private set; } = true;
        public bool IsBlocking { get; private set; }
        /// <summary>這次舉盾已經舉了多久（秒）；沒舉盾為 0。盾反判定用。</summary>
        public float BlockHeldTime => IsBlocking ? Time.time - blockStartTime : 0f;
        public bool IsRolling => Action == PlayerAction.Roll;
        public bool IsDead => Action == PlayerAction.Dead;
        /// <summary>翻滾開始後的 rollInvulnerableTime 秒內無敵（受擊後的無敵在 PlayerHealth）。</summary>
        public bool IsInvulnerable => IsRolling && rollTime < stats.rollInvulnerableTime;
        /// <summary>目前這次翻滾的進度 0..1。</summary>
        public float RollProgress => IsRolling ? Mathf.Clamp01(rollTime / stats.rollDuration) : 0f;
        /// <summary>目前（或上一次）翻滾的方向，水平單位向量。</summary>
        public Vector3 RollDirection { get; private set; }
        /// <summary>蓄力進度 0..1；滿了就劈出重擊。</summary>
        public float ChargeProgress => Action == PlayerAction.Charge ? Mathf.Clamp01(actionTime / stats.heavyChargeTime) : 0f;
        /// <summary>啃骨頭進度 0..1。</summary>
        public float HealProgress => Action == PlayerAction.Heal ? Mathf.Clamp01(actionTime / stats.boneDuration) : 0f;
        /// <summary>破防剩餘時間（秒）。</summary>
        public float GuardBreakRemaining => Action == PlayerAction.GuardBreak ? Mathf.Max(0f, stats.guardBreakDuration - actionTime) : 0f;
        public float MoveSpeed => stats.moveSpeed;
        public float PlanarSpeed { get; private set; }
        public LockOnTarget LockTarget { get; private set; }

        CharacterController cc;
        PlayerStamina stamina;
        PlayerHealth health;
        PlayerAttack attack;
        InputAction moveAction, jumpAction, rollAction, blockAction, attackAction, healAction;
        Vector3 planarVelocity;
        float verticalVelocity;
        float rollTime, rollTravelled;
        float blockStartTime;
        float actionTime, healed;
        bool jumpRequested, bufferedAttack, bufferedRoll;

        void Awake()
        {
            cc = GetComponent<CharacterController>();
            stamina = GetComponent<PlayerStamina>();
            health = GetComponent<PlayerHealth>();
            attack = GetComponent<PlayerAttack>();

            var actions = InputSystem.actions;
            if (actions == null)
            {
                Debug.LogError("[Player] 找不到 Project-wide Input Actions，請在 Project Settings > Input System Package 指定 InputSystem_Actions。", this);
                enabled = false;
                return;
            }
            moveAction = actions.FindAction("Player/Move", true);
            jumpAction = actions.FindAction("Player/Jump", true);
            rollAction = actions.FindAction("Player/Roll", true);
            blockAction = actions.FindAction("Player/Block", true);
            attackAction = actions.FindAction("Player/Attack", true);
            healAction = actions.FindAction("Player/Heal", true);
        }

        void OnEnable()
        {
            moveAction?.actionMap.Enable();
            if (cameraController != null) cameraController.OnLockTargetChanged += HandleLockTargetChanged;
            health.OnDamageTaken += HandleDamageTaken;
            health.OnDied += HandleDied;
        }

        void OnDisable()
        {
            if (cameraController != null) cameraController.OnLockTargetChanged -= HandleLockTargetChanged;
            health.OnDamageTaken -= HandleDamageTaken;
            health.OnDied -= HandleDied;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            Vector2 input = IsDead ? Vector2.zero : Vector2.ClampMagnitude(moveAction.ReadValue<Vector2>(), 1f);
            GetMoveFrame(out Vector3 forward, out Vector3 right);
            Vector3 wishDir = forward * input.y + right * input.x;

            if (!IsDead) HandleActionInput(wishDir);

            bool blocking = Action == PlayerAction.None && IsGrounded && blockAction.IsPressed();
            if (blocking && !IsBlocking) blockStartTime = Time.time;
            IsBlocking = blocking;
            stamina.Regenerate(dt, IsBlocking);

            Vector3 delta;
            switch (Action)
            {
                case PlayerAction.None:
                    float speed = stats.moveSpeed * (IsBlocking ? stats.blockMoveSpeedMultiplier : 1f);
                    planarVelocity = Vector3.MoveTowards(planarVelocity, wishDir * speed, stats.acceleration * dt);
                    delta = planarVelocity * dt;
                    if (LockTarget != null) delta = OrbitAroundTarget(delta);
                    break;
                case PlayerAction.Roll:
                    delta = RollStep(dt);
                    break;
                case PlayerAction.Attack:
                    planarVelocity = Vector3.zero;
                    delta = attack.Tick(dt);
                    break;
                default:
                    // 蓄力、啃骨頭、破防、倒地：原地不動
                    planarVelocity = Vector3.zero;
                    delta = Vector3.zero;
                    break;
            }

            UpdateFacing(wishDir, dt);

            // 畫面停頓（timeScale = 0）時不動 CharacterController：Move 位移為 0 時 isGrounded 會變成 false，舉盾跟著掉
            if (dt > 0f)
            {
                // 重力：著地時歸零再加一幀的量，讓 CharacterController 持續貼地
                IsGrounded = cc.isGrounded;
                if (IsGrounded && verticalVelocity < 0f) verticalVelocity = 0f;
                if (jumpRequested)
                {
                    jumpRequested = false;
                    verticalVelocity = Mathf.Sqrt(2f * Mathf.Abs(stats.gravity) * stats.jumpHeight);
                    OnJumped?.Invoke();
                }
                verticalVelocity += stats.gravity * dt;

                cc.Move(delta + Vector3.up * (verticalVelocity * dt));

                Vector3 v = cc.velocity;
                PlanarSpeed = new Vector2(v.x, v.z).magnitude;
            }

            UpdateActionTimers(dt);
        }

        // 每個動作能接受的輸入。揮刀中的左鍵、翻滾會先記下來，到 cancelTime 才執行
        void HandleActionInput(Vector3 wishDir)
        {
            bool attackPressed = attackAction.WasPressedThisFrame();
            bool rollPressed = rollAction.WasPressedThisFrame();

            switch (Action)
            {
                case PlayerAction.None:
                    if (!IsGrounded) break; // 空中只能移動
                    if (rollPressed) TryStartRoll(wishDir);
                    else if (jumpAction.WasPressedThisFrame()) TryJump();
                    else if (healAction.WasPressedThisFrame()) TryStartHeal();
                    else if (attackPressed && !blockAction.IsPressed()) StartCharge(wishDir);
                    break;

                case PlayerAction.Charge:
                    // 翻滾、舉盾取消蓄力；滿 heavyChargeTime 前放開左鍵就是輕攻擊第 1 段
                    if (rollPressed)
                    {
                        SetAction(PlayerAction.None);
                        TryStartRoll(wishDir);
                    }
                    else if (blockAction.WasPressedThisFrame()) SetAction(PlayerAction.None);
                    else if (!attackAction.IsPressed()) StartLightAttack(0, wishDir);
                    break;

                case PlayerAction.Attack:
                    if (attackPressed) bufferedAttack = true;
                    if (rollPressed) bufferedRoll = true;
                    if (!attack.CanCancel) break;
                    if (bufferedRoll && stamina.Current > 0f)
                    {
                        attack.Stop();
                        SetAction(PlayerAction.None);
                        TryStartRoll(wishDir);
                    }
                    else if (blockAction.IsPressed())
                    {
                        attack.Stop();
                        SetAction(PlayerAction.None);
                    }
                    else if (bufferedAttack && HasNextCombo) StartLightAttack(attack.ComboIndex + 1, wishDir);
                    break;
            }
        }

        void UpdateActionTimers(float dt)
        {
            switch (Action)
            {
                case PlayerAction.Roll:
                    if (rollTime >= stats.rollDuration) EndRoll();
                    break;
                case PlayerAction.Attack:
                    if (!attack.IsFinished) break;
                    if (bufferedAttack && HasNextCombo) StartLightAttack(attack.ComboIndex + 1, Flat(transform.forward));
                    else
                    {
                        attack.Stop();
                        SetAction(PlayerAction.None);
                    }
                    break;
                case PlayerAction.Charge:
                    actionTime += dt;
                    if (actionTime >= stats.heavyChargeTime) ReleaseHeavy();
                    break;
                case PlayerAction.Heal:
                    TickHeal(dt);
                    break;
                case PlayerAction.GuardBreak:
                    actionTime += dt;
                    if (actionTime >= stats.guardBreakDuration) SetAction(PlayerAction.None);
                    break;
            }
        }

        void SetAction(PlayerAction next)
        {
            actionTime = 0f;
            if (next == Action) return;
            var prev = Action;
            Action = next;
            if (next != PlayerAction.None) IsBlocking = false;
            if (prev == PlayerAction.Roll) OnRollEnded?.Invoke();
            OnActionChanged?.Invoke(next);
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

        void TryJump()
        {
            if (stats.jumpStaminaCost > 0f && !stamina.TrySpend(stats.jumpStaminaCost)) return;
            jumpRequested = true;
        }

        void TryStartRoll(Vector3 wishDir)
        {
            if (!stamina.TrySpend(stats.rollStaminaCost)) return;

            // 沒有方向輸入時往角色面向翻滾
            Vector3 dir = wishDir.sqrMagnitude > 1e-4f ? Flat(wishDir) : Flat(transform.forward);
            RollDirection = dir.normalized;
            SetAction(PlayerAction.Roll);
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

        // OnRollEnded 在 SetAction 離開翻滾時送出
        void EndRoll() => SetAction(PlayerAction.None);

        void StartCharge(Vector3 wishDir)
        {
            FaceForAttack(wishDir);
            SetAction(PlayerAction.Charge);
        }

        bool HasNextCombo => !attack.IsHeavy && attack.ComboIndex + 1 < stats.lightCombo.Length;

        void StartLightAttack(int index, Vector3 wishDir)
        {
            if (stats.lightCombo == null || index >= stats.lightCombo.Length)
            {
                SetAction(PlayerAction.None);
                return;
            }
            FaceForAttack(wishDir);
            bufferedAttack = bufferedRoll = false;
            attack.Begin(stats.lightCombo[index], index, false);
            SetAction(PlayerAction.Attack);
        }

        // 蓄滿才扣耐力；耐力是 0 就不出招，要重新按左鍵
        void ReleaseHeavy()
        {
            if (!stamina.TrySpend(stats.heavyStaminaCost))
            {
                SetAction(PlayerAction.None);
                return;
            }
            bufferedAttack = bufferedRoll = false;
            attack.Begin(stats.heavyAttack, 0, true);
            SetAction(PlayerAction.Attack);
        }

        // 出手瞬間轉向：鎖定時對準目標，否則轉向輸入方向（沒輸入就維持面向）
        void FaceForAttack(Vector3 wishDir)
        {
            Vector3 face = LockTarget != null ? Flat(LockTarget.transform.position - transform.position) : Flat(wishDir);
            if (face.sqrMagnitude > 1e-4f) transform.rotation = Quaternion.LookRotation(face);
        }

        void TryStartHeal()
        {
            if (!health.TryUseBone()) return;
            healed = 0f;
            SetAction(PlayerAction.Heal);
        }

        // 回血量隨時間線性增加，動作結束時剛好回滿 boneHealAmount
        void TickHeal(float dt)
        {
            actionTime += dt;
            float total = stats.boneHealAmount * Mathf.Clamp01(actionTime / stats.boneDuration);
            health.Heal(total - healed);
            healed = total;
            if (actionTime >= stats.boneDuration) SetAction(PlayerAction.None);
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

        // 鎖定：面向目標；翻滾：已在起滾時轉好；舉盾：面向鏡頭前方（可以側移）；移動、蓄力：面向輸入方向。
        // 揮刀時只有鎖定才會跟著目標轉；啃骨頭、破防、倒地都不轉
        void UpdateFacing(Vector3 wishDir, float dt)
        {
            bool canTurn = Action == PlayerAction.None || Action == PlayerAction.Roll || Action == PlayerAction.Charge
                           || (Action == PlayerAction.Attack && LockTarget != null);
            if (!canTurn) return;

            Vector3 face;
            if (LockTarget != null) face = Flat(LockTarget.transform.position - transform.position);
            else if (IsRolling) face = RollDirection;
            else if (IsBlocking && cameraTransform != null) face = Flat(cameraTransform.forward);
            else face = wishDir;

            if (face.sqrMagnitude > 1e-4f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(face), stats.turnSpeed * dt);
        }

        void HandleDamageTaken(PlayerDamageReport report)
        {
            if (IsDead) return;
            if (report.result == DamageResult.Hit && Action == PlayerAction.Charge) SetAction(PlayerAction.None); // 蓄力中被打會中斷
            else if (report.result == DamageResult.GuardBroken) SetAction(PlayerAction.GuardBreak);
        }

        void HandleDied()
        {
            attack.Stop();
            SetAction(PlayerAction.Dead);
        }

        void HandleLockTargetChanged(LockOnTarget target)
        {
            LockTarget = target;
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
