using System;
using BossFight.Boss;
using BossFight.Core;
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
        public TutorialView(TutorialStepData step, float progress, bool visible, bool completed, string interactionHint = null)
        { this.step = step; this.progress = progress; this.visible = visible; this.completed = completed; this.interactionHint = interactionHint; }
    }

    /// <summary>六步教學的事件式流程；只指揮下層，不引用 UI 或 Hub，不寫序章完成存檔。</summary>
    public class TutorialDirector : MonoBehaviour, IPlayerBinding
    {
        [SerializeField] TutorialData data;
        [SerializeField] PrologueSession session;
        [SerializeField] DialogueSession dialogue;
        [SerializeField] DaisyMentor mentor;
        [SerializeField] TrainingSapling sapling;
        [Tooltip("劇情的距離觸發中心，由 Builder 接中央花壇錨點")]
        [SerializeField] Transform startCenter;
        public event Action<TutorialView> OnViewChanged;
        public event Action<string> OnStepCompleted;
        public event Action OnTutorialCompleted;
        /// <summary>結尾台詞與停頓已完成，開始鑽回；正式登場首鏡在這裡接手。</summary>
        public event Action OnEndingBurrowStarted;

        enum Stage { Approach, Emerge, Inviting, Opening, Explanation, Operating, Completing, Praise, Ending, EndingPause, Burrow, Complete }
        Stage stage;
        PlayerRig player;
        bool subscribed, started, waitingLine, waitingCue, parryPending, parrySaid, prankPending, sideDialogue;
        bool actionBusy, delayFinished;
        bool inInteractionRange;
        InputAction interactAction;
        int stepIndex = -1, lineIndex, lastAttackId = -1;
        float progress, delayLeft, greetingLeft;
        TutorialLine[] lines;
        Action sequenceFinished;
        TutorialStepData Step => stepIndex >= 0 && stepIndex < data.steps.Length ? data.steps[stepIndex] : null;

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
            mentor.OnBurrowed -= HandleBurrowed;
            mentor.OnPrankRequested -= HandlePrank;
            mentor.OnPrankFinished -= HandlePrankFinished;
            sapling.OnAppeared -= HandleAppeared;
            sapling.OnAttackResolved -= HandleAttackResolved;
            sapling.OnDamaged -= HandleDamaged;
            sapling.OnDefeated -= HandleDefeated;
            session.SetInputLocked(this, false);
        }
        void SubscribePlayer()
        {
            if (player == null) return;
            player.Controller.OnMoved += HandleMoved;
            player.Controller.OnPositionChanged += HandlePositionChanged;
            player.Controller.OnHealCompleted += HandleHealed;
            player.Controller.OnActionChanged += HandlePlayerAction;
            actionBusy = player.Controller.Action != PlayerAction.None;
        }
        void UnsubscribePlayer()
        {
            if (player == null) return;
            player.Controller.OnMoved -= HandleMoved;
            player.Controller.OnPositionChanged -= HandlePositionChanged;
            player.Controller.OnHealCompleted -= HandleHealed;
            player.Controller.OnActionChanged -= HandlePlayerAction;
        }
        void Start()
        {
            started = true;
            player.Health.SetBoneGainEnabled(false);
            player.Health.SetBones(data.initialBones);
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
            if (!started || stage != Stage.Emerge) return;
            stage = Stage.Inviting;
            // 招呼只逐字顯示，不攔操作、不要求按鍵讀完；靠近後的互動才接正式劇情。
            dialogue.Begin(data.speaker, new[] { data.greeting }, false, false);
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
            waitingLine = true;
            dialogue.Begin(data.speaker, new[] { lines[lineIndex].text }, false);
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
            TutorialCue cue = lines[lineIndex].after;
            lineIndex++;
            if (cue == TutorialCue.None) { PlayLine(); return; }
            waitingCue = true;
            switch (cue)
            {
                case TutorialCue.Summon: sapling.Appear(); break;
                case TutorialCue.Whip:
                    if (!mentor.Whip()) RetryCue();
                    break;
                case TutorialCue.Slap:
                    if (!mentor.Slap(sapling)) RetryCue();
                    break;
                case TutorialCue.GiveBone:
                    player.Health.SetBones(data.grantedBones);
                    FinishCue();
                    break;
            }
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
        }
        void HandleAppeared()
        {
            sapling.SetInvulnerable(true);
            sapling.SetStaggerAccumulation(false);
            if (waitingCue) FinishCue();
        }
        void FinishCue()
        {
            if (!waitingCue) return;
            waitingCue = false;
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
        void HandleAttackResolved(SaplingAttackReport report)
        {
            if (stage != Stage.Operating || sideDialogue || report.cancelled || !report.hadWindow || report.id == lastAttackId) return;
            lastAttackId = report.id;
            if (Step.condition == TutorialCondition.Dodge)
            {
                if (IsSuccessfulDodge(report, player.Health.ForcedInvulnerable)) AddProgress(1f);
            }
            else if (Step.condition == TutorialCondition.Block && report.hasHit)
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
        { if (Step != null && Step.condition == TutorialCondition.Execute && reason == SaplingDefeatReason.Execution) AddProgress(1f); }
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
            else Say(new[] { new TutorialLine { text = Step.praise } }, NextStep);
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
        }
        void BeginEndingPause() { stage = Stage.EndingPause; delayLeft = data.endingPause; }
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
                if (delayLeft <= 0f) { stage = Stage.Burrow; OnEndingBurrowStarted?.Invoke(); mentor.Burrow(); }
            }
            else if (stage == Stage.Operating && prankPending && !sideDialogue && !actionBusy)
            { prankPending = false; ShowSideDialogue(data.prankLine, false); }
        }
        /// <summary>T30 取得自己的登場鎖後呼叫；不會解除其他流程的 owner 鎖。</summary>
        public void ReleaseCompletionLock() => session.SetInputLocked(this, false);
        /// <summary>交棒或開發捷徑中止教學；不偽造完成、不改存檔。</summary>
        public void Abort()
        {
            stage = Stage.Complete; started = false;
            waitingLine = waitingCue = retryCue = sideDialogue = prankPending = parryPending = false;
            sequenceFinished = null; lines = null; delayLeft = greetingLeft = 0f;
            dialogue.Hide(); sapling.SetMode(SaplingMode.Idle); sapling.Despawn(SaplingDespawnReason.Cleared);
            mentor.CancelActions(); PublishState(); session.SetInputLocked(this, false); enabled = false;
        }
        /// <summary>只接受有威脅、完整結束的翻滾閃避；零傷害命中與強制無敵都不計。</summary>
        public static bool IsSuccessfulDodge(in SaplingAttackReport report, bool forcedInvulnerable) =>
            !forcedInvulnerable && !report.cancelled && report.hadWindow && report.rollEvaded &&
            (!report.hasHit || report.hit.result == DamageResult.Evaded);
        public void PublishState() => OnViewChanged?.Invoke(new TutorialView(Step, progress,
            stage == Stage.Operating || stage == Stage.Completing || (stage == Stage.Inviting && inInteractionRange),
            stage == Stage.Completing, stage == Stage.Inviting && inInteractionRange ? data.interactionHint : null));
    }
}
