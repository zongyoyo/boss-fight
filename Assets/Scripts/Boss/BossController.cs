using System;
using System.Collections.Generic;
using BossFight.Core;
using UnityEngine;

namespace BossFight.Boss
{
    /// <summary>
    /// Boss AI 的狀態。Idle → ChooseMove → Telegraph → Active → Recovery → Idle 是每一招的循環；
    /// Stunned、Transition、Defeated 會打斷循環。
    /// </summary>
    public enum BossState
    {
        /// <summary>出招間隔：轉向玩家，等階段的出招間隔。</summary>
        Idle,
        /// <summary>選招；沒有能出的招時在這裡走位或等冷卻。</summary>
        ChooseMove,
        /// <summary>預兆。</summary>
        Telegraph,
        /// <summary>出招。</summary>
        Active,
        /// <summary>破綻。</summary>
        Recovery,
        /// <summary>頭暈（失衡值滿，或招式指定的固定頭暈）：不出招、受傷加倍、可以被處決。</summary>
        Stunned,
        /// <summary>轉階段：無敵、不出招。</summary>
        Transition,
        /// <summary>血量歸零。</summary>
        Defeated,
    }

    /// <summary>這一招是怎麼選出來的。</summary>
    public enum MoveChoiceSource
    {
        /// <summary>Debug 指定（F3）。</summary>
        Forced,
        /// <summary>階段的固定開場序列。</summary>
        Opening,
        /// <summary>依距離、冷卻篩選後加權隨機。</summary>
        Random,
        /// <summary>走位逾時（BossPhaseData.repositionTimeout）：沒走進距離範圍，直接出正在走向的那一招。</summary>
        Timeout,
        /// <summary>下一階段的進場招式（BossPhaseData.entryMove，例如血量首次 ≤ 400 的 OIIA 大迴旋）。</summary>
        PhaseEntry,
    }

    /// <summary>選招的結果，OnMoveChosen 送出。</summary>
    public readonly struct BossMoveChoice
    {
        public readonly BossMove move;
        public readonly MoveChoiceSource source;
        /// <summary>加權隨機時的候選數；其他來源為 0。</summary>
        public readonly int candidateCount;
        /// <summary>選招時和玩家的水平距離 (m)。</summary>
        public readonly float distance;

        public BossMoveChoice(BossMove move, MoveChoiceSource source, int candidateCount, float distance)
        {
            this.move = move;
            this.source = source;
            this.candidateCount = candidateCount;
            this.distance = distance;
        }
    }

    /// <summary>一次預兆，OnMoveTelegraph 送出：招式開頭的預兆，或招式中途下一下的預兆（例如三連拍第三下改成不可擋）。</summary>
    public readonly struct MoveTelegraph
    {
        public readonly BossMove move;
        /// <summary>這一下可以擋（白光）；不可擋為紅光。</summary>
        public readonly bool canBlock;
        /// <summary>預兆幾秒後出手。</summary>
        public readonly float duration;

        public MoveTelegraph(BossMove move, bool canBlock, float duration)
        {
            this.move = move;
            this.canBlock = canBlock;
            this.duration = duration;
        }
    }

    /// <summary>招式的衝擊時刻（打擊回饋的音效與鏡頭震動用）。</summary>
    public enum BossImpactKind
    {
        /// <summary>貓掌拍下去（每一下）。</summary>
        PawSwipe,
        /// <summary>三連拍第三下（帶電）。</summary>
        PawFinisher,
        /// <summary>迴旋衝撞撞牆。</summary>
        WallSlam,
        /// <summary>扭屁股飛撲（或幻影分身的本體）落地。</summary>
        PounceLanding,
        /// <summary>幻影落地。</summary>
        PhantomLanding,
        /// <summary>放出一圈衝擊波。</summary>
        Shockwave,
        /// <summary>一道落雷劈下。</summary>
        Lightning,
        /// <summary>龍捲風成形。</summary>
        Tornado,
        /// <summary>大迴旋落地。</summary>
        GrandSpinLanding,
    }

