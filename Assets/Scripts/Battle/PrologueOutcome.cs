using System;
using BossFight.Boss;
using BossFight.Core;
using BossFight.Player;
using BossFight.Progression;
using UnityEngine;

namespace BossFight.Battle
{
    /// <summary>
    /// 序章結局的唯一入口（T29–T30）：玩家倒下 → 送 OnKnockdownRequested（不開一般敗北畫面）；守衛血量歸零 → 送 OnVictoryPresentationRequested(token)。
    /// 兩種結局都由 PrologueCutsceneFlow 播演出，演出正常結束才呼叫 Complete…Presentation 寫存檔（記擊敗、發首通獎勵、序章完成）；
    /// 存檔失敗可以重試，取消、卸載、重複回呼都不提交。勝利寫完存檔後送 OnBattleResult 開勝利畫面（只有「返回休息區」）。
    /// </summary>
    public class PrologueOutcome : MonoBehaviour, IPlayerBinding
    {
        [SerializeField] GuardianPhaseController guardian;
        [SerializeField] InputLockArbiter session;
        [SerializeField] BossHealth bossHealth;
        [SerializeField] BossDefinition definition;
        [SerializeField] SceneFlow sceneFlow;
        [SerializeField] AudioManager audioManager;
        [SerializeField] GameOptions options;

        public event Action OnKnockdownRequested;
        public event Action<int> OnVictoryPresentationRequested;
        public event Action<BattleResult> OnBattleResult;

        public int PresentationToken { get; private set; }
        public bool IsVictoryPending { get; private set; }
        public bool IsKnockdownPending { get; private set; }
        public bool HasOutcome { get; private set; }
        /// <summary>勝利已寫進存檔（勝利畫面開著）。</summary>
        public bool IsVictoryCommitted => committed && victory;

        bool committed, cancelled, victory;
        PlayerHealth health;
        bool subscribed;

        public void BindPlayer(PlayerRig rig)
        {
            if (subscribed && health != null) health.OnDied -= Died;
            health = rig != null ? rig.Health : null;
            if (subscribed && health != null) health.OnDied += Died;
        }

        void OnEnable()
        {
            subscribed = true;
            if (health != null) health.OnDied += Died;
            bossHealth.OnDefeated += Victory;
        }

        void OnDisable()
        {
            if (health != null) health.OnDied -= Died;
            bossHealth.OnDefeated -= Victory;
            subscribed = false;
            CancelVictoryPresentation(PresentationToken);
            CancelKnockdownPresentation();
            session.SetInputLocked(this, false);
            session.SetCursorReleased(this, false);
        }

        void Victory()
        {
            if (HasOutcome || guardian.Stage == GuardianStage.Dormant || (health != null && health.IsDead)) return;
            HasOutcome = IsVictoryPending = victory = true;
            PresentationToken++;
            cancelled = false;
            session.SetInputLocked(this, true);
            guardian.StopBattle();
            guardian.ReleaseTransitionLock();
            OnVictoryPresentationRequested?.Invoke(PresentationToken);
        }

        /// <summary>勝利演出正常結束時呼叫（帶收到的 token）；取消、卸載及重複回呼都無效。</summary>
        public bool CompleteVictoryPresentation(int token)
        {
            if (!isActiveAndEnabled || !gameObject.scene.isLoaded || token != PresentationToken || !IsVictoryPending || committed || cancelled) return false;
            if (!ProgressRecorder.TryCompletePrologue(definition, PrologueResult.Victory, out var rewards)) return false;
            committed = true;
            IsVictoryPending = false;
            session.SetCursorReleased(this, true);
            if (audioManager != null) audioManager.FadeOutMusic(0f);
            OnBattleResult?.Invoke(new BattleResult(true, guardian.Elapsed, 1, 0f, options != null ? options.Difficulty : Difficulty.Hard, rewards));
            return true;
        }

        public void CancelVictoryPresentation(int token)
        {
            if (token != PresentationToken || committed) return;
            cancelled = true;
            IsVictoryPending = false;
        }

        /// <summary>勝利畫面的「返回休息區」。</summary>
        public void ReturnToHub()
        {
            if (committed && sceneFlow != null) sceneFlow.ReturnFromPrologue(false);
        }

        /// <summary>擊倒演出正常結束的唯一提交入口；存檔失敗可重試，取消、卸載、重複回呼不提交。</summary>
        public bool CompleteKnockdownPresentation()
        {
            if (!isActiveAndEnabled || !gameObject.scene.isLoaded || !IsKnockdownPending || committed || cancelled) return false;
            if (!ProgressRecorder.TryCompletePrologue(null, PrologueResult.KnockedDown, out _)) return false;
            committed = true;
            IsKnockdownPending = false;
            if (sceneFlow != null) sceneFlow.ReturnFromPrologue(true);
            return true;
        }

        public void CancelKnockdownPresentation()
        {
            if (!IsKnockdownPending || committed) return;
            cancelled = true;
            IsKnockdownPending = false;
        }

        void Died()
        {
            if (HasOutcome || guardian.Stage == GuardianStage.Dormant) return;
            HasOutcome = IsKnockdownPending = true;
            cancelled = false;
            session.SetInputLocked(this, true);
            guardian.StopBattle();
            guardian.ReleaseTransitionLock();
            OnKnockdownRequested?.Invoke();
        }
    }
}
