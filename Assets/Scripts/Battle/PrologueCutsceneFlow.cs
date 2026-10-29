using System;
using BossFight.Boss;
using BossFight.Cameras;
using BossFight.Core;
using BossFight.Cutscene;
using BossFight.Progression;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace BossFight.Battle
{
    /// <summary>
    /// 序章的上層演出協調（T30）：開場（玩家起身、帶過空地）→ 教學；教學結尾鑽回 → 守衛登場 → 開戰；活過萬根齊發 → 轉階段演出 → 二階段；擊倒、勝利演出 → 單次提交結局。
    /// Timeline 只送 CutsceneCue，這裡接對白、角色與戰鬥；台詞與說話者讀 GuardianBattleData／TutorialData／PrologueCutsceneData。
    /// 也是序章這場戰鬥給開發者工具的入口（IBattleDebugTarget）。
    /// </summary>
    public class PrologueCutsceneFlow : MonoBehaviour, IBattleDebugTarget
    {
        [SerializeField] TutorialDirector tutorial;
        [SerializeField] GuardianPhaseController guardian;
        [SerializeField] GuardianPhaseTwo phaseTwo;
        [SerializeField] PrologueOutcome outcome;
        [SerializeField] InputLockArbiter session;
        [SerializeField] DialogueSession dialogue;
        [SerializeField] DaisyMentor mentor;
        [SerializeField] GuardianFullPowerVisual fullPower;
        [SerializeField] AudioManager audioManager;
        [SerializeField] CameraController cameraController;
        [SerializeField] SceneFlow sceneFlow;
        [SerializeField] GamePause pause;
        [SerializeField] PrologueCutsceneData data;
        [SerializeField] PrologueCinematicStage stage;
        [SerializeField] CutsceneDirector entrance, transition, knockdown, victory;
        [Tooltip("序章開場；留空時教學直接開始")]
        [SerializeField] CutsceneDirector opening;

        enum Kind { None, Entrance, Transition, Knockdown, Victory, Opening }

        Kind kind;
        CutsceneDirector current;
        int victoryToken, dialogueId;
        bool entered, powered, retryCommit, committing;
        InputAction submit;
        BattleState debugState;
        int selfCostCount;
        float selfCostTotal, selfCostLast;

        GuardianBattleData Battle => guardian.Data;

        void Awake()
        {
            submit = InputSystem.actions?.FindAction("UI/Submit", false);
        }

        void OnEnable()
        {
            tutorial.OnEndingBurrowStarted += Enter;
            phaseTwo.OnTransitionPresentationRequested += Transition;
            phaseTwo.OnSelfCost += HandleSelfCost;
            guardian.OnStageChanged += HandleStage;
            outcome.OnVictoryPresentationRequested += Victory;
            outcome.OnKnockdownRequested += Knockdown;
            sceneFlow.OnLoadingChanged += Loading;
            dialogue.OnFinished += DialogueFinished;
            foreach (var seq in Sequences())
            {
                seq.OnCutsceneFinished += Finished;
                seq.OnCutsceneCancelled += Cancelled;
                seq.OnCue += Cue;
            }
        }

        void Start()
        {
            if (guardian.Stage != GuardianStage.Dormant || sceneFlow.IsLoading) return;
            // 開場與教學只在序章一開始跑一次；沒接開場演出就直接開始教學
            if (opening != null) Begin(Kind.Opening, opening);
            else tutorial.StartApproach();
            audioManager.PlayMusic(data.tutorialMusic);
        }

        void OnDisable()
        {
            tutorial.OnEndingBurrowStarted -= Enter;
            phaseTwo.OnTransitionPresentationRequested -= Transition;
            phaseTwo.OnSelfCost -= HandleSelfCost;
            guardian.OnStageChanged -= HandleStage;
            outcome.OnVictoryPresentationRequested -= Victory;
            outcome.OnKnockdownRequested -= Knockdown;
            sceneFlow.OnLoadingChanged -= Loading;
            dialogue.OnFinished -= DialogueFinished;
            Cancelled();
            foreach (var seq in Sequences())
            {
                seq.OnCutsceneFinished -= Finished;
                seq.OnCutsceneCancelled -= Cancelled;
                seq.OnCue -= Cue;
            }
            session.SetInputLocked(this, false);
            session.SetCursorReleased(this, false);
        }

        CutsceneDirector[] Sequences() => opening != null ? new[] { entrance, transition, knockdown, victory, opening } : new[] { entrance, transition, knockdown, victory };

        void Enter()
        {
            if (entered || guardian.Stage != GuardianStage.Dormant) return;
            entered = true;
            Begin(Kind.Entrance, entrance);
            tutorial.ReleaseCompletionLock();
            SetDebugState(BattleState.Intro);
        }

        void Transition()
        {
            powered = false;
            Begin(Kind.Transition, transition);
        }

        void Knockdown() => Begin(Kind.Knockdown, knockdown);

        void Victory(int token)
        {
            victoryToken = token;
            Begin(Kind.Victory, victory);
        }

        void Begin(Kind next, CutsceneDirector seq)
        {
            if (current != null && current.IsPlaying) current.Cancel();
            session.SetInputLocked(this, true);
            stage.ResetTransient();
            kind = next;
            current = seq;
            retryCommit = false;
            stage.AlignToPlayer();
            dialogue.Hide();
            if (next != Kind.Transition) audioManager.StopMusic();
            seq.Play(false);
        }

        void Cue(CutsceneCue cue)
        {
            if (kind == Kind.None || sceneFlow.IsLoading) return;
            switch (cue)
            {
                case CutsceneCue.TransitionLine:
                    dialogueId = dialogue.Begin(Battle.guardianName, new[] { Battle.phase2Line }, false);
                    break;
                case CutsceneCue.Power:
                    if (powered) return;
                    powered = true;
                    dialogue.Hide();
                    fullPower.SetFullPower();
                    audioManager.PlayMusic(data.phase2Music);
                    break;
                case CutsceneCue.Farewell:
                    dialogue.Begin(Battle.guardianName, new[] { data.farewellLine }, false, false);
                    break;
                case CutsceneCue.FlowerEmerge:
                    mentor.Emerge();
                    break;
                case CutsceneCue.Surprise:
                    dialogue.Begin(tutorial.Data.speaker, new[] { data.surpriseLine }, false, false);
                    mentor.Talk();
                    break;
                case CutsceneCue.FlowerBurrow:
                    dialogue.Hide();
                    mentor.Burrow();
                    break;
                case CutsceneCue.MoundPeek:
                    mentor.Peek();
                    break;
            }
        }

        void DialogueFinished(int id, bool completed)
        {
            if (kind == Kind.Transition && !powered && completed && id == dialogueId) transition.JumpToCue(CutsceneCue.Power);
        }

        void Finished(bool skipped)
        {
            if (kind == Kind.None || sceneFlow.IsLoading) return;
            if (skipped)
            {
                Cancelled();
                return;
            }
            dialogue.Hide();
            if (kind == Kind.Opening)
            {
                stage.ResetTransient();
                cameraController.ResetBehind(mentor.transform.position);
                tutorial.StartApproach();
            }
            else if (kind == Kind.Entrance)
            {
                stage.ResetTransient();
                StartGuardianBattle();
            }
            else if (kind == Kind.Transition)
            {
                stage.ResetTransient();
                phaseTwo.CompleteTransition();
            }
            else
            {
                // 演出已正常結束才嘗試提交；存檔失敗保留相同 token，確認鍵可重試
                retryCommit = true;
                TryCommit();
                return;
            }
            kind = Kind.None;
            current = null;
            session.SetInputLocked(this, false);
        }

        void StartGuardianBattle()
        {
            guardian.StartBattle();
            audioManager.PlayMusic(data.phase1Music);
            cameraController.ResetBehind(guardian.Boss.transform.position);
        }

        void TryCommit()
        {
            bool keepBlack = kind == Kind.Knockdown;
            committing = true;
            bool saved = kind == Kind.Victory ? outcome.CompleteVictoryPresentation(victoryToken) : outcome.CompleteKnockdownPresentation();
            committing = false;
            if (!saved)
            {
                stage.ShowSaveFailure(true);
                session.SetCursorReleased(this, true);
                return;
            }
            retryCommit = false;
            stage.ShowSaveFailure(false);
            stage.ResetTransient();
            if (keepBlack) stage.Apply(CinematicValue.Black, 1f);
            kind = Kind.None;
            current = null;
            session.SetInputLocked(this, false);
            session.SetCursorReleased(this, false);
        }

        void Update()
        {
            if (retryCommit && !(pause != null && pause.IsPaused) && submit != null && submit.WasPressedThisFrame()) TryCommit();
        }

        void Loading(bool loading)
        {
            if (loading && !committing && kind != Kind.None) Cancelled();
        }

        void Cancelled()
        {
            var old = kind;
            kind = Kind.None;
            retryCommit = false;
            var seq = current;
            current = null;
            if (seq != null && seq.IsPlaying) seq.Cancel();
            if (old == Kind.Victory && outcome != null) outcome.CancelVictoryPresentation(victoryToken);
            if (old == Kind.Knockdown && outcome != null) outcome.CancelKnockdownPresentation();
            if (old == Kind.Transition && phaseTwo != null) phaseTwo.CancelTransition();
            // 開場被中止（開發者跳步或略過登場）時教學照樣能開始；已經開始就不動
            if (old == Kind.Opening && tutorial != null && tutorial.isActiveAndEnabled && gameObject.scene.isLoaded) tutorial.StartApproach();
            if (stage != null)
            {
                stage.ResetTransient();
                stage.ShowSaveFailure(false);
            }
            if (dialogue != null) dialogue.Hide();
            if (session != null)
            {
                session.SetInputLocked(this, false);
                session.SetCursorReleased(this, false);
            }
        }

        // ---------- 開發者工具（IBattleDebugTarget 與序章面板，只給 Debug 呼叫） ----------

        /// <summary>開發者略過登場直接開戰：中止教學，套上登場演出結尾的天色與守衛外觀，照登場結束的流程開戰。</summary>
        public bool DebugSkipEntrance()
        {
            if (guardian.Stage != GuardianStage.Dormant) return false;
            if (kind == Kind.Entrance || kind == Kind.Opening) Cancelled();
            entered = true;
            tutorial.Abort();
            stage.Apply(CinematicValue.Rise, 1f);
            stage.Apply(CinematicValue.GroundCrack, 1f);
            stage.Apply(CinematicValue.Darkness, 1f);
            stage.ResetTransient();
            StartGuardianBattle();
            return guardian.Stage == GuardianStage.Phase1;
        }

        void HandleStage(GuardianStage value)
        {
            switch (value)
            {
                case GuardianStage.Phase1: SetDebugState(BattleState.Phase1); break;
                case GuardianStage.RootBarrage: SetDebugState(BattleState.Ultimate); break;
                case GuardianStage.AwaitingPhase2: SetDebugState(BattleState.Transition); break;
                case GuardianStage.Phase2: SetDebugState(BattleState.Phase2); break;
                case GuardianStage.Stopped: SetDebugState(outcome.IsKnockdownPending ? BattleState.Defeat : BattleState.Victory); break;
            }
        }

        void HandleSelfCost(float amount)
        {
            selfCostCount++;
            selfCostTotal += amount;
            selfCostLast = amount;
        }

        void SetDebugState(BattleState value)
        {
            if (debugState == value) return;
            debugState = value;
            OnDebugStateChanged?.Invoke(value);
        }

        public event Action<BattleState> OnDebugStateChanged;
        string IBattleDebugTarget.DebugBossName => Battle.guardianName;
        BattleState IBattleDebugTarget.DebugState => debugState;
        bool IBattleDebugTarget.DebugUltimateBeforeTransition => true;
        string IBattleDebugTarget.DebugUltimateName => guardian.Barrage != null ? guardian.Barrage.DisplayName : "萬根齊發";
        Difficulty IBattleDebugTarget.DebugDifficulty => Difficulty.Hard;
        int IBattleDebugTarget.DebugAttempt => 1;
        bool IBattleDebugTarget.DebugIsOver => outcome.HasOutcome;
        BossMove IBattleDebugTarget.DebugPendingMove => guardian.RootBarrageRequested ? guardian.Barrage : null;
        string IBattleDebugTarget.DebugNextPhaseHint => "下一階段（開戰→萬根→二階段）";

        // F1：還沒開戰 → 略過登場開戰；一階段 → 萬根齊發；萬根中 → 直接進轉階段演出
        string IBattleDebugTarget.DebugNextPhase()
        {
            switch (guardian.Stage)
            {
                case GuardianStage.Dormant:
                    return DebugSkipEntrance() ? "F1：略過登場，直接開戰" : "F1：現在不能開戰（玩家倒下或還沒生成）";
                case GuardianStage.Phase1:
                    guardian.RequestRootBarrage();
                    return $"F1：放{guardian.Barrage.DisplayName}（頭暈或處決中會等結束；先站 {Battle.guidanceRadius:0.#} m 內）";
                case GuardianStage.RootBarrage:
                    return phaseTwo.DebugEnterPhase2() ? "F1：中止萬根齊發，進二階段演出" : "F1：現在不能進二階段";
                default:
                    return "F1：已經是最後一個階段";
            }
        }

        // F6：越過一階段的血量鎖，直接觸發勝利演出
        string IBattleDebugTarget.DebugDefeatBoss()
        {
            var health = guardian.Health;
            if (guardian.Stage == GuardianStage.Dormant) return "F6：還沒開戰（先按 F1）";
            if (health.IsDefeated || outcome.HasOutcome) return null;
            health.HealthFloor = 0f;
            health.DebugSetHealth(0f);
            return "F6：守衛血量歸零（越過血量鎖），播勝利演出";
        }

        // F5：重新載入序章（從教學開頭）
        void IBattleDebugTarget.DebugRetry()
        {
            Time.timeScale = 1f; // 慢動作或畫面停頓中按下時不要把 timeScale 帶到新場景
            SceneManager.LoadScene(gameObject.scene.name);
        }

        // F3 選到萬根齊發：走一階段的萬根流程（無敵、計時、轉階段），不當成一般招式
        bool IBattleDebugTarget.DebugForceMove(BossMove move)
        {
            if (move == null || move != guardian.Barrage) return false;
            guardian.RequestRootBarrage();
            return true;
        }

        string IBattleDebugTarget.DebugPanelLines()
        {
            var health = guardian.Health;
            string stageName = StageName(guardian.Stage);
            string limit = guardian.Stage == GuardianStage.Phase1 ? $" / {Battle.phaseDuration:0} s（{Battle.guidanceAt:0} s 提示）" : " s";
            string floor = health.HealthFloor > 0f ? $"血量鎖 {health.HealthFloor:0}（{Battle.healthFloorRatio:P0}）" : "血量鎖 解除";
            string request = guardian.RootBarrageRequested ? $"　<color=#ffd84d>{guardian.Barrage.DisplayName}已要求，等頭暈／處決結束</color>" : "";
            string cost = $"自損（每招 {Battle.selfCostRatio:P0} 當前血量）{selfCostCount} 次　共 −{selfCostTotal:0}　上次 −{selfCostLast:0}";
            return $"流程  {stageName}　戰鬥時間 {guardian.Elapsed:0.0}{limit}\n{floor}{request}\n{cost}";
        }

        static string StageName(GuardianStage s)
        {
            switch (s)
            {
                case GuardianStage.Dormant: return "還沒開戰（教學／登場）";
                case GuardianStage.Phase1: return "一階段";
                case GuardianStage.RootBarrage: return "萬根齊發";
                case GuardianStage.AwaitingPhase2: return "轉階段";
                case GuardianStage.Phase2: return "二階段（全力）";
                case GuardianStage.Stopped: return "分出勝負";
                default: return s.ToString();
            }
        }
    }
}