    /// <summary>一次招式的衝擊，OnMoveImpact 送出。</summary>
    public readonly struct BossImpact
    {
        public readonly BossImpactKind kind;
        /// <summary>發生的位置（世界座標）。</summary>
        public readonly Vector3 position;

        public BossImpact(BossImpactKind kind, Vector3 position)
        {
            this.kind = kind;
            this.position = position;
        }
    }

    /// <summary>
    /// OIIA 貓的 AI 狀態機與選招，持有目前階段的 BossPhaseData。數值讀 BossStats／BossPhaseData／BossMoveData；
    /// 冷卻、開場序列進度這些執行期狀態存在元件自己身上。
    /// 選招順序：F3 指定（什麼都不看）→ 階段的固定開場序列（不看冷卻、權重、不連出，但要走進距離範圍）→
    /// 目前招式池裡「距離內、冷卻好、不是上一招、權重 &gt; 0」的招式加權隨機。
    /// 沒有能出的招時，往「冷卻好、不是上一招」裡距離範圍最近的一招走位（靠近或後退）；全部都在冷卻就原地轉向玩家等。
    /// 連續走位超過階段的 repositionTimeout 秒還走不到（Boss 比玩家慢，玩家一直拉開或貼身就永遠走不到）：
    /// 開場序列跳過這一招，一般選招直接出正在走向的那一招。
    /// 冷卻從招式結束（或被打斷）開始算，乘上當下階段的冷卻倍率。
    /// 血量降到下一個階段的門檻時換階段：有 transitionTime 的進入 Transition（無敵、打斷招式、清掉 Hazard），結束後才套用；沒有的直接套用。
    /// 下一階段有進場招式（BossPhaseData.entryMove，狂暴的 OIIA 大迴旋）時：打斷目前的動作（頭暈也一樣）、清掉 Hazard，立刻出那一招（MoveChoiceSource.PhaseEntry），
    /// 那一招結束、回到 Idle 時（它讓 Boss 頭暈的話是頭暈結束時）才套用新階段；PendingPhase 在這段期間也是新階段。
    /// 轉階段的結束：有轉階段演出時由 BattleManager 關掉 AutoCompleteTransition，演出結束時呼叫 CompleteTransition；沒有演出時 transitionTime 秒後自動結束。
    /// 頭暈（Stun）打斷目前的招式，時間到回到 Idle；失衡值的累積、受傷倍率與處決在同一個物件上的 BossStagger，
    /// 它在處決進行中設 HoldStun（時間到也先不結束），處決打到時呼叫 EndStun。
    /// 轉階段、倒下時收掉場上所有招式放出的 Hazard（ClearAllHazards）。
    /// 招式的執行交給 BossMove；這裡只送事件，UI 與演出訂閱 OnStateChanged、OnMoveChosen、OnMoveTelegraph、OnPhaseChanged、OnMoveHit、OnMoveImpact。
    /// </summary>
    [RequireComponent(typeof(BossHealth))]
    [DisallowMultipleComponent]
    public class BossController : MonoBehaviour
    {
        [SerializeField] BossStats stats;
        [Tooltip("場地半徑：走位不會走出場地")]
        [SerializeField] ArenaData arena;
        [Tooltip("場地中心；留空用世界原點")]
        [SerializeField] Transform arenaCenter;
        [Tooltip("本體的碰撞膠囊，用它的半徑讓 Boss 不會走進牆裡")]
        [SerializeField] CapsuleCollider body;
        [Tooltip("Boss 會用的招式（Moves 底下的 BossMove）；用哪些由 BossMoveData.phase 與階段的招式池決定")]
        [SerializeField] List<BossMove> moves = new List<BossMove>();
        [Tooltip("進 Play 模式就開打（場景裡沒有 BattleManager 時測試用）。平常關掉，由 BattleManager 在開場動畫結束時呼叫 StartBattle")]
        [SerializeField] bool startOnPlay;

        // 玩家：選招的距離與轉向都以它為準；PlayerSpawner 生成角色後用 SetTarget 設定（Boss 不引用 Player）
        Transform target;

