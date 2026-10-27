using System;
using BossFight.Boss;
using BossFight.Core;
using BossFight.Player;
using BossFight.Progression;
using UnityEngine;
namespace BossFight.Battle
{
    /// <summary>序章結局的唯一入口；倒下交接 T30，勝利先呈現、正常完成後才整批提交，不建立一般敗北。</summary>
    public class PrologueOutcome : MonoBehaviour, IPlayerBinding
    {
        [SerializeField] GuardianPhaseController guardian;
        [SerializeField] PrologueSession session;
        [SerializeField] BossHealth bossHealth;
        [SerializeField] BossDefinition definition;
        [SerializeField] SceneFlow sceneFlow;
        [SerializeField] AudioManager audioManager;
        [SerializeField] GameOptions options;
        public event Action OnKnockdownRequested;
        public event Action<int> OnVictoryPresentationRequested;
        public event Action<BattleResult> OnBattleResult;
        public event Action<string> OnPresentationHint;
        /// <summary>T30 在演出接管時設 true，並以收到的 token 回報正常完成。</summary>
        public bool ExternalVictoryPresentation { get; set; }
        public int PresentationToken { get; private set; }
        public bool IsVictoryPending { get; private set; }
        public bool HasOutcome { get; private set; }
        bool committed, cancelled, fallbackRunning;
        float presentationAge;
        PlayerHealth health;
        bool subscribed;
        public void BindPlayer(PlayerRig rig)
        {
            if (subscribed && health != null) health.OnDied -= Died;
            health = rig != null ? rig.Health : null;
            if (subscribed && health != null) health.OnDied += Died;
        }
        void OnEnable() { subscribed = true; if (health != null) health.OnDied += Died; bossHealth.OnDefeated += Victory; }
        void OnDisable()
        {
            if (health != null) health.OnDied -= Died;
            bossHealth.OnDefeated -= Victory; subscribed = false;
            CancelVictoryPresentation(PresentationToken);
            session.SetInputLocked(this, false); session.SetCursorReleased(this, false);
        }
        void Victory()
        {
            if (HasOutcome || guardian.Stage == GuardianStage.Dormant || (health != null && health.IsDead)) return;
            HasOutcome = IsVictoryPending = true; PresentationToken++; cancelled = false;
            session.SetInputLocked(this, true); guardian.StopBattle(); guardian.ReleaseTransitionLock();
            presentationAge = 0f; fallbackRunning = !ExternalVictoryPresentation;
            if (fallbackRunning) session.SetCursorReleased(this, true);
            OnPresentationHint?.Invoke(guardian.Data.placeholderVictoryHint);
            OnVictoryPresentationRequested?.Invoke(PresentationToken);
        }
        void Update()
        {
            if (!fallbackRunning || !IsVictoryPending || Time.deltaTime <= 0f) return;
            presentationAge += Time.deltaTime;
            if (presentationAge < guardian.Data.placeholderVictoryTime) return;
            fallbackRunning = false; CompleteVictoryPresentation(PresentationToken);
        }
        /// <summary>T29 暫代呈現與 T30 Timeline 共用的唯一提交入口；取消、卸載及重複回呼皆無效。</summary>
        public bool CompleteVictoryPresentation(int token)
        {
            if (!isActiveAndEnabled || !gameObject.scene.isLoaded || token != PresentationToken || !IsVictoryPending || committed || cancelled) return false;
            fallbackRunning = false;
            if (!ProgressRecorder.TryCompletePrologue(definition, PrologueResult.Victory, out var rewards)) return false;
            committed = true; IsVictoryPending = false;
            session.SetCursorReleased(this, true); OnPresentationHint?.Invoke(string.Empty);
            if (audioManager != null) audioManager.FadeOutMusic(0f);
            OnBattleResult?.Invoke(new BattleResult(true, guardian.Elapsed, 1, 0f, options != null ? options.Difficulty : Difficulty.Hard, rewards));
            return true;
        }
        public void CancelVictoryPresentation(int token)
        {
            if (token != PresentationToken || committed) return;
            cancelled = true; fallbackRunning = false; IsVictoryPending = false;
        }
        public void ReturnToHub() { if (committed && sceneFlow != null) sceneFlow.ReturnFromPrologue(false); }
        void Died()
        {
            if (HasOutcome || guardian.Stage == GuardianStage.Dormant) return;
            HasOutcome = true; session.SetInputLocked(this, true); guardian.StopBattle(); guardian.ReleaseTransitionLock();
            OnKnockdownRequested?.Invoke(); Debug.Log("[Prologue] 玩家倒下：交接擊倒演出（T30），不顯示一般敗北、不存檔。", this);
        }
    }
}
