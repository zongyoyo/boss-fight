using System;
using BossFight.Cameras;
using BossFight.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace BossFight.Cutscene
{
    /// <summary>字卡的內容，OnTitleCard 送出。</summary>
    public readonly struct TitleCardInfo
    {
        public readonly string title;
        public readonly string subtitle;
        /// <summary>顯示秒數，之後淡出。</summary>
        public readonly float duration;

        public TitleCardInfo(string title, string subtitle, float duration)
        {
            this.title = title;
            this.subtitle = subtitle;
            this.duration = duration;
        }
    }

    /// <summary>
    /// 播放一段演出（同一個物件上 PlayableDirector 的 Timeline），期間鎖住玩家與鏡頭的輸入、由 Timeline 的 Cinemachine Track 接管鏡頭。
    /// 場景裡一段演出一個物件：開場（IntroCutscene，Assets/Timelines/Intro.playable）、轉階段（TransitionCutscene，Transition.playable）。
    /// BattleManager 呼叫 Play；skippable 時長按 Player/SkipCutscene（Space）settings.skipHoldTime 秒，跳到 Timeline 上的 SkipTarget 標記（開場第 7 鏡）。
    /// Timeline 用遊戲時間播，暫停（ESC 選單、F12 面板，timeScale 0）時演出停住，長按跳過也不累積。
    /// 有接 CutsceneStage 時，開始播之前先依玩家與場地中心的位置擺好演出鏡頭（轉階段時玩家在哪裡都有可能）。
    /// 播完時把自由視角擺到玩家背後、面向 Boss，解除輸入鎖定，送出 OnCutsceneFinished；BattleManager 收到後讓 Boss 開打或進入下一個階段。
    /// 字卡由 Timeline 的 CutsceneEventMarker 觸發，轉成 OnTitleCard；UI 只訂閱這裡的事件。
    /// Timeline 上的自訂 Track 都依「目前時間」算出畫面狀態，所以直接改 director.time 跳過也會得到正確的結果。
    /// </summary>
    [RequireComponent(typeof(PlayableDirector))]
    [DisallowMultipleComponent]
    public class CutsceneDirector : MonoBehaviour, INotificationReceiver
    {
        [Tooltip("跳過與字卡的設定；留空 = 不能跳過、沒有字卡（轉階段）")]
        [SerializeField] IntroCutsceneSettings settings;
        [Tooltip("演出期間鎖住輸入，由 Timeline 帶著走")]
        [SerializeField] PlayerController player;
        [Tooltip("演出期間鎖住視角；結束時轉到玩家背後")]
        [SerializeField] CameraController cameraController;
        [Tooltip("演出結束時，自由視角轉到玩家背後、面向這個目標（Boss）")]
        [SerializeField] Transform faceTarget;
        [Tooltip("開始播之前依玩家位置擺好演出鏡頭（可留空：鏡頭位置固定，例如開場）")]
        [SerializeField] CutsceneStage stage;

        /// <summary>演出開始播放。</summary>
        public event Action OnCutsceneStarted;
        /// <summary>演出結束、輸入已解除；參數表示是不是跳過的。</summary>
        public event Action<bool> OnCutsceneFinished;
        /// <summary>現在能不能長按跳過（開始時送一次，跳過或結束時送 false）。</summary>
        public event Action<bool> OnSkipAvailableChanged;
        /// <summary>長按跳過的進度 0..1（放開歸零）。</summary>
        public event Action<float> OnSkipProgress;
        /// <summary>顯示字卡。</summary>
        public event Action<TitleCardInfo> OnTitleCard;

        public bool IsPlaying { get; private set; }
        public bool CanSkip => IsPlaying && skippable && !skipped;

        PlayableDirector director;
        InputAction skipAction;
        bool skippable, skipped;
        float held, lastProgress;

        void Awake()
        {
            director = GetComponent<PlayableDirector>();
            var actions = InputSystem.actions;
            if (actions == null)
            {
                Debug.LogError("[Cutscene] 找不到 Project-wide Input Actions，請在 Project Settings > Input System Package 指定 InputSystem_Actions。", this);
                enabled = false;
                return;
            }
            skipAction = actions.FindAction("Player/SkipCutscene", true);
        }

        void OnEnable() => director.stopped += HandleStopped;

        void OnDisable() => director.stopped -= HandleStopped;

        /// <summary>從頭播放；canSkip = 可以長按跳過（開場第二次挑戰起；沒有 settings 時一律不能跳過）。</summary>
        public void Play(bool canSkip)
        {
            if (IsPlaying) return;
            IsPlaying = true;
            skippable = canSkip && settings != null;
            skipped = false;
            held = lastProgress = 0f;

            if (player != null) player.SetInputLocked(true);
            if (cameraController != null) cameraController.SetInputLocked(true);
            FaceGameplayCamera();
            if (stage != null) stage.Align();

            director.time = 0;
            director.Play();
            OnCutsceneStarted?.Invoke();
            OnSkipAvailableChanged?.Invoke(skippable);
        }

        void Update()
        {
            if (!CanSkip || Time.timeScale <= 0f) return; // 暫停中不累積（選單裡按 Space 改鍵不會跳過開場）

            held =skipAction.IsPressed() ? held + Time.unscaledDeltaTime : 0f;
            float progress = Mathf.Clamp01(held / settings.skipHoldTime);
            if (!Mathf.Approximately(progress, lastProgress))
            {
                lastProgress = progress;
                OnSkipProgress?.Invoke(progress);
            }
            if (held >= settings.skipHoldTime) Skip();
        }

        void Skip()
        {
            skipped = true;
            OnSkipAvailableChanged?.Invoke(false);

            double target = SkipTargetTime();
            if (target >= director.duration)
                director.Stop(); // 送出 stopped → Finish
            else if (target > director.time)
                director.time = target;
        }

        // Timeline 上 SkipTarget 標記的時間；沒有就是結尾
        double SkipTargetTime()
        {
            if (director.playableAsset is TimelineAsset timeline && timeline.markerTrack != null)
            {
                foreach (var marker in timeline.markerTrack.GetMarkers())
                {
                    if (marker is CutsceneEventMarker m && m.kind == CutsceneEvent.SkipTarget) return m.time;
                }
            }
            return director.duration;
        }

        void HandleStopped(PlayableDirector d)
        {
            if (IsPlaying) Finish();
        }

        void Finish()
        {
            IsPlaying = false;
            if (skippable && !skipped) OnSkipAvailableChanged?.Invoke(false);
            FaceGameplayCamera();
            if (player != null) player.SetInputLocked(false);
            if (cameraController != null) cameraController.SetInputLocked(false);
            OnCutsceneFinished?.Invoke(skipped);
        }

        // 演出最後混回的自由視角：玩家背後、面向 Boss（輸入鎖住時視角不會被滑鼠轉走）
        void FaceGameplayCamera()
        {
            if (cameraController == null || faceTarget == null) return;
            if (player != null) player.ClearLockTarget();
            cameraController.ResetBehind(faceTarget.position);
        }

        public void OnNotify(Playable origin, INotification notification, object context)
        {
            if (!IsPlaying || !(notification is CutsceneEventMarker marker)) return;
            if (marker.kind == CutsceneEvent.TitleCard && settings != null)
                OnTitleCard?.Invoke(new TitleCardInfo(settings.title, settings.bossName, marker.duration));
        }
    }
}