        /// <summary>狀態改變；參數是新的狀態。</summary>
        public event Action<BossState> OnStateChanged;
        /// <summary>選好一招，接著進入預兆。</summary>
        public event Action<BossMoveChoice> OnMoveChosen;
        /// <summary>預兆開始：進入 Telegraph 時送一次（BossMoveData.canBlock），招式中途的下一下由 BossMove.Telegraph 再送。預兆閃光訂閱。</summary>
        public event Action<MoveTelegraph> OnMoveTelegraph;
        /// <summary>套用新的階段；Start 時也會送一次第一階段。</summary>
        public event Action<BossPhaseData> OnPhaseChanged;
        /// <summary>招式的攻擊判定打到目標並結算完（命中火花、Debug 面板）。盾反的失衡值走 PlayerHealth.OnParry，不在這裡加。</summary>
        public event Action<BossMove, HitReport> OnMoveHit;
        /// <summary>招式的衝擊時刻：拍下去、撞牆、落地、放出衝擊波、落雷劈下（BossImpactFeedback 的音效與鏡頭震動）。打不打到人都會送。</summary>
        public event Action<BossImpact> OnMoveImpact;
        /// <summary>正常完成整招（包含破綻）；中斷不發送。</summary>
        public event Action<BossMove> OnMoveFinished;
        /// <summary>外部流程接管階段條件；預設維持既有血量門檻。</summary>
        public bool AutomaticPhaseTransitions { get; set; } = true;
        public bool CombatSuspended { get; private set; }
        /// <summary>上層提供受擊範圍與翻滾狀態，Boss 不引用 Player。</summary>
        public Collider TargetHurtbox { get; set; }
        public float TargetBodyRadius { get; set; }
        public bool TargetRolling { get; set; }
        /// <summary>上層提供的閃避回饋資格；只影響回饋紀錄，不介入傷害判定。</summary>
        public Func<bool> TargetDodgeEligible { get; set; }
        public event Action<Vector3> OnAttackAvoided;
        public void NotifyAttackAvoided(Vector3 position) => OnAttackAvoided?.Invoke(position);
        /// <summary>清場並停住戰鬥，供演出與外部結局使用。</summary>
        public void SuspendCombat()
        {
            CombatSuspended = true;
            InterruptMove(); ClearAllHazards(); ForcedNextMove = null;
            SetState(BossState.Idle);
        }
        /// <summary>立即打斷普通招、清場並出指定招；頭暈由呼叫方等結束後請求。</summary>
        public bool StartScriptedMove(BossMove move)
        {
            if (!HasStarted || State == BossState.Stunned || State == BossState.Defeated || move == null || !moves.Contains(move)) return false;
            InterruptMove(); ClearAllHazards(); ForcedNextMove = null; CombatSuspended = false;
            StartMove(new BossMoveChoice(move, MoveChoiceSource.Forced, 0, DistanceToTarget));
            return true;
        }
        /// <summary>由外部流程請求既有階段轉場；階段完成仍走 CompleteTransition。</summary>
        public bool RequestPhaseTransition(int index)
        {
            if (!HasStarted || index < 0 || index >= stats.phases.Length || State == BossState.Defeated || State == BossState.Stunned) return false;
            CombatSuspended = false;
            BeginTransition(index);
            return true;
        }

