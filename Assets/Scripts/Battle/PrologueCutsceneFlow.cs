using BossFight.Boss;
using BossFight.Cameras;
using BossFight.Core;
using BossFight.Cutscene;
using BossFight.Progression;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BossFight.Battle
{
    /// <summary>序章上層演出協調；Timeline 只送提示，這裡接戰鬥、對白與單次結局提交。</summary>
    public class PrologueCutsceneFlow : MonoBehaviour
    {
        [SerializeField] TutorialDirector tutorial;
        [SerializeField] GuardianPhaseController guardian;
        [SerializeField] GuardianPhaseTwo phaseTwo;
        [SerializeField] PrologueOutcome outcome;
        [SerializeField] PrologueSession session;
        [SerializeField] DialogueSession dialogue;
        [SerializeField] DaisyMentor mentor;
        [SerializeField] GuardianFullPowerVisual fullPower;
        [SerializeField] AudioManager audioManager;
        [SerializeField] CameraController cameraController;
        [SerializeField] SceneFlow sceneFlow;
        [SerializeField] GamePause pause;
        [SerializeField] PrologueCutsceneData data;
        [SerializeField] CinematicStage stage;
        [SerializeField] CutsceneDirector entrance, transition, knockdown, victory;
        enum Kind { None, Entrance, Transition, Knockdown, Victory }
        Kind kind;
        CutsceneDirector current;
        int victoryToken, dialogueId;
        bool entered, powered, retryCommit, committing;
        InputAction submit;
        void Awake()
        {
            phaseTwo.ExternalTransitionPresentation = true; outcome.ExternalVictoryPresentation = true;
            submit = InputSystem.actions?.FindAction("UI/Submit", false);
        }
        void OnEnable()
        {
            tutorial.OnEndingBurrowStarted += Enter;
            phaseTwo.OnTransitionPresentationRequested += Transition;
            outcome.OnVictoryPresentationRequested += Victory;
            outcome.OnKnockdownRequested += Knockdown;
            sceneFlow.OnLoadingChanged += Loading;
            dialogue.OnFinished += DialogueFinished;
            foreach (var seq in new[] { entrance, transition, knockdown, victory })
            { seq.OnCutsceneFinished += Finished; seq.OnCutsceneCancelled += Cancelled; seq.OnCue += Cue; }
        }
        void Start() { if (guardian.Stage == GuardianStage.Dormant && !sceneFlow.IsLoading) audioManager.PlayMusic(data.tutorialMusic); }
        void OnDisable()
        {
            tutorial.OnEndingBurrowStarted -= Enter;
            phaseTwo.OnTransitionPresentationRequested -= Transition;
            outcome.OnVictoryPresentationRequested -= Victory;
            outcome.OnKnockdownRequested -= Knockdown;
            sceneFlow.OnLoadingChanged -= Loading; dialogue.OnFinished -= DialogueFinished;
            Cancelled();
            foreach (var seq in new[] { entrance, transition, knockdown, victory })
            { seq.OnCutsceneFinished -= Finished; seq.OnCutsceneCancelled -= Cancelled; seq.OnCue -= Cue; }
            session.SetInputLocked(this, false); session.SetCursorReleased(this, false);
        }
        void Enter()
        {
            if (entered || guardian.Stage != GuardianStage.Dormant) return;
            entered = true; Begin(Kind.Entrance, entrance); tutorial.ReleaseCompletionLock();
        }
        void Transition() { powered = false; Begin(Kind.Transition, transition); }
        void Knockdown() => Begin(Kind.Knockdown, knockdown);
        void Victory(int token) { victoryToken = token; Begin(Kind.Victory, victory); }
        void Begin(Kind next, CutsceneDirector seq)
        {
            if (current != null && current.IsPlaying) current.Cancel();
            session.SetInputLocked(this, true);
            stage.ResetTransient(); kind = next; current = seq; retryCommit = false;
            stage.AlignToPlayer(); dialogue.Hide();
            if (next != Kind.Transition) audioManager.StopMusic();
            seq.Play(false);
        }
        void Cue(string cue)
        {
            if (kind == Kind.None || sceneFlow.IsLoading) return;
            switch (cue)
            {
                case "TransitionLine":
                    dialogueId = dialogue.Begin(data.guardianSpeaker, new[] { data.transitionLine }, false); break;
                case "Power":
                    if (powered) return; powered = true; dialogue.Hide();
                    fullPower.SetFullPower(); audioManager.PlayMusic(data.phase2Music); break;
                case "Farewell": dialogue.Begin(data.guardianSpeaker, new[] { data.farewellLine }, false, false); break;
                case "FlowerEmerge": mentor.Emerge(); break;
                case "Surprise": dialogue.Begin(data.mentorSpeaker, new[] { data.surpriseLine }, false, false); mentor.Talk(); break;
                case "FlowerBurrow": dialogue.Hide(); mentor.Burrow(); break;
            }
        }
        void DialogueFinished(int id, bool completed)
        { if (kind == Kind.Transition && !powered && completed && id == dialogueId) transition.JumpToCue("Power"); }
        void Finished(bool skipped)
        {
            if (kind == Kind.None || sceneFlow.IsLoading) return;
            if (skipped) { Cancelled(); return; }
            dialogue.Hide();
            if (kind == Kind.Entrance)
            {
                stage.ResetTransient(); guardian.StartBattle(); audioManager.PlayMusic(data.phase1Music);
                cameraController.ResetBehind(guardian.Boss.transform.position);
            }
            else if (kind == Kind.Transition) { stage.ResetTransient(); phaseTwo.CompleteTransition(); }
            else
            {
                // 演出已正常結束才嘗試提交；存檔失敗保留相同 token，確認鍵可重試。
                retryCommit = true; TryCommit(); return;
            }
            kind = Kind.None; current = null; session.SetInputLocked(this, false);
        }
        void TryCommit()
        {
            bool keepBlack = kind == Kind.Knockdown;
            committing = true;
            bool saved = kind == Kind.Victory ? outcome.CompleteVictoryPresentation(victoryToken) : outcome.CompleteKnockdownPresentation();
            committing = false;
            if (!saved) { stage.ShowSaveFailure(true); session.SetCursorReleased(this, true); return; }
            retryCommit = false; stage.ShowSaveFailure(false);
            stage.ResetTransient(); if (keepBlack) stage.Apply(CinematicValue.Black, 1f);
            kind = Kind.None; current = null; session.SetInputLocked(this, false); session.SetCursorReleased(this, false);
        }
        void Update()
        { if (retryCommit && !(pause != null && pause.IsPaused) && submit != null && submit.WasPressedThisFrame()) TryCommit(); }
        void Loading(bool loading) { if (loading && !committing && kind != Kind.None) Cancelled(); }
        void Cancelled()
        {
            var old = kind; kind = Kind.None; retryCommit = false;
            var seq = current; current = null; if (seq != null && seq.IsPlaying) seq.Cancel();
            if (old == Kind.Victory && outcome != null) outcome.CancelVictoryPresentation(victoryToken);
            if (old == Kind.Knockdown && outcome != null) outcome.CancelKnockdownPresentation();
            if (old == Kind.Transition && phaseTwo != null) phaseTwo.CancelTransition();
            if (stage != null) { stage.ResetTransient(); stage.ShowSaveFailure(false); }
            if (dialogue != null) dialogue.Hide();
            if (session != null) { session.SetInputLocked(this, false); session.SetCursorReleased(this, false); }
        }
    }
}
