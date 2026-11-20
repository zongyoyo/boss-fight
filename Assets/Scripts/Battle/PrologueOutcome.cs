using System;
using System.Collections.Generic;
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
    /// T34 重複挑戰（從休息區傳送門來：這次的出生點是 BossDefinition.portalArrival，Awake 時判斷，IsChallenge）：
    /// 不寫序章結果與序章獎勵；勝利演出播完記擊敗（ProgressRecorder.RecordBossDefeat）再開勝利畫面（再挑戰、返回休息區）；
    /// 玩家倒下不播擊倒演出，直接送戰敗的 OnBattleResult（OIIA 的戰敗畫面）。Retry 經 SceneFlow 重新進序章（同一個出生點），ReturnToHub 回傳送門前。
    /// 第幾次挑戰記在 BattleSession（依 Boss id）。
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
        /// <summary>從休息區傳送門來的重複挑戰（T34）。第一次讀到時判斷（最晚在自己的 Awake，SceneFlow 取走出生點之前），別的元件在 Awake 讀也可以。</summary>
        public bool IsChallenge
        {
            get
            {
                ResolveMode();
                return challenge;
            }
        }
        /// <summary>重複挑戰：這次執行的第幾次（1 起算）；不是挑戰時 0。</summary>
        public int Attempt
        {
            get
            {
                ResolveMode();
                return attempt;
            }
        }
        /// <summary>重複挑戰裡玩家倒下、戰敗畫面開著。</summary>
        public bool IsChallengeDefeat { get; private set; }
        public BossDefinition Definition => definition;

        bool committed, cancelled, victory;
        PlayerHealth health;
        bool subscribed;

        public void BindPlayer(PlayerRig rig)
        {
            if (subscribed && health != null) health.OnDied -= Died;
            health = rig != null ? rig.Health : null;
            if (subscribed && health != null) health.OnDied += Died;
        }

        bool modeResolved, challenge;
        int attempt;

        void Awake() => ResolveMode();

        // 換場景進來的出生點是這隻 Boss 的傳送門出生點 = 重複挑戰（SceneFlow 在 Start 才取走，所以 Awake 階段看得到）
        void ResolveMode()
        {
            if (modeResolved) return;
            modeResolved = true;
            challenge = definition != null && !string.IsNullOrEmpty(definition.portalArrival) &&
                        ProfileStore.PeekArrival(out string arrival) && arrival == definition.portalArrival;
            if (challenge) attempt = BattleSession.BeginChallengeAttempt(definition.id);
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
            IReadOnlyList<RewardData> rewards;
            // 重複挑戰只記擊敗（首通獎勵照 BossDefinition；序章結果與序章獎勵不動）
            if (IsChallenge) rewards = ProgressRecorder.RecordBossDefeat(definition);
            else if (!ProgressRecorder.TryCompletePrologue(definition, PrologueResult.Victory, out rewards)) return false;
            committed = true;
            IsVictoryPending = false;
            session.SetCursorReleased(this, true);
            if (audioManager != null) audioManager.FadeOutMusic(0f);
            OnBattleResult?.Invoke(new BattleResult(true, guardian.Elapsed, IsChallenge ? Attempt : 1, 0f, CurrentDifficulty, rewards));
            return true;
        }

        public void CancelVictoryPresentation(int token)
        {
            if (token != PresentationToken || committed) return;
            cancelled = true;
            IsVictoryPending = false;
        }

        Difficulty CurrentDifficulty => options != null ? options.Difficulty : Difficulty.Hard;

        /// <summary>勝利畫面的「返回休息區」；重複挑戰（T34）時勝敗畫面與 ESC 選單隨時都能回休息區的傳送門前。</summary>
        public void ReturnToHub()
        {
            if (sceneFlow == null) return;
            if (IsChallenge) sceneFlow.ReturnFromBattle();
            else if (committed) sceneFlow.ReturnFromPrologue(false);
        }

        /// <summary>重複挑戰（T34）的「再挑戰」「重試」與 ESC 選單的重新挑戰：經 SceneFlow 重新進序章（同一個出生點，還是重複挑戰）。</summary>
        public void Retry()
        {
            if (IsChallenge && sceneFlow != null) sceneFlow.GoToBattle(definition.sceneName, definition.portalArrival);
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
            if (IsChallenge)
            {
                // 重複挑戰不播擊倒演出、不寫序章：直接開戰敗畫面（重試、返回休息區）
                HasOutcome = IsChallengeDefeat = true;
                session.SetInputLocked(this, true);
                session.SetCursorReleased(this, true);
                guardian.StopBattle();
                guardian.ReleaseTransitionLock();
                float ratio = bossHealth.Max > 0f ? bossHealth.Current / bossHealth.Max : 0f;
                OnBattleResult?.Invoke(new BattleResult(false, guardian.Elapsed, Attempt, ratio, CurrentDifficulty, null));
                return;
            }
            HasOutcome = IsKnockdownPending = true;
            cancelled = false;
            session.SetInputLocked(this, true);
            guardian.StopBattle();
            guardian.ReleaseTransitionLock();
            OnKnockdownRequested?.Invoke();
        }
    }
}