        public BossState State { get; private set; }
        /// <summary>進入目前狀態後經過的秒數。</summary>
        public float StateTime { get; private set; }
        public BossPhaseData Phase { get; private set; }
        public int PhaseIndex { get; private set; }
        /// <summary>下一個階段；已經是最後一個時為 null。</summary>
        public BossPhaseData NextPhase => PhaseIndex + 1 < stats.phases.Length ? stats.phases[PhaseIndex + 1] : null;
        public bool HasStarted { get; private set; }
        /// <summary>預兆到破綻之間的招式；其他狀態為 null。</summary>
        public BossMove CurrentMove { get; private set; }
        /// <summary>上一次選到的招（不連出同一招用）。</summary>
        public BossMove LastMove { get; private set; }
        /// <summary>下一次選招直接出的招（Debug F3）；null = 不指定。</summary>
        public BossMove ForcedNextMove { get; private set; }
        /// <summary>沒有能出的招時，正在走進哪一招的距離範圍；null = 原地等。</summary>
        public BossMove RepositionGoal { get; private set; }
        /// <summary>這次已經連續走位幾秒（逾時判定用）；沒在走位為 0。</summary>
        public float RepositionTime { get; private set; }
        /// <summary>目前階段的開場序列已經出了幾招。</summary>
        public int OpeningIndex { get; private set; }
        /// <summary>這次 Idle 要等的秒數。</summary>
        public float IdleDuration { get; private set; }
        /// <summary>轉階段中（或進場招式到它造成的頭暈結束前），結束後要套用的階段；其他時候為 null。</summary>
        public BossPhaseData PendingPhase => pendingPhase >= 0 ? stats.phases[pendingPhase] : null;
        /// <summary>
        /// 轉階段 transitionTime 秒後自動結束（預設）。有轉階段演出時由 BattleManager 關掉，改由演出結束時呼叫 CompleteTransition。
        /// </summary>
        public bool AutoCompleteTransition { get; set; } = true;
        /// <summary>難度修正（BattleManager 開場前用 SetDifficulty 設定）；招式與 BossStagger 讀數值時加上去，不改 SO。</summary>
        public DifficultyModifiers Difficulty { get; private set; } = DifficultyModifiers.None;
        /// <summary>這次頭暈或轉階段的總秒數。</summary>
        public float TimedStateDuration { get; private set; }
        /// <summary>頭暈時間到也先不結束（被處決中，等那一刀打到）；離開頭暈時自動清掉。由 BossStagger 設定。</summary>
        public bool HoldStun { get; set; }
        /// <summary>同一個物件上的失衡值（可為 null）；招式用 BossMove.AddImbalance 加失衡值。</summary>
        public BossStagger Stagger { get; private set; }
        public IReadOnlyList<BossMove> Moves => moves;
        public Transform Target => target;
        /// <summary>設定玩家（PlayerSpawner 生成或換角色時呼叫）。</summary>
        public void SetTarget(Transform value) => target = value;
        /// <summary>競技場數值（半徑、外牆 Layer）；招式的撞牆、落點也讀這一份。</summary>
        public ArenaData Arena => arena;
        /// <summary>場地中心的世界座標。</summary>
        public Vector3 ArenaCenter => arenaCenter != null ? arenaCenter.position : Vector3.zero;

        /// <summary>和玩家的水平距離 (m)。</summary>
        public float DistanceToTarget => target != null ? Flat(target.position - transform.position).magnitude : float.PositiveInfinity;

        BossHealth health;
        LockOnTarget lockOn;
        readonly Dictionary<BossMove, float> readyTime = new Dictionary<BossMove, float>();
        readonly List<BossMove> candidates = new List<BossMove>();
        int pendingPhase = -1;

        void Awake()
        {
            health = GetComponent<BossHealth>();
            Stagger = GetComponent<BossStagger>();
            lockOn = GetComponent<LockOnTarget>();
            if (stats == null || stats.phases == null || stats.phases.Length == 0 || stats.phases[0] == null)
            {
                Debug.LogError("[Boss] BossStats 沒有設定階段，Boss 不會行動。", this);
                enabled = false;
                return;
            }

            for (int i = moves.Count - 1; i >= 0; i--)
            {
                if (moves[i] != null && moves[i].Data != null) continue;
                Debug.LogError($"[Boss] 招式清單第 {i} 項是空的或沒有 BossMoveData，已略過。", this);
                moves.RemoveAt(i);
            }
            foreach (var move in moves) move.Bind(this);

            PhaseIndex = 0;
            Phase = stats.phases[0];
        }

        void OnEnable()
        {
            health.OnBossHealthChanged += HandleHealthChanged;
            health.OnDefeated += HandleDefeated;
        }

        void OnDisable()
        {
            health.OnBossHealthChanged -= HandleHealthChanged;
            health.OnDefeated -= HandleDefeated;
        }

        void Start()
        {
            OnPhaseChanged?.Invoke(Phase);
            if (startOnPlay) StartBattle();
        }

