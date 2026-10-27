using System;
using System.Collections.Generic;
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
        /// <summary>處決頭暈的目標（F），細節在 PlayerAttack（IsExecution）。</summary>
        Execute,
        GuardBreak,
        Dead,
    }

    /// <summary>
    /// 刀盾狗的移動、跳躍、翻滾、舉盾、攻擊、啃骨頭與處決。數值全部讀 PlayerStats；輸入讀 Project-wide Actions（Assets/InputSystem_Actions）的 Player map：
    /// Move 移動（相對鏡頭）、Jump 跳躍、Roll 翻滾、Block 舉盾、Attack 攻擊（點擊輕攻擊、長按重擊）、Heal 啃骨頭、Execute 處決、LockOn 鎖定。
    /// 鎖定由這裡決定（玩法的一部分）：LockOn 在 Inspector 列出的 LockOnTarget 裡選 lockOnRange 內最近的一個，再按一次解除，目標不能鎖定時自動解除；
    /// 結果用 OnLockTargetChanged 送出，CameraController 訂閱它切換鎖定鏡頭。鎖定時角色面向目標，前後是靠近／遠離，左右是繞著目標走。
    /// 處決：Inspector 列出的 ExecutionTarget 裡，executeRange 內最近、可以處決的一個（Boss 頭暈中）；站在地上、沒有動作、蓄力中都可以按，
    /// 揮刀中按會先記下來，到可取消的時間才接。轉向目標劈出 executeAttack，整段不能取消，executeInvulnerable 時無敵。
    /// 揮刀交給 PlayerAttack；受傷結算在 PlayerHealth，這裡訂閱它的事件處理蓄力中斷、破防與倒地。
    /// 演出期間（開場、轉階段）由 CutsceneDirector 呼叫 SetInputLocked 鎖住輸入：不讀任何按鍵，只照 ScriptedMoveTo 給的位置走（重力照常）。
    /// 動畫由 DaoDunDogAnimator 讀這裡的狀態來擺姿勢。
    /// </summary>
    [RequireComponent(typeof(CharacterController), typeof(PlayerStamina))]
    [RequireComponent(typeof(PlayerHealth), typeof(PlayerAttack))]
    [DisallowMultipleComponent]
    public class PlayerController : MonoBehaviour
    {
        [SerializeField] PlayerStats stats;

        // 場景物件，由 PlayerSpawner 生成角色後經 PlayerRig.SetSceneReferences 填入（T18）
        Transform cameraTransform; // 移動方向以這個鏡頭為準（Main Camera）
        readonly List<LockOnTarget> lockTargets = new List<LockOnTarget>(); // 按鎖定時選 lockOnRange 內最近的一個
        readonly List<ExecutionTarget> executionTargets = new List<ExecutionTarget>(); // 按處決時選 executeRange 內最近、現在可以處決的一個

        /// <summary>翻滾開始；參數是翻滾方向（水平單位向量）。</summary>
        public event Action<Vector3> OnRollStarted;
        public event Action OnRollEnded;
        public event Action OnJumped;
        /// <summary>鎖定目標改變；null 表示解除鎖定。</summary>
        public event Action<LockOnTarget> OnLockTargetChanged;
        /// <summary>動作改變；參數是新的動作。連擊接下一段不算改變（看 PlayerAttack.OnAttackStarted）。</summary>
        public event Action<PlayerAction> OnActionChanged;

        public PlayerAction Action { get; private set; }
        public bool IsGrounded { get; private set; } = true;
        public bool IsBlocking { get; private set; }
        /// <summary>這次舉盾已經舉了多久（秒）；沒舉盾為 0。盾反判定用。</summary>
        public float BlockHeldTime => IsBlocking ? Time.time - blockStartTime : 0f;
        public bool IsRolling => Action == PlayerAction.Roll;
        public bool IsExecuting => Action == PlayerAction.Execute;
        public bool IsDead => Action == PlayerAction.Dead;
        /// <summary>翻滾開始後的 rollInvulnerableTime（加上難度修正）秒內、處決中（executeInvulnerable）無敵；受擊後的無敵在 PlayerHealth。</summary>
        public bool IsInvulnerable => (IsRolling && rollTime < stats.rollInvulnerableTime + Difficulty.rollInvulnerableBonus) || (IsExecuting && stats.executeInvulnerable);
        /// <summary>難度修正（BattleManager 開場前用 SetDifficulty 設定）；翻滾無敵與 PlayerHealth 的盾反判定讀數值時加上去。</summary>
        public DifficultyModifiers Difficulty { get; private set; } = DifficultyModifiers.None;
        /// <summary>正在處決的目標；沒有處決時為 null。</summary>
        public ExecutionTarget ExecutingTarget { get; private set; }
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
        /// <summary>目前讀的數值（PlayerSpawner 套用角色定義與修正後的複本，T19）。</summary>
        public PlayerStats Stats => stats;
        public float PlanarSpeed { get; private set; }
        public LockOnTarget LockTarget { get; private set; }
        /// <summary>演出中：不讀輸入，只照 ScriptedMoveTo 走。</summary>
        public bool InputLocked { get; private set; }

        CharacterController cc;
        PlayerStamina stamina;
        PlayerHealth health;
        PlayerAttack attack;
        InputAction moveAction, jumpAction, rollAction, blockAction, attackAction, healAction, executeAction, lockOnAction;
        Vector3 planarVelocity;
        float verticalVelocity;
        float rollTime, rollTravelled;
        float blockStartTime;
        float actionTime, healed;
        bool jumpRequested, bufferedAttack, bufferedRoll, bufferedExecute;
        Vector3 scriptedTarget, scriptedFace;
        bool hasScriptedTarget;
        Vector3 damagePushDirection;
        float damagePushDistance, damagePushDuration, damagePushTime, damagePushTravelled;

        /// <summary>移動方向以這個鏡頭為準（Main Camera）；PlayerSpawner 生成後設定。</summary>
        public void SetCameraTransform(Transform value) => cameraTransform = value;

        /// <summary>可以被鎖定的目標（Boss）；PlayerSpawner 生成後設定。</summary>
        public void SetLockTargets(IEnumerable<LockOnTarget> targets) => Fill(lockTargets, targets);

        /// <summary>可以處決的目標（Boss）；PlayerSpawner 生成後設定。</summary>
        public void SetExecutionTargets(IEnumerable<ExecutionTarget> targets) => Fill(executionTargets, targets);

        static void Fill<T>(List<T> list, IEnumerable<T> items) where T : UnityEngine.Object
        {
            list.Clear();
            if (items == null) return;
            foreach (var item in items)
            {
                if (item != null) list.Add(item);
            }
        }

        /// <summary>換成另一份數值（PlayerRig.ApplyStats 呼叫；角色定義加上修正後的複本）。</summary>
        public void SetStats(PlayerStats value)
        {
            if (value != null) stats = value;
        }

        /// <summary>套用難度修正（不改 PlayerStats，讀數值時才加）；null = 不修正。</summary>
        public void SetDifficulty(DifficultyModifiers modifiers) => Difficulty = modifiers ?? DifficultyModifiers.None;

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
            executeAction = actions.FindAction("Player/Execute", true);
            lockOnAction = actions.FindAction("Player/LockOn", true);
        }

        void OnEnable()
        {
            moveAction?.actionMap.Enable();
            health.OnDamageTaken += HandleDamageTaken;
            health.OnDied += HandleDied;
        }

        void OnDisable()
        {
            damagePushDistance = 0f;
            health.OnDamageTaken -= HandleDamageTaken;
            health.OnDied -= HandleDied;
            ReleaseExecutionTarget();
        }

        /// <summary>解除鎖定（演出結束前把視角擺回玩家背後時用）。</summary>
        public void ClearLockTarget() => SetLockTarget(null);

        /// <summary>
        /// 鎖住或解除輸入（演出用）。鎖住時清掉預約的輸入、中斷蓄力；正在做的動作（翻滾、揮刀）照常做完。鎖住期間不能切換鎖定。
        /// 解除時停止照腳本走；照腳本走的速度（跳過演出時一幀被推開好幾公尺）不帶進操作，免得解除後繼續滑。
        /// </summary>
        public void SetInputLocked(bool locked)
        {
            InputLocked = locked;
            jumpRequested = bufferedAttack = bufferedRoll = bufferedExecute = false;
            if (locked && Action == PlayerAction.Charge) SetAction(PlayerAction.None);
            if (!locked && hasScriptedTarget)
            {
                hasScriptedTarget = false;
                planarVelocity = Vector3.zero;
            }
        }

        /// <summary>輸入鎖住時，這一幀往 position（只看水平）走過去，面向前進方向；由 Timeline 每幀給。</summary>
        public void ScriptedMoveTo(Vector3 position) => ScriptedMoveTo(position, Vector3.zero);

        /// <summary>同上，但面向 face（水平方向；零向量 = 面向前進方向），例如被衝擊波推開時面向 Boss 往後滑。</summary>
        public void ScriptedMoveTo(Vector3 position, Vector3 face)
        {
            scriptedTarget = position;
            scriptedFace = Flat(face);
            hasScriptedTarget = true;
        }

        /// <summary>直接瞬移到 position、轉向 rotation（演出開始、跳過時用）。</summary>
        public void Teleport(Vector3 position, Quaternion rotation)
        {
            damagePushDistance = 0f;
            cc.enabled = false;
            transform.SetPositionAndRotation(position, rotation);
            cc.enabled = true;
            planarVelocity = Vector3.zero;
            verticalVelocity = 0f;
            scriptedTarget = position;
        }

        void Update()
        {
            UpdateLockOn();
            float dt = Time.deltaTime;
            Vector2 input = IsDead || InputLocked ? Vector2.zero : Vector2.ClampMagnitude(moveAction.ReadValue<Vector2>(), 1f);
            GetMoveFrame(out Vector3 forward, out Vector3 right);
            Vector3 wishDir = forward * input.y + right * input.x;
            Vector3 scriptedDelta = Vector3.zero;
            if (InputLocked && hasScriptedTarget)
            {
                scriptedDelta = Flat(scriptedTarget - transform.position);
                wishDir = scriptedDelta.sqrMagnitude > 1e-6f ? scriptedDelta.normalized : Vector3.zero;
            }

            if (!IsDead && !InputLocked) HandleActionInput(wishDir);

            bool blocking = !InputLocked && Action == PlayerAction.None && IsGrounded && blockAction.IsPressed();
            if (blocking && !IsBlocking) blockStartTime = Time.time;
            IsBlocking = blocking;
            stamina.Regenerate(dt, IsBlocking, BlockHeldTime);

            Vector3 delta;
            switch (Action)
            {
                case PlayerAction.None:
                    if (InputLocked)
                    {
                        // 演出：Timeline 已經算好這一幀該在哪，直接走過去
                        delta = scriptedDelta;
                        planarVelocity = dt > 0f ? scriptedDelta / dt : Vector3.zero;
                        break;
                    }
                    float speed = stats.moveSpeed * (IsBlocking ? stats.blockMoveSpeedMultiplier : 1f);
                    planarVelocity = Vector3.MoveTowards(planarVelocity, wishDir * speed, stats.acceleration * dt);
                    delta = planarVelocity * dt;
                    if (LockTarget != null) delta = OrbitAroundTarget(delta);
                    break;
                case PlayerAction.Roll:
                    delta = RollStep(dt);
                    break;
                case PlayerAction.Attack:
                case PlayerAction.Execute:
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
                // 擊退是傷害鏈的可選資料；鎖輸入時也照走，不改相機或別人的輸入鎖。
                Vector3 push = DamagePushStep(dt);
                delta += push;
                if (InputLocked && hasScriptedTarget) scriptedTarget += push;
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

        // 每個動作能接受的輸入。揮刀中的左鍵、翻滾、處決會先記下來，到 cancelTime 才執行
        void HandleActionInput(Vector3 wishDir)
        {
            bool attackPressed = attackAction.WasPressedThisFrame();
            bool rollPressed = rollAction.WasPressedThisFrame();
            bool executePressed = executeAction.WasPressedThisFrame();

            switch (Action)
            {
                case PlayerAction.None:
                    if (!IsGrounded) break; // 空中只能移動
                    if (executePressed && TryStartExecution()) break;
                    if (rollPressed) TryStartRoll(wishDir);
                    else if (jumpAction.WasPressedThisFrame()) TryJump();
                    else if (healAction.WasPressedThisFrame()) TryStartHeal();
                    else if (attackPressed && !blockAction.IsPressed()) StartCharge(wishDir);
                    break;

                case PlayerAction.Charge:
                    // 處決、翻滾、舉盾取消蓄力；滿 heavyChargeTime 前放開左鍵就是輕攻擊第 1 段
                    if (executePressed && TryStartExecution()) break;
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
                    if (executePressed) bufferedExecute = true;
                    if (!attack.CanCancel) break;
                    if (bufferedExecute && TryStartExecution()) break;
                    if (bufferedRoll && stamina.Current >= stats.rollStaminaCost)
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
                    if (bufferedExecute && TryStartExecution()) break;
                    if (bufferedAttack && HasNextCombo) StartLightAttack(attack.ComboIndex + 1, Flat(transform.forward));
                    else
                    {
                        attack.Stop();
                        SetAction(PlayerAction.None);
                    }
                    break;
                case PlayerAction.Execute:
                    if (!attack.IsFinished) break;
                    attack.Stop();
                    SetAction(PlayerAction.None, true); // 完整播完才標記成功結束，放開目標在 SetAction
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

        void SetAction(PlayerAction next, bool executionCompleted = false)
        {
            actionTime = 0f;
            if (next == Action) return;
            var prev = Action;
            Action = next;
            if (next != PlayerAction.None) IsBlocking = false;
            if (prev == PlayerAction.Roll) OnRollEnded?.Invoke();
            if (prev == PlayerAction.Execute) ReleaseExecutionTarget(executionCompleted);
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
            // 耐力要夠一次的消耗才能滾（翻滾中照常回復，用「> 0 就能滾」會一直滾下去）
            if (!stamina.TrySpendFull(stats.rollStaminaCost)) return;

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
            bufferedAttack = bufferedRoll = bufferedExecute = false;
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
            bufferedAttack = bufferedRoll = bufferedExecute = false;
            attack.Begin(stats.heavyAttack, 0, true);
            SetAction(PlayerAction.Attack);
        }

        /// <summary>executeRange 內最近、現在可以處決的目標；沒有時為 null。</summary>
        public ExecutionTarget FindExecutionTarget()
        {
            ExecutionTarget best = null;
            float bestSqr = stats.executeRange * stats.executeRange;
            foreach (var target in executionTargets)
            {
                if (target == null || !target.IsExecutable || target.IsBeingExecuted) continue;
                float sqr = Flat(target.transform.position - transform.position).sqrMagnitude;
                if (sqr > bestSqr) continue;
                best = target;
                bestSqr = sqr;
            }
            return best;
        }

        // 轉向目標劈出處決；目標在處決動作結束前保持可被處決（Boss 的頭暈時間到也先不結束）
        bool TryStartExecution()
        {
            var target = FindExecutionTarget();
            if (target == null || !target.TryBeginExecution()) return false;

            Vector3 face = Flat(target.transform.position - transform.position);
            if (face.sqrMagnitude > 1e-4f) transform.rotation = Quaternion.LookRotation(face);
            bufferedAttack = bufferedRoll = bufferedExecute = false;
            planarVelocity = Vector3.zero;
            attack.BeginExecution(stats.executeAttack);
            SetAction(PlayerAction.Execute);
            ExecutingTarget = target;
            return true;
        }

        // 處決動作結束（打完、倒地、元件停用）時通知目標
        void ReleaseExecutionTarget(bool completed = false)
        {
            var target = ExecutingTarget;
            ExecutingTarget = null;
            if (target != null) target.EndExecution(completed);
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

        // 處決：面向處決的目標；鎖定：面向目標；翻滾：已在起滾時轉好；舉盾：面向鏡頭前方（可以側移）；移動、蓄力：面向輸入方向。
        // 揮刀時只有鎖定才會跟著目標轉；啃骨頭、破防、倒地都不轉
        void UpdateFacing(Vector3 wishDir, float dt)
        {
            bool canTurn = Action == PlayerAction.None || Action == PlayerAction.Roll || Action == PlayerAction.Charge
                           || (Action == PlayerAction.Attack && LockTarget != null) || IsExecuting;
            if (!canTurn) return;

            Vector3 face;
            if (IsExecuting) face = ExecutingTarget != null ? Flat(ExecutingTarget.transform.position - transform.position) : Vector3.zero;
            else if (LockTarget != null) face = Flat(LockTarget.transform.position - transform.position);
            else if (IsRolling) face = RollDirection;
            else if (InputLocked && hasScriptedTarget && scriptedFace.sqrMagnitude > 1e-4f) face = scriptedFace;
            else if (IsBlocking && cameraTransform != null) face = Flat(cameraTransform.forward);
            else face = wishDir;

            if (face.sqrMagnitude > 1e-4f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(face), stats.turnSpeed * dt);
        }

        void HandleDamageTaken(PlayerDamageReport report)
        {
            if (IsDead) return;
            if (report.result == DamageResult.Hit && report.info.pushDistance > 0f)
            {
                Vector3 away = Flat(transform.position - report.info.sourcePosition);
                damagePushDirection = away.sqrMagnitude > 1e-6f ? away.normalized : -transform.forward;
                damagePushDistance = report.info.pushDistance;
                damagePushDuration = Mathf.Max(0f, report.info.pushDuration);
                damagePushTime = damagePushTravelled = 0f;
            }
            if (report.result == DamageResult.Hit && Action == PlayerAction.Charge) SetAction(PlayerAction.None); // 蓄力中被打會中斷
            else if (report.result == DamageResult.GuardBroken) SetAction(PlayerAction.GuardBreak);
        }

        void HandleDied()
        {
            damagePushDistance = 0f;
            attack.Stop();
            SetAction(PlayerAction.Dead);
        }

        // 目標被停用或銷毀（例如 Boss 倒下）就自動解除；演出中不能切換
        void UpdateLockOn()
        {
            if (LockTarget != null && !LockTarget.IsLockable) SetLockTarget(null);
            if (!InputLocked && lockOnAction.WasPressedThisFrame()) SetLockTarget(LockTarget != null ? null : FindNearestLockTarget());
        }

        /// <summary>水平距離 lockOnRange 內最近的可鎖定目標；沒有則為 null。</summary>
        LockOnTarget FindNearestLockTarget()
        {
            LockOnTarget best = null;
            float bestSqr = stats.lockOnRange * stats.lockOnRange;
            foreach (var t in lockTargets)
            {
                if (t == null || !t.IsLockable) continue;
                float sqr = Flat(t.transform.position - transform.position).sqrMagnitude;
                if (sqr > bestSqr) continue;
                best = t;
                bestSqr = sqr;
            }
            return best;
        }

        void SetLockTarget(LockOnTarget target)
        {
            if (target == LockTarget) return;
            LockTarget = target;
            OnLockTargetChanged?.Invoke(target);
        }

        Vector3 DamagePushStep(float dt)
        {
            if (damagePushDistance <= 0f) return Vector3.zero;
            damagePushTime += dt;
            float t = damagePushDuration > 0f ? Mathf.Clamp01(damagePushTime / damagePushDuration) : 1f;
            float travelled = damagePushDistance * Mathf.SmoothStep(0f, 1f, t);
            Vector3 step = damagePushDirection * (travelled - damagePushTravelled);
            damagePushTravelled = travelled;
            if (t >= 1f) damagePushDistance = 0f;
            return step;
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
