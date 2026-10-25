using System;
using System.Collections.Generic;
using BossFight.Boss;
using BossFight.Cameras;
using BossFight.Core;
using BossFight.Cutscene;
using BossFight.Player;
using BossFight.Progression;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BossFight.Battle
{
    /// <summary>戰鬥流程的狀態：Intro → Phase1 → Transition → Phase2 → Ultimate → Berserk，最後是 Victory 或 Defeat。</summary>
    public enum BattleState
    {
        /// <summary>還沒開始（Start 之前）。</summary>
        None,
        /// <summary>開場動畫：玩家輸入鎖定，Boss 還沒行動。</summary>
        Intro,
        /// <summary>第一階段：BossController 從開場序列開始出招。</summary>
        Phase1,
        /// <summary>轉階段演出：Boss 無敵、場上 Hazard 已清掉、玩家輸入鎖定。</summary>
        Transition,
        /// <summary>第二階段。</summary>
        Phase2,
        /// <summary>終極技：Boss 血量首次降到狂暴門檻，放 OIIA 大迴旋（浮空倒數、安全區、落地頭暈）；頭暈結束後進入 Berserk。</summary>
        Ultimate,
        /// <summary>狂暴（HP ≤ 400）：出招間隔 0.3 秒、冷卻 ×0.75、音樂 1.3 倍速，招式池不變。</summary>
        Berserk,
        /// <summary>勝利：Boss 血量歸零（Boss 自己停止選招、清掉 Hazard、倒地）。</summary>
        Victory,
        /// <summary>失敗：玩家血量歸零。Boss 不停手、音樂照播，繼續在倒下的玩家旁邊出招（嘲諷）。</summary>
        Defeat,
    }

    /// <summary>一場戰鬥的結算，OnBattleResult 送出，勝敗畫面顯示。</summary>
    public readonly struct BattleResult
    {
        /// <summary>true = 勝利，false = 失敗。</summary>
        public readonly bool victory;
        /// <summary>從第一階段開打到分出勝負的秒數（遊戲時間，不含開場）。</summary>
        public readonly float battleTime;
        /// <summary>這次執行的第幾次挑戰（1 起算）。</summary>
        public readonly int attempt;
        /// <summary>分出勝負時 Boss 剩餘血量比例 0..1。</summary>
        public readonly float bossHealthRatio;
        /// <summary>這一場的難度。</summary>
        public readonly Difficulty difficulty;
        /// <summary>這次拿到的首次擊敗獎勵（T24）；不是首通或失敗時是空的。</summary>
        public readonly IReadOnlyList<RewardData> firstClearRewards;

        public BattleResult(bool victory, float battleTime, int attempt, float bossHealthRatio, Difficulty difficulty, IReadOnlyList<RewardData> firstClearRewards)
        {
            this.firstClearRewards = firstClearRewards ?? Array.Empty<RewardData>();
            this.victory = victory;
            this.battleTime = battleTime;
            this.attempt = attempt;
            this.bossHealthRatio = bossHealthRatio;
            this.difficulty = difficulty;
        }
    }

    /// <summary>
    /// 戰鬥流程狀態機：Intro → Phase1 → Transition → Phase2 → Ultimate → Berserk → Victory／Defeat。Start 時記一次挑戰，播開場動畫；第二次挑戰起開場可以長按跳過。
    /// 開場結束（播完或跳過）時記下「看過開場」，切到第一階段並呼叫 BossController.StartBattle，Boss 從固定開場序列開打。
    /// Boss 血量降到第二階段門檻時 BossController 自己進入 Transition（無敵、打斷招式、清 Hazard）；這裡收到後播轉階段演出，
    /// 演出結束時呼叫 BossController.CompleteTransition 套用第二階段，切到 Phase2。有接轉階段演出時會關掉 Boss 的 AutoCompleteTransition。
    /// 轉階段演出跟開場一樣，這次執行看過一次（BattleSession.TransitionSeen）之後可以長按跳過。
    /// 血量首次降到狂暴門檻時 BossController 自己出狂暴的進場招式 OIIA 大迴旋（OnMoveChosen 的 MoveChoiceSource.PhaseEntry），這裡切到 Ultimate；
    /// 大迴旋與落地頭暈結束、Boss 套用狂暴（OnPhaseChanged）時切到 Berserk，音樂改成狂暴的速度。沒有設定進場招式時從 Phase2 直接切到 Berserk。
    /// 勝敗：Boss 血量歸零 → Victory（Boss 自己停手、清場、倒地；最後一擊放慢動作）；玩家血量歸零 → Defeat（Boss 不停手、音樂照播，當作嘲諷）。
    /// 先發生的算數，之後另一邊倒下不理。勝利時音樂淡出；兩者都等 BattleSettings 的秒數後鎖住玩家與鏡頭的輸入、放開游標，送 OnBattleResult（勝敗畫面）。
    /// 重試（Retry，勝敗畫面的按鈕與 Debug F5）重新載入場景；「看過開場」「看過轉階段」存在 BattleSession，重試後可以長按跳過。
    /// 返回休息區（ReturnToHub，勝敗畫面與 ESC 選單）交給 SceneFlow，回到休息區的傳送門前；之後再從傳送門進來算新的一次挑戰，開場一樣可以跳過。
    /// 存檔：Boss 一倒下就交給 ProgressRecorder 記錄擊敗 BattleSettings.boss（不等勝利畫面；F6 也算），第一次擊敗就發首通獎勵並存檔（T24），
    /// 發了哪些放進 BattleResult.firstClearRewards（勝利畫面的「首次擊敗獎勵」）。失敗沒有獎勵。
    /// 音樂速度：換階段時照 BossPhaseData.musicSpeed 設定；轉階段演出開始時先排好下一階段的速度，演出中重播音樂時才套用。
    /// 沒有接 CutsceneDirector 時直接開打／直接換階段。
    /// 難度：Awake 時讀 GameOptions.Difficulty（存檔的值），把 DifficultyData 的修正交給玩家（PlayerController）與 Boss（BossController），整場不變；
    /// ESC 選單改難度後要重新挑戰才套用。
    /// ESC 選單的秘技（UnlockDeveloperOptions）記在 BattleSession，重試後 Start 時再送一次 OnDeveloperOptionsUnlocked，選單繼續顯示開發者模式開關。
    /// 暫停（ESC 選單，GamePause）：暫停時鎖住玩家與鏡頭的輸入、放開游標；解除時還原（演出中交給演出解鎖、勝敗畫面維持鎖定）。
    /// UI 與演出訂閱 OnBattleStateChanged、OnBattleResult。
    /// 玩家（PlayerController、PlayerHealth）由 PlayerSpawner 生成後交給 BindPlayer（T18）。
    /// 放在 Battle（最上層）：它要指揮 Boss、玩家與演出，Core 才能維持在最底層、不引用其他系統。
    /// </summary>
    [DisallowMultipleComponent]
    public class BattleManager : MonoBehaviour, IPlayerBinding
    {
        [Tooltip("勝敗結算的節奏")]
        [SerializeField] BattleSettings settings;
        [Tooltip("開場動畫；留空時直接開打")]
        [SerializeField] CutsceneDirector intro;
        [Tooltip("轉階段演出；留空時 Boss 照 BossPhaseData.transitionTime 計時自動轉階段")]
        [SerializeField] CutsceneDirector transition;
        [SerializeField] BossController boss;
        [Tooltip("Boss 血量歸零 → 勝利；也用來記結算時的剩餘血量")]
        [SerializeField] BossHealth bossHealth;
        [Tooltip("結算時鎖住視角、放開游標給勝敗畫面的按鈕")]
        [SerializeField] CameraController cameraController;
        [Tooltip("換階段時改音樂速度、勝敗時淡出；留空時不管音樂")]
        [SerializeField] AudioManager audioManager;
        [Tooltip("ESC 選單的暫停：暫停時鎖住玩家與鏡頭的輸入、放開游標；留空時不管")]
        [SerializeField] GamePause pause;
        [Tooltip("存檔的難度；留空時一律困難")]
        [SerializeField] GameOptions options;
        [Tooltip("各難度的修正值；留空時不修正")]
        [SerializeField] DifficultyData difficultyData;
        [Tooltip("返回休息區；留空時不能返回")]
        [SerializeField] SceneFlow sceneFlow;

        /// <summary>流程狀態改變；參數是新的狀態。</summary>
        public event Action<BattleState> OnBattleStateChanged;
        /// <summary>分出勝負、等完 BattleSettings 的秒數後送一次：顯示勝敗畫面。</summary>
        public event Action<BattleResult> OnBattleResult;
        /// <summary>這次執行在 ESC 選單輸入了秘技（重試後 Start 時也會送）：選單顯示開發者模式開關。</summary>
        public event Action OnDeveloperOptionsUnlocked;

        public BattleState State { get; private set; }
        public bool IsOver => State == BattleState.Victory || State == BattleState.Defeat;
        /// <summary>這一場的難度（Awake 時從 GameOptions 讀到的值）。</summary>
        public Difficulty Difficulty { get; private set; }

        float battleStartTime, bossHealthRatio = 1f;
        BattleResult pendingResult;
        IReadOnlyList<RewardData> firstClearRewards;
        float resultAt = -1f;      // 真實時間；< 0 = 沒有待送的結算
        float slowMotionUntil = -1f;
        bool resultShown, pauseLocked;
        // 玩家由 PlayerSpawner 交給 BindPlayer：血量歸零 → 失敗；結算、暫停時鎖住輸入、解除鎖定
        PlayerController player;
        PlayerHealth playerHealth;
        DifficultyModifiers modifiers;
        bool subscribed;

        /// <summary>PlayerSpawner 生成（或換）角色時呼叫；已經訂閱就先退訂舊角色。</summary>
        public void BindPlayer(PlayerRig rig)
        {
            if (subscribed && playerHealth != null) playerHealth.OnDied -= HandlePlayerDied;
            player = rig != null ? rig.Controller : null;
            playerHealth = rig != null ? rig.Health : null;
            if (subscribed && playerHealth != null) playerHealth.OnDied += HandlePlayerDied;
            if (modifiers != null && player != null) player.SetDifficulty(modifiers); // Awake 之後才換人時補套難度
        }

        void Awake()
        {
            Difficulty = options != null ? options.Difficulty : Difficulty.Hard;
            modifiers = difficultyData != null ? difficultyData.Get(Difficulty) : DifficultyModifiers.None;
            if (player != null) player.SetDifficulty(modifiers);
            if (boss != null) boss.SetDifficulty(modifiers);
        }

        void OnEnable()
        {
            if (intro != null) intro.OnCutsceneFinished += HandleIntroFinished;
            if (transition != null) transition.OnCutsceneFinished += HandleTransitionFinished;
            if (boss != null)
            {
                boss.OnStateChanged += HandleBossStateChanged;
                boss.OnPhaseChanged += HandlePhaseChanged;
                boss.OnMoveChosen += HandleMoveChosen;
            }
            if (bossHealth != null)
            {
                bossHealth.OnBossHealthChanged += HandleBossHealthChanged;
                bossHealth.OnDefeated += HandleBossDefeated;
            }
            if (playerHealth != null) playerHealth.OnDied += HandlePlayerDied;
            subscribed = true;
        }

        void OnDisable()
        {
            if (intro != null) intro.OnCutsceneFinished -= HandleIntroFinished;
            if (transition != null) transition.OnCutsceneFinished -= HandleTransitionFinished;
            if (boss != null)
            {
                boss.OnStateChanged -= HandleBossStateChanged;
                boss.OnPhaseChanged -= HandlePhaseChanged;
                boss.OnMoveChosen -= HandleMoveChosen;
            }
            if (bossHealth != null)
            {
                bossHealth.OnBossHealthChanged -= HandleBossHealthChanged;
                bossHealth.OnDefeated -= HandleBossDefeated;
            }
            if (playerHealth != null) playerHealth.OnDied -= HandlePlayerDied;
            subscribed = false;
        }

        void Start()
        {
            if (boss != null) boss.AutoCompleteTransition = transition == null;
            BattleSession.BeginAttempt();
            if (BattleSession.DeveloperOptionsUnlocked) OnDeveloperOptionsUnlocked?.Invoke();
            if (intro == null)
            {
                StartPhase1();
                return;
            }
            SetState(BattleState.Intro);
            intro.Play(BattleSession.IntroSeen);
        }

        void Update()
        {
            UpdateSlowMotion();
            if (resultAt >= 0f && Time.unscaledTime >= resultAt)
            {
                resultAt = -1f;
                ShowResult();
            }
        }

        // 暫停（ESC 選單）的輸入鎖定。放在 LateUpdate：選單在 Update 開關、F12 面板被關掉時還原的鎖定都處理完了才鎖或解鎖；
        // 解除那一幀玩家的 Update 已經跑完，關選單用的鍵（手把 B＝翻滾、A＝跳躍）不會被當成動作
        void LateUpdate()
        {
            bool paused = pause != null && pause.IsPaused;
            if (paused == pauseLocked) return;
            pauseLocked = paused;
            if (paused)
            {
                if (player != null) player.SetInputLocked(true);
                if (cameraController != null)
                {
                    cameraController.SetInputLocked(true);
                    cameraController.SetCursorReleased(true);
                }
                return;
            }
            if (resultShown) return; // 勝敗畫面：維持 ShowResult 的鎖定與游標
            bool cutscene = (intro != null && intro.IsPlaying) || (transition != null && transition.IsPlaying); // 演出結束時 CutsceneDirector 自己解鎖
            if (!cutscene && player != null) player.SetInputLocked(false);
            if (cameraController != null)
            {
                if (!cutscene) cameraController.SetInputLocked(false);
                cameraController.SetCursorReleased(false);
            }
        }

        /// <summary>重新挑戰：重新載入場景。「看過開場」留在 BattleSession，開場可以長按跳過。</summary>
        public void Retry()
        {
            Time.timeScale = 1f; // 慢動作或畫面停頓中按下時不要把 timeScale 帶到新場景
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        /// <summary>返回休息區（勝敗畫面、ESC 選單）：黑幕淡出後換場景，出生在傳送門前。</summary>
        public void ReturnToHub()
        {
            if (sceneFlow != null) sceneFlow.ReturnFromBattle();
        }

        /// <summary>ESC 選單輸入了秘技：這次執行（含重試）都顯示開發者模式開關。</summary>
        public void UnlockDeveloperOptions()
        {
            if (BattleSession.DeveloperOptionsUnlocked) return;
            BattleSession.MarkDeveloperOptionsUnlocked();
            OnDeveloperOptionsUnlocked?.Invoke();
        }

        void HandleIntroFinished(bool skipped)
        {
            if (State != BattleState.Intro) return;
            BattleSession.MarkIntroSeen();
            StartPhase1();
        }

        void StartPhase1()
        {
            battleStartTime = Time.time;
            SetState(BattleState.Phase1);
            if (boss != null) boss.StartBattle();
        }

        void HandleBossStateChanged(BossState next)
        {
            if (next != BossState.Transition || State == BattleState.Transition || IsOver) return;
            SetState(BattleState.Transition);
            if (transition == null) return; // Boss 自己計時結束，HandlePhaseChanged 再切到第二階段

            var phase = boss.PendingPhase;
            if (audioManager != null && phase != null) audioManager.SetMusicSpeed(phase.musicSpeed, false);
            transition.Play(BattleSession.TransitionSeen);
        }

        void HandleTransitionFinished(bool skipped)
        {
            BattleSession.MarkTransitionSeen();
            if (State == BattleState.Transition) boss.CompleteTransition(); // → OnPhaseChanged → Phase2
        }

        // 進場招式（OIIA 大迴旋）開始
        void HandleMoveChosen(BossMoveChoice choice)
        {
            if (choice.source == MoveChoiceSource.PhaseEntry && State == BattleState.Phase2) SetState(BattleState.Ultimate);
        }

        void HandlePhaseChanged(BossPhaseData phase)
        {
            if (IsOver) return;
            if (audioManager != null) audioManager.SetMusicSpeed(phase.musicSpeed);
            switch (State)
            {
                case BattleState.Transition:
                    SetState(BattleState.Phase2);
                    break;
                case BattleState.Phase2:
                case BattleState.Ultimate:
                    SetState(BattleState.Berserk);
                    break;
            }
        }

        // ---------- 勝敗 ----------

        void HandleBossHealthChanged(float current, float max) => bossHealthRatio = max > 0f ? current / max : 0f;

        void HandleBossDefeated()
        {
            if (IsOver) return;
            firstClearRewards = settings != null ? ProgressRecorder.RecordBossDefeat(settings.boss) : null;
            EndBattle(true);
            if (settings != null && settings.victorySlowMotionTime > 0f && settings.victorySlowMotionScale < 1f)
                slowMotionUntil = Time.unscaledTime + settings.victorySlowMotionTime;
        }

        void HandlePlayerDied()
        {
            if (IsOver) return;
            EndBattle(false); // Boss 不停手：繼續對倒下的玩家出招，當作嘲諷
        }

        void EndBattle(bool victory)
        {
            float time = State == BattleState.None || State == BattleState.Intro ? 0f : Time.time - battleStartTime;
            pendingResult = new BattleResult(victory, time, BattleSession.Attempt, bossHealthRatio, Difficulty, victory ? firstClearRewards : null);
            if (victory && audioManager != null) audioManager.FadeOutMusic(settings != null ? settings.resultMusicFadeTime : 0f); // 失敗時照播（嘲諷）
            float delay = settings == null ? 0f : victory ? settings.victoryResultDelay : settings.defeatResultDelay;
            resultAt = Time.unscaledTime + delay;
            SetState(victory ? BattleState.Victory : BattleState.Defeat);
        }

        void ShowResult()
        {
            resultShown = true;
            if (player != null)
            {
                player.ClearLockTarget();
                player.SetInputLocked(true);
            }
            if (cameraController != null)
            {
                cameraController.SetInputLocked(true);
                cameraController.SetCursorReleased(true);
            }
            OnBattleResult?.Invoke(pendingResult);
        }

        // 最後一擊的慢動作；畫面停頓（HitStop 把 timeScale 設 0）期間不搶，等它還原後再放慢
        void UpdateSlowMotion()
        {
            if (slowMotionUntil < 0f || Time.timeScale <= 0f) return;
            if (Time.unscaledTime < slowMotionUntil)
            {
                Time.timeScale = settings.victorySlowMotionScale;
                return;
            }
            Time.timeScale = 1f;
            slowMotionUntil = -1f;
        }

        void SetState(BattleState next)
        {
            State = next;
            OnBattleStateChanged?.Invoke(next);
        }
    }
}