        /// <summary>開打：直接進入選招（開場演出結束時接第一招，不另等出招間隔）。</summary>
        /// <summary>套用難度修正（預兆、失衡值、龍捲風速度、衝擊波間隔）；null = 不修正。</summary>
        public void SetDifficulty(DifficultyModifiers modifiers) => Difficulty = modifiers ?? DifficultyModifiers.None;

        public void StartBattle()
        {
            if (HasStarted || State == BossState.Defeated) return;
            HasStarted = true;
            SetState(BossState.ChooseMove);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (!HasStarted || CombatSuspended || dt <= 0f) return;
            StateTime += dt;

            switch (State)
            {
                case BossState.Idle:
                    FaceTarget(dt);
                    if (StateTime >= IdleDuration) SetState(BossState.ChooseMove);
                    break;
                case BossState.ChooseMove:
                    TickChooseMove(dt);
                    break;
                case BossState.Telegraph:
                    CurrentMove.TickTelegraph(dt);
                    if (StateTime >= CurrentMove.TelegraphTime)
                    {
                        SetState(BossState.Active);
                        CurrentMove.BeginActive();
                    }
                    break;
                case BossState.Active:
                    if (CurrentMove.TickActive(dt))
                    {
                        SetState(BossState.Recovery);
                        CurrentMove.BeginRecovery();
                    }
                    break;
                case BossState.Recovery:
                    CurrentMove.TickRecovery(dt);
                    if (StateTime >= CurrentMove.RecoveryTime) FinishMove();
                    break;
                case BossState.Stunned:
                    if (StateTime >= TimedStateDuration && !HoldStun) EnterIdle();
                    break;
                case BossState.Transition:
                    if (AutoCompleteTransition && StateTime >= TimedStateDuration) CompleteTransition();
                    break;
            }
        }

        // ---------- 選招 ----------

        void TickChooseMove(float dt)
        {
            float distance = DistanceToTarget;
            if (TryChoose(distance, out var choice))
            {
                StartMove(choice);
                return;
            }
            FaceTarget(dt);
            if (RepositionGoal == null)
            {
                RepositionTime = 0f; // 全部在冷卻：原地等，不算走位
                return;
            }
            if (Phase.stationary) return;
            WalkTowardRange(RepositionGoal.Data, distance, dt);
            RepositionTime += dt;
        }

        bool RepositionTimedOut => Phase.repositionTimeout > 0f && RepositionTime >= Phase.repositionTimeout;

        bool TryChoose(float distance, out BossMoveChoice choice)
        {
            choice = default;
            RepositionGoal = null;
            bool timedOut = RepositionTimedOut;

            if (ForcedNextMove != null)
            {
                choice = new BossMoveChoice(ForcedNextMove, MoveChoiceSource.Forced, 0, distance);
                ForcedNextMove = null;
                return true;
            }

            var opening = Phase.openingSequence;
            while (opening != null && OpeningIndex < opening.Length)
            {
                var move = FindMove(opening[OpeningIndex]);
                if (move == null)
                {
                    // 序列裡的招式沒掛在 Boss 身上：跳過，不讓整個循環卡住
                    Debug.LogError($"[Boss] {Phase.displayName} 開場序列第 {OpeningIndex + 1} 招不在 Boss 的招式清單裡，已略過。", this);
                    OpeningIndex++;
                    continue;
                }
                if (!move.Data.InRange(distance))
                {
                    if (timedOut)
                    {
                        // 走不到這一招的距離：跳過它，下一招重新計時
                        OpeningIndex++;
                        RepositionTime = 0f;
                        timedOut = false;
                        continue;
                    }
                    RepositionGoal = move;
                    return false;
                }
                OpeningIndex++;
                choice = new BossMoveChoice(move, MoveChoiceSource.Opening, 0, distance);
                return true;
            }

            // 招式池 → 距離 → 冷卻 → 不連出；距離不合的留下最近的一招當走位目標
            candidates.Clear();
            float totalWeight = 0f;
            float nearestGap = float.PositiveInfinity;
            foreach (var move in moves)
            {
                var data = move.Data;
                if (data.phase != Phase.movePool || data.WeightAt(distance) <= 0f || move == LastMove || !IsReady(move)) continue;
                float gap = data.DistanceToRange(distance);
                if (gap <= 0f)
                {
                    candidates.Add(move);
                    totalWeight += data.WeightAt(distance);
                }
                else if (gap < nearestGap)
                {
                    nearestGap = gap;
                    RepositionGoal = move;
                }
            }
            if (candidates.Count == 0)
            {
                if (!timedOut || RepositionGoal == null) return false;
                choice = new BossMoveChoice(RepositionGoal, MoveChoiceSource.Timeout, 0, distance);
                RepositionGoal = null;
                return true;
            }

            RepositionGoal = null;
            choice = new BossMoveChoice(PickWeighted(totalWeight, distance), MoveChoiceSource.Random, candidates.Count, distance);
            return true;
        }

