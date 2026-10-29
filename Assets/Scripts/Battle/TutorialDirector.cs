using System;
using BossFight.Boss;
using BossFight.Cameras;
using BossFight.Core;
using BossFight.Cutscene;
using BossFight.Player;
using BossFight.Progression;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BossFight.Battle
{
    public readonly struct TutorialView
    {
        public readonly TutorialStepData step;
        public readonly float progress;
        public readonly bool visible, completed;
        public readonly string interactionHint;
        /// <summary>第幾步（0 起算）與總步數，提示上方的進度用。</summary>
        public readonly int stepIndex, stepCount;
        public TutorialView(TutorialStepData step, float progress, bool visible, bool completed, string interactionHint, int stepIndex, int stepCount)
        {
            this.step = step; this.progress = progress; this.visible = visible; this.completed = completed; this.interactionHint = interactionHint;
            this.stepIndex = stepIndex; this.stepCount = stepCount;
        }
    }

    /// <summary>
    /// 六步教學的事件式流程；只指揮下層，不引用 UI 或 Hub，不寫序章完成存檔。
    /// 每句台詞照資料做菊花與小樹精的動作、切對白鏡頭（TutorialStage），開放操作時切回玩家鏡頭並面向小樹精。
    /// 開場演出（PrologueCutsceneFlow）播完才呼叫 StartApproach；沒接演出時（autoStart）自己開始。
    /// </summary>
    public class TutorialDirector : MonoBehaviour, IPlayerBinding
    {
        [SerializeField] TutorialData data;
        [SerializeField] InputLockArbiter session;
        [SerializeField] DialogueSession dialogue;
        [SerializeField] DaisyMentor mentor;
        [SerializeField] TrainingSapling sapling;
        [Tooltip("劇情的距離觸發中心，由 Builder 接中央花壇錨點")]
        [SerializeField] Transform startCenter;
        [Tooltip("對白鏡頭")]
        [SerializeField] TutorialStage shots;
        [Tooltip("開放操作時把自由視角轉到玩家背後、面向小樹精")]
        [SerializeField] CameraController cameraController;
        [Tooltip("地鳴、低吼、丟骨頭的聲音與震動")]
        [SerializeField] FeedbackEmitter feedback;
        [Tooltip("地鳴與北方低吼的發聲位置（守衛破土的地方）；PrologueCutsceneBuilder 接守衛本體")]
        [SerializeField] Transform roarSource;
        [Tooltip("Start 時自己開始；有開場演出時由 PrologueCutsceneBuilder 關掉，演出播完才 StartApproach")]
        [SerializeField] bool autoStart = true;
        public event Action<TutorialView> OnViewChanged;
        public event Action<string> OnStepCompleted;
        public event Action OnTutorialCompleted;
        /// <summary>結尾台詞與停頓已完成，開始鑽回；正式登場首鏡在這裡接手。</summary>
        public event Action OnEndingBurrowStarted;
        /// <summary>菊花丟的骨頭飛到玩家身上、骨頭補上（HUD 閃骨頭用）。</summary>
        public event Action OnBonesGranted;

        enum Stage { Waiting, Approach, Emerge, Inviting, Opening, Explanation, Operating, Completing, Praise, Ending, EndingPause, Burrow, Complete }
        Stage stage;
        PlayerRig player;
        bool subscribed, started, waitingLine, waitingCue, parryPending, parrySaid, prankPending, sideDialogue;
        bool actionBusy, delayFinished;
        bool inInteractionRange;
        InputAction interactAction;
        int stepIndex = -1, lineIndex, lastAttackId = -1;
        float progress, delayLeft, greetingLeft, cueLeft;
        TutorialLine[] lines;
        Action sequenceFinished;
        Action debugResume; // 開發者跳步：菊花冒出來之後接著做的事
        TutorialStepData Step => stepIndex >= 0 && stepIndex < data.steps.Length ? data.steps[stepIndex] : null;

        /// <summary>教學的資料（說話者、對白）；演出流程借用菊花的名字。</summary>
        public TutorialData Data => data;
        /// <summary>目前第幾步（0 起算）；還沒開始時 -1，結尾之後等於步數。</summary>
        public int StepIndex => stepIndex;
        public int StepCount => data.steps.Length;
        /// <summary>教學還在進行（可以跳步）。</summary>
        public bool IsRunning => (started || stage == Stage.Waiting) && stage != Stage.Burrow && stage != Stage.Complete;

        void Awake() => interactAction = InputSystem.actions?.FindAction("Player/Interact", true);

        public void BindPlayer(PlayerRig rig)
        {
            if (subscribed) UnsubscribePlayer();
            player = rig;
            if (subscribed) SubscribePlayer();
        }
        void OnEnable()
        {
            subscribed = true;
            interactAction?.Enable();
            SubscribePlayer();
            dialogue.OnFinished += HandleDialogueFinished;
            mentor.OnEmerged += HandleEmerged;
            mentor.OnWhipFinished += FinishCue;
            mentor.OnSlapFinished += FinishCue;
            mentor.OnTossFinished += HandleTossed;
            sapling.OnDemoFinished += FinishCue;
            mentor.OnBurrowed += HandleBurrowed;
            mentor.OnPrankRequested += HandlePrank;
            mentor.OnPrankFinished += HandlePrankFinished;
            sapling.OnAppeared += HandleAppeared;
            sapling.OnAttackResolved += HandleAttackResolved;
            sapling.OnDamaged += HandleDamaged;
            sapling.OnDefeated += HandleDefeated;
            PublishState();
        }
        void OnDisable()
        {
            UnsubscribePlayer();
            subscribed = false;
            dialogue.OnFinished -= HandleDialogueFinished;
            mentor.OnEmerged -= HandleEmerged;
            mentor.OnWhipFinished -= FinishCue;
            mentor.OnSlapFinished -= FinishCue;
            mentor.OnTossFinished -= HandleTossed;
            sapling.OnDemoFinished -= FinishCue;
            mentor.OnBurrowed -= HandleBurrowed;
            mentor.OnPrankRequested -= HandlePrank;
            mentor.OnPrankFinished -= HandlePrankFinished;
            sapling.OnAppeared -= HandleAppeared;
            sapling.OnAttackResolved -= HandleAttackResolved;
            sapling.OnDamaged -= HandleDamaged;
            sapling.OnDefeated -= HandleDefeated;
            session.SetInputLocked(this, false);
            if (shots != null) shots.Release();
        }
        void SubscribePlayer()
        {
            if (player == null) return;
            player.Controller.OnMoved += HandleMoved;
            player.Controller.OnPositionChanged += HandlePositionChanged;
            player.Controller.OnHealCompleted += HandleHealed;
            player.Controller.OnActionChanged += HandlePlayerAction;
            player.Health.OnDodgeSucceeded += HandleDodged;
            actionBusy = player.Controller.Action != PlayerAction.None;
        }
        void UnsubscribePlayer()
        {
            if (player == null) return;
            player.Controller.OnMoved -= HandleMoved;
            player.Controller.OnPositionChanged -= HandlePositionChanged;
            player.Controller.OnHealCompleted -= HandleHealed;
            player.Controller.OnActionChanged -= HandlePlayerAction;
            player.Health.OnDodgeSucceeded -= HandleDodged;
        }
        void Start()
        {
            player.Health.SetBoneGainEnabled(false);
            player.Health.SetBones(data.initialBones);
            stage = Stage.Waiting;
            if (autoStart) StartApproach();
        }
        /// <summary>開場演出播完後呼叫：開放自由走動，靠近中央時小花冒出。已經開始（例如開發者跳步）就不動。</summary>
        public void StartApproach()
        {
            if (stage != Stage.Waiting) return;
            started = true;
            stage = Stage.Approach;
            session.SetInputLocked(this, false);
            HandlePositionChanged(player.transform.position);
        }
        void HandlePositionChanged(Vector3 position)
        {
            if (!started || startCenter == null) return;
            Vector3 offset = position - startCenter.position;
            offset.y = 0f;
            bool near = offset.sqrMagnitude <= data.startRadius * data.startRadius;
            if (near != inInteractionRange)
            {
                inInteractionRange = near;
                if (stage == Stage.Inviting) PublishState();
            }
            if (stage == Stage.Approach && offset.sqrMagnitude <= data.greetingRadius * data.greetingRadius)
            {
                stage = Stage.Emerge;
                mentor.Emerge();
            }
        }
        void HandleEmerged()
        {
            if (debugResume != null)
            {
                var resume = debugResume;
                debugResume = null;
                resume();
                return;
            }
            if (!started || stage != Stage.Emerge) return;
            stage = Stage.Inviting;
            // 招呼只逐字顯示，不攔操作、不要求按鍵讀完；靠近後的互動才接正式劇情。
            dialogue.Begin(data.speaker, new[] { data.greeting }, false, false);
            mentor.Talk();
            mentor.Gesture(data.greetingGesture);
            greetingLeft = data.greetingDuration;
            PublishState();
        }
        void BeginStory()
        {
            greetingLeft = 0f;
            stage = Stage.Opening;
            PublishState();
            Say(data.opening, NextStep);
        }
        void Say(TutorialLine[] text, Action finished)
        {
            session.SetInputLocked(this, true);
            lines = text;
            lineIndex = 0;
            sequenceFinished = finished;
            PlayLine();
        }
        void PlayLine()
        {
            if (lines == null || lineIndex >= lines.Length)
            {
                var done = sequenceFinished;
                sequenceFinished = null;
                done?.Invoke();
                return;
            }
            var line = lines[lineIndex];
            Show(line.shot);
            if (line.gesture != DaisyGesture.None) mentor.Gesture(line.gesture, GesturePoint(line.gesture));
            if (line.saplingGesture != SaplingGesture.None) sapling.Gesture(line.saplingGesture);
            waitingLine = true;
            // 空白台詞不開對話框（DialogueSession 立刻送完成），直接執行這句的動作
            if (!string.IsNullOrEmpty(line.text)) mentor.Talk();
            dialogue.Begin(data.speaker, new[] { line.text }, false);
        }
        // 對白鏡頭：None 用雙人鏡頭
        void Show(TutorialShot shot)
        {
            if (shots == null || player == null) return;
            shots.Show(shot == TutorialShot.None ? TutorialShot.TwoShot : shot, player.transform, mentor.transform, sapling.IsAlive ? sapling.transform : null);
        }
        // 指向小樹精（還在時），不在就指向玩家
        Vector3 GesturePoint(DaisyGesture gesture)
        {
            if (gesture != DaisyGesture.Point) return default;
            Transform t = sapling.IsAlive ? sapling.transform : player.transform;
            return t.position + Vector3.up * 0.5f;
        }
        void HandleDialogueFinished(int id, bool completed)
        {
            if (!waitingLine || !completed) return;
            waitingLine = false;
            if (sideDialogue)
            {
                sideDialogue = false;
                if (stage == Stage.Completing) TryPraise();
                else if (stage == Stage.Operating) ResumeOperation();
                return;
            }
            var line = lines[lineIndex];
            TutorialCue cue = line.after;
            lineIndex++;
            if (cue == TutorialCue.None) { PlayLine(); return; }
            waitingCue = true;
            if (line.cueShot != TutorialShot.None) Show(line.cueShot);
            switch (cue)
            {
                case TutorialCue.Summon:
                    mentor.Gesture(DaisyGesture.Spin);
                    sapling.Appear();
                    break;
                case TutorialCue.Whip:
                    if (!mentor.Whip()) RetryCue();
                    break;
                case TutorialCue.Slap:
                    if (!mentor.Slap(sapling)) RetryCue();
                    break;
                case TutorialCue.GiveBone:
                    if (mentor.TossBone()) { if (feedback != null) feedback.Play(data.boneToss, mentor.transform.position); }
                    else RetryCue();
                    break;
                case TutorialCue.Demo:
                    // 往遠離玩家的方向甩一次（不開判定）；做不到（例如小樹精不在）就直接下一句
                    Vector3 away = sapling.transform.position - player.transform.position;
                    if (!sapling.Demonstrate(away, Step != null ? Step.mode : SaplingMode.DodgeAttack)) FinishCue();
                    break;
                case TutorialCue.ShowBars:
                    // 攻擊這步先把小樹精切成標靶，頭上的血量與軀幹條才會出現
                    sapling.SetMode(SaplingMode.Target);
                    FinishCue();
                    break;
                case TutorialCue.Rumble:
                    if (feedback != null) feedback.Play(data.rumble, roarSource != null ? roarSource.position : mentor.transform.position);
                    cueLeft = data.rumbleTime;
                    if (cueLeft <= 0f) FinishCue();
                    break;
            }
        }
        void HandleTossed()
        {
            if (!waitingCue) return;
            player.Health.SetBones(data.grantedBones);
            OnBonesGranted?.Invoke();
            FinishCue();
        }
        // 菊花彩蛋若剛好仍在收尾，保留命令，等 OnPrankFinished 再執行。
        bool retryCue;
        void RetryCue() => retryCue = true;
        void HandlePrankFinished()
        {
            if (!retryCue || !waitingCue) return;
            retryCue = false;
            var cue = lines[lineIndex - 1].after;
            if (cue == TutorialCue.Whip) mentor.Whip();
            else if (cue == TutorialCue.Slap) mentor.Slap(sapling);
            else if (cue == TutorialCue.GiveBone) mentor.TossBone();
        }
        void HandleAppeared()
        {
            sapling.SetInvulnerable(true);
            sapling.SetStaggerAccumulation(false);
            if (!waitingCue) return;
            sapling.Gesture(SaplingGesture.Wave);
            FinishCue();
        }
        void FinishCue()
        {
            if (!waitingCue) return;
            waitingCue = false;
            cueLeft = 0f;
            PlayLine();
        }
        void NextStep()
        {
            stepIndex++;
            if (stepIndex >= data.steps.Length)
            {
                stage = Stage.Ending;
                PublishState();
                Say(data.ending, BeginEndingPause);
                return;
            }
            progress = 0f;
            parryPending = parrySaid = false;
            sapling.SetMode(SaplingMode.Idle);
            sapling.SetInvulnerable(Step.invulnerable);
            sapling.SetStaggerAccumulation(false);
            stage = Stage.Explanation;
            PublishState();
            Say(Step.opening, BeginOperation);
        }
        void BeginOperation()
        {
            stage = Stage.Operating;
            // 回到玩家鏡頭，自由視角轉到玩家背後、面向小樹精
            if (shots != null) shots.Release();
            if (cameraController != null && player != null)
                cameraController.ResetBehind(sapling.IsAlive ? sapling.transform.position : mentor.transform.position);
            ResumeOperation();
        }
        void ResumeOperation()
        {
            // 先切狀態再開攻擊，避免同步事件在 UI 或操作尚未就緒時被漏掉。
            if (stage != Stage.Operating) return;
            sapling.SetMode(Step.mode);
            PublishState();
            session.SetInputLocked(this, false);
        }
        void AddProgress(float value)
        {
            if (stage != Stage.Operating || sideDialogue) return;
            progress = Mathf.Min(Step.target, progress + value);
            PublishState();
            if (progress >= Step.target) CompleteStep();
        }
        void HandleMoved(float distance)
        { if (Step != null && Step.condition == TutorialCondition.Move) AddProgress(distance); }
        void HandleHealed(float current, float max)
        { if (Step != null && Step.condition == TutorialCondition.Heal && current >= max) AddProgress(1f); }
        // 成功閃避只由 PlayerHealth 結算（同一次翻滾只送一次）；這裡只算小樹精的攻擊
        void HandleDodged(DamageInfo info)
        {
            if (stage != Stage.Operating || sideDialogue || Step == null || Step.condition != TutorialCondition.Dodge) return;
            if (info.source == null || !info.source.transform.IsChildOf(sapling.transform)) return;
            AddProgress(1f);
        }
        void HandleAttackResolved(SaplingAttackReport report)
        {
            if (stage != Stage.Operating || sideDialogue || report.cancelled || !report.hadWindow || report.id == lastAttackId) return;
            lastAttackId = report.id;
            if (Step.condition == TutorialCondition.Block && report.hasHit)
            {
                if (report.hit.result != DamageResult.Blocked && report.hit.result != DamageResult.Parried) return;
                if (report.hit.result == DamageResult.Parried && !parrySaid) parryPending = true;
                AddProgress(1f);
                if (stage == Stage.Operating && parryPending) ShowSideDialogue(data.parryPraise, true);
            }
        }
        void HandleDamaged(SaplingDamageReport report)
        {
            if (Step != null && Step.condition == TutorialCondition.Hit && report.result == DamageResult.Hit &&
                !report.info.isExecution && player != null && report.info.source == player.gameObject)
                AddProgress(1f);
        }
        void HandleDefeated(SaplingDefeatReason reason)
        {
            if (Step == null || Step.condition != TutorialCondition.Execute || reason != SaplingDefeatReason.Execution) return;
            // 伏筆（H7）：孩子倒下的那一刻，北方傳來一聲低吼
            if (feedback != null && stage == Stage.Operating) feedback.Play(data.distantRoar, roarSource != null ? roarSource.position : mentor.transform.position);
            AddProgress(1f);
        }
        void CompleteStep()
        {
            stage = Stage.Completing;
            delayFinished = false;
            delayLeft = data.completionDelay;
            session.SetInputLocked(this, true);
            sapling.SetMode(SaplingMode.Idle);
            OnStepCompleted?.Invoke(Step.id);
            PublishState();
        }
        void HandlePlayerAction(PlayerAction action)
        {
            actionBusy = action != PlayerAction.None;
            if (stage == Stage.Completing) TryPraise();
        }
        void TryPraise()
        {
            if (!delayFinished || actionBusy || sideDialogue || stage != Stage.Completing) return;
            if (parryPending) { ShowSideDialogue(data.parryPraise, true); return; }
            stage = Stage.Praise;
            if (string.IsNullOrEmpty(Step.praise)) NextStep();
            else Say(new[] { new TutorialLine { text = Step.praise, gesture = Step.praiseGesture } }, NextStep);
        }
        void HandlePrank()
        {
            if (stage != Stage.Operating || sideDialogue || string.IsNullOrEmpty(data.prankLine)) return;
            prankPending = true;
        }
        void ShowSideDialogue(string text, bool parry)
        {
            if (parry) { parryPending = false; parrySaid = true; }
            sideDialogue = waitingLine = true;
            session.SetInputLocked(this, true);
            sapling.SetMode(SaplingMode.Idle);
            dialogue.Begin(data.speaker, new[] { text }, false);
            mentor.Talk();
            if (parry) mentor.Gesture(data.parryGesture);
        }
        void BeginEndingPause()
        {
            stage = Stage.EndingPause;
            delayLeft = data.endingPause;
            mentor.Gesture(data.endingGesture);
        }
        // 鑽回時交給登場演出（Timeline 的鏡頭接手），先收掉對白鏡頭
        void BeginBurrow()
        {
            stage = Stage.Burrow;
            if (shots != null) shots.Release();
            OnEndingBurrowStarted?.Invoke();
            mentor.Burrow();
        }
        void HandleBurrowed()
        {
            if (stage != Stage.Burrow) return;
            stage = Stage.Complete;
            PublishState();
            // 保留 owner 鎖，接收流程先取得自己的鎖，再接守衛或正式登場演出。
            OnTutorialCompleted?.Invoke();
            Debug.Log("[Tutorial] 六步教學完成，已發送守衛交接事件。", this);
        }
        void Update()
        {
            if (Time.deltaTime <= 0f) return;
            if (stage == Stage.Inviting)
            {
                if (inInteractionRange && !actionBusy && player.Controller.IsGrounded &&
                    interactAction != null && interactAction.WasPressedThisFrame()) BeginStory();
                else if (greetingLeft > 0f)
                {
                    greetingLeft = Mathf.Max(0f, greetingLeft - Time.deltaTime);
                    if (greetingLeft <= 0f) dialogue.Hide();
                }
            }
            else if (stage == Stage.Completing)
            {
                delayLeft = Mathf.Max(0f, delayLeft - Time.deltaTime);
                delayFinished = delayLeft <= 0f;
                TryPraise();
            }
            else if (stage == Stage.EndingPause)
            {
                delayLeft -= Time.deltaTime;
                if (delayLeft <= 0f) BeginBurrow();
            }
            if (waitingCue && cueLeft > 0f)
            {
                cueLeft -= Time.deltaTime;
                if (cueLeft <= 0f) FinishCue();
            }
            else if (stage == Stage.Operating && prankPending && !sideDialogue && !actionBusy)
            { prankPending = false; ShowSideDialogue(data.prankLine, false); }
        }
        /// <summary>T30 取得自己的登場鎖後呼叫；不會解除其他流程的 owner 鎖。</summary>
        public void ReleaseCompletionLock() => session.SetInputLocked(this, false);
        /// <summary>交棒或開發捷徑中止教學；不偽造完成、不改存檔。</summary>
        public void Abort()
        {
            stage = Stage.Complete; started = false; debugResume = null;
            waitingLine = waitingCue = retryCue = sideDialogue = prankPending = parryPending = false;
            sequenceFinished = null; lines = null; delayLeft = greetingLeft = cueLeft = 0f;
            if (shots != null) shots.Release();
            dialogue.Hide(); sapling.SetMode(SaplingMode.Idle); sapling.Despawn(SaplingDespawnReason.Cleared);
            mentor.CancelActions(); PublishState(); session.SetInputLocked(this, false); enabled = false;
        }

        // ---------- 開發者工具（序章面板的跳步，只給 Debug 呼叫） ----------

        /// <summary>開發者跳到第 index 步（0 起算）：中止目前的對白與動作，菊花重新冒出、小樹精重新出現後播那一步的開場對白。</summary>
        public bool DebugJumpToStep(int index)
        {
            if (!IsRunning || index < 0 || index >= data.steps.Length) return false;
            DebugRestart(() =>
            {
                stepIndex = index - 1;
                NextStep();
            });
            return true;
        }

        /// <summary>開發者直接完成教學：中止目前的對白與動作，接結尾的鑽回（之後照常交給登場演出）。</summary>
        public bool DebugComplete()
        {
            if (!IsRunning) return false;
            DebugRestart(() =>
            {
                stepIndex = data.steps.Length;
                if (sapling.IsAlive) sapling.Despawn(SaplingDespawnReason.Cleared);
                PublishState();
                session.SetInputLocked(this, true);
                BeginBurrow();
            });
            return true;
        }

        void DebugRestart(Action resume)
        {
            waitingLine = waitingCue = retryCue = sideDialogue = prankPending = parryPending = parrySaid = false;
            sequenceFinished = null;
            lines = null;
            delayLeft = greetingLeft = cueLeft = 0f;
            progress = 0f;
            started = true;
            dialogue.Hide();
            if (sapling.IsAlive) sapling.Despawn(SaplingDespawnReason.Cleared);
            sapling.Appear();
            stage = Stage.Opening;
            session.SetInputLocked(this, true);
            debugResume = resume;
            mentor.CancelActions();
            mentor.Emerge(); // 冒出來之後 HandleEmerged 接著做 resume
        }

        public void PublishState() => OnViewChanged?.Invoke(new TutorialView(Step, progress,
            stage == Stage.Operating || stage == Stage.Completing || (stage == Stage.Inviting && inInteractionRange),
            stage == Stage.Completing, stage == Stage.Inviting && inInteractionRange ? data.interactionHint : null,
            stepIndex, data.steps.Length));
    }
}