        BossMove PickWeighted(float totalWeight, float distance)
        {
            float r = UnityEngine.Random.value * totalWeight;
            foreach (var move in candidates)
            {
                r -= move.Data.WeightAt(distance);
                if (r < 0f) return move;
            }
            return candidates[candidates.Count - 1]; // Random.value 可能剛好是 1
        }

        BossMove FindMove(BossMoveData data)
        {
            if (data == null) return null;
            foreach (var move in moves)
            {
                if (move.Data == data) return move;
            }
            return null;
        }

        // ---------- 出招 ----------

        void StartMove(in BossMoveChoice choice)
        {
            CurrentMove = choice.move;
            LastMove = choice.move;
            OnMoveChosen?.Invoke(choice);
            SetState(BossState.Telegraph);
            OnMoveTelegraph?.Invoke(new MoveTelegraph(CurrentMove, CurrentMove.Data.canBlock, CurrentMove.TelegraphTime));
            CurrentMove.BeginTelegraph();
        }

        void FinishMove()
        {
            var move = CurrentMove;
            CurrentMove = null;
            move.End();
            StartCooldown(move);
            EnterIdle();
            if (Phase.chainChance > 0f && UnityEngine.Random.value < Phase.chainChance) IdleDuration = 0f;
            OnMoveFinished?.Invoke(move);
        }

        // 頭暈、轉階段、倒下時收掉目前的招式；被打斷的招也進冷卻
        void InterruptMove()
        {
            RepositionGoal = null;
            if (CurrentMove == null) return;
            var move = CurrentMove;
            CurrentMove = null;
            move.Interrupt();
            StartCooldown(move);
        }

        void StartCooldown(BossMove move)
        {
            readyTime[move] = Time.time + move.Data.cooldown * Phase.cooldownMultiplier;
        }

        public bool IsReady(BossMove move) => CooldownRemaining(move) <= 0f;

        /// <summary>這招還要冷卻幾秒；0 = 可以出。</summary>
        public float CooldownRemaining(BossMove move) =>
            readyTime.TryGetValue(move, out float t) ? Mathf.Max(0f, t - Time.time) : 0f;

        /// <summary>指定下一次選招直接出這招（Debug F3）；null 取消。不看距離、冷卻、不連出與開場序列。</summary>
        public void ForceNextMove(BossMove move)
        {
            ForcedNextMove = move != null && moves.Contains(move) ? move : null;
        }

        internal void NotifyMoveHit(BossMove move, in HitReport report) => OnMoveHit?.Invoke(move, report);

        // 不限目前這一招：已經結束的招放出的落雷照樣會劈下來
        internal void NotifyImpact(BossImpactKind kind, Vector3 position) => OnMoveImpact?.Invoke(new BossImpact(kind, position));

        // 只接受目前這一招的預兆（被打斷後還在跑的招不算）
        internal void NotifyTelegraph(BossMove move, bool canBlock, float duration)
        {
            if (move == CurrentMove) OnMoveTelegraph?.Invoke(new MoveTelegraph(move, canBlock, duration));
        }

        /// <summary>收掉場上所有招式放出、還沒跑完的 Hazard（轉階段清場；已經結束的招放的衝擊波、落雷也一起）。</summary>
        public void ClearAllHazards()
        {
            foreach (var move in moves) move.ClearHazards();
        }

        // ---------- 頭暈、轉階段、倒下 ----------

        /// <summary>進入頭暈（失衡值滿）：打斷目前的招式，階段的頭暈時間後回到 Idle。轉階段、倒下時無效。</summary>
        public void Stun() => Stun(Phase.stunDuration);

        /// <summary>進入頭暈 duration 秒（大迴旋落地等固定時間的頭暈）。</summary>
        public void Stun(float duration)
        {
            if (!HasStarted || State == BossState.Transition || State == BossState.Defeated) return;
            InterruptMove();
            TimedStateDuration = duration;
            SetState(BossState.Stunned);
        }

        /// <summary>提早結束頭暈（被處決）：回到 Idle，照階段的出招間隔再選招。不在頭暈中時無效。</summary>
        public void EndStun()
        {
            if (State == BossState.Stunned) EnterIdle();
        }

        void HandleHealthChanged(float current, float max)
        {
            if (!AutomaticPhaseTransitions || pendingPhase >= 0 || State == BossState.Transition || State == BossState.Defeated || current <= 0f) return;
            var next = NextPhase;
            if (next != null && current <= next.healthThreshold) BeginTransition(PhaseIndex + 1);
        }

        void BeginTransition(int phaseIndex)
        {
            var next = stats.phases[phaseIndex];
            if (next.entryMove != null)
            {
                var entry = FindMove(next.entryMove);
                if (entry != null)
                {
                    BeginPhaseEntry(phaseIndex, entry);
                    return;
                }
                Debug.LogError($"[Boss] {next.displayName}的進場招式 {next.entryMove.displayName} 不在 Boss 的招式清單裡，直接換階段。", this);
            }
            if (next.transitionTime <= 0f)
            {
                // 沒有轉階段時間（狂暴）：直接換數值，目前的招照常打完；一次跨過好幾個門檻就繼續往下換
                ApplyPhase(phaseIndex);
                HandleHealthChanged(health.Current, health.Max);
                return;
            }

            InterruptMove();
            ClearAllHazards(); // 企劃書：轉階段時場上所有攻擊判定清除
            pendingPhase = phaseIndex;
            health.Invulnerable = true;
            TimedStateDuration = next.transitionTime;
            SetState(BossState.Transition);
        }

        // 進場招式：打斷目前的動作（頭暈中也一樣，BossStagger 會當成被打斷）、清場，立刻出；新階段等這招結束回到 Idle 時才套用（EnterIdle）
        void BeginPhaseEntry(int phaseIndex, BossMove entry)
        {
            InterruptMove();
            ClearAllHazards();
            pendingPhase = phaseIndex;
            StartMove(new BossMoveChoice(entry, MoveChoiceSource.PhaseEntry, 0, DistanceToTarget));
        }

        /// <summary>結束轉階段：套用新階段、解除無敵、回到 Idle。由轉階段演出結束時呼叫（沒有演出時由 transitionTime 計時呼叫）。不在轉階段中時無效。</summary>
        public void CompleteTransition()
        {
            if (State != BossState.Transition) return;
            ApplyPhase(pendingPhase);
            pendingPhase = -1;
            health.Invulnerable = false;
            EnterIdle();
            HandleHealthChanged(health.Current, health.Max);
        }

        void ApplyPhase(int index)
        {
            PhaseIndex = index;
            Phase = stats.phases[index];
            OpeningIndex = 0;
            OnPhaseChanged?.Invoke(Phase);
        }

        // 倒下：打斷招式、清掉場上所有 Hazard、不能再被鎖定（玩家的鎖定會自動解除）
        void HandleDefeated()
        {
            InterruptMove();
            ClearAllHazards();
            ForcedNextMove = null;
            health.Invulnerable = false;
            if (lockOn != null) lockOn.enabled = false;
            SetState(BossState.Defeated);
        }


        // 招式要求的無敵（BossMove.SetInvulnerable，大迴旋浮空期間）
        internal void SetMoveInvulnerable(bool on)
        {
            if (State == BossState.Transition || State == BossState.Defeated) return;
            health.Invulnerable = on;
        }

        // ---------- 狀態、移動 ----------

        void EnterIdle()
        {
            // 進場招式（與它造成的頭暈）結束：現在才套用新階段（轉階段的套用在 CompleteTransition）
            bool entryDone = pendingPhase >= 0 && State != BossState.Transition;
            if (entryDone)
            {
                int index = pendingPhase;
                pendingPhase = -1;
                ApplyPhase(index);
            }
            IdleDuration = Phase.RandomInterval();
            SetState(BossState.Idle);
            if (entryDone) HandleHealthChanged(health.Current, health.Max); // 一次跨過好幾個門檻就繼續往下換
        }

        void SetState(BossState next)
        {
            if (next != BossState.Stunned) HoldStun = false;
            State = next;
            StateTime = 0f;
            RepositionTime = 0f;
            OnStateChanged?.Invoke(next);
        }

        /// <summary>以 BossStats.turnSpeed 轉向玩家（只轉水平）。</summary>
        public void FaceTarget(float dt)
        {
            if (target == null) return;
            Vector3 to = Flat(target.position - transform.position);
            if (to.sqrMagnitude < 1e-4f) return;
            float speed = Phase != null && Phase.turnSpeedOverride > 0f ? Phase.turnSpeedOverride : stats.turnSpeed;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(to), speed * dt);
        }

        // 靠近或後退，剛好走到範圍邊緣為止；不會走出場地。
        // 後退時背後是牆（例如迴旋衝撞撞牆後，玩家站在 Boss 和場地中心之間），改沿著牆往離玩家較遠的一側繞，不然會卡在牆邊永遠拉不開距離
        void WalkTowardRange(BossMoveData data, float distance, float dt)
        {
            float gap = data.DistanceToRange(distance);
            if (gap <= 0f || target == null) return;

            Vector3 to = Flat(target.position - transform.position);
            Vector3 dir = to.sqrMagnitude > 1e-4f ? to.normalized : Flat(transform.forward).normalized;
            if (distance < data.minDistance) dir = -dir;
            float step = Mathf.Min(Phase.moveSpeed * dt, gap);
            Vector3 next = transform.position + dir * step;

            if (distance < data.minDistance && arena != null)
            {
                Vector3 radial = Flat(transform.position - ArenaCenter);
                float limit = Mathf.Max(0f, arena.radius - BodyRadius);
                if (Flat(next - ArenaCenter).magnitude > limit && radial.sqrMagnitude > 1e-4f)
                {
                    Vector3 tangent = Vector3.Cross(Vector3.up, radial.normalized);
                    if (Vector3.Dot(tangent, dir) < 0f) tangent = -tangent;
                    next = transform.position + tangent * (Phase.moveSpeed * dt);
                }
            }
            transform.position = ClampToArena(next);
        }

        /// <summary>把位置拉回場地內（扣掉身體半徑），高度不變。</summary>
        public Vector3 ClampToArena(Vector3 pos) => ClampToArena(pos, BodyRadius);

        /// <summary>把位置拉回離場地邊緣至少 margin 的範圍內，高度不變。</summary>
        public Vector3 ClampToArena(Vector3 pos, float margin)
        {
            if (arena == null) return pos;
            Vector3 center = ArenaCenter;
            float limit = Mathf.Max(0f, arena.radius - margin);
            Vector3 offset = Flat(pos - center);
            if (offset.sqrMagnitude <= limit * limit) return pos;
            offset = offset.normalized * limit;
            return new Vector3(center.x + offset.x, pos.y, center.z + offset.z);
        }

        /// <summary>
        /// 開關本體的實心碰撞。衝撞時關掉，才會穿過玩家，不會把玩家（CharacterController 被擠出）一路推到牆邊；
        /// 招式結束時要開回來。Hurtbox 是另一個 trigger，不受影響。
        /// </summary>
        public void SetBodyCollision(bool solid)
        {
            if (body != null) body.enabled = solid;
        }

        /// <summary>本體碰撞膠囊的水平半徑 (m)。</summary>
        public float BodyRadius
        {
            get
            {
                if (body == null) return 0f;
                Vector3 s = body.transform.lossyScale;
                return body.radius * Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.z));
            }
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
