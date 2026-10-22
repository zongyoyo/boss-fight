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
    /// 播放開場動畫（同一個物件上 PlayableDirector 的 Timeline，Assets/Timelines/Intro.playable），期間鎖住玩家與鏡頭的輸入、由 Timeline 的 Cinemachine Track 接管鏡頭。
    /// BattleManager 呼叫 PlayIntro；skippable 時長按 Player/SkipCutscene（Space）settings.skipHoldTime 秒，跳到 Timeline 上的 SkipTarget 標記（第 7 鏡）。
    /// 播完時把自由視角擺到玩家背後、面向 Boss，解除輸入鎖定，送出 OnIntroFinished；BattleManager 收到後讓 Boss 開打。
    /// 字卡由 Timeline 的 CutsceneEventMarker 觸發，轉成 OnTitleCard；UI 只訂閱這裡的事件。
    /// Timeline 上的自訂 Track 都依「目前時間」算出畫面狀態，所以直接改 director.time 跳過也會得到正確的結果。
    /// </summary>
    [RequireComponent(typeof(PlayableDirector))]
    [DisallowMultipleComponent]
    public class CutsceneDirector : MonoBehaviour, INotificationReceiver
    {
        [SerializeField] IntroCutsceneSettings settings;
        [Tooltip("開場期間鎖住輸入，由 Timeline 帶著走進場")]
        [SerializeField] PlayerController player;
        [Tooltip("開場期間鎖住視角；結束時轉到玩家背後")]
        [SerializeField] CameraController cameraController;
        [Tooltip("開場結束時，自由視角轉到玩家背後、面向這個目標（Boss）")]
        [SerializeField] Transform faceTarget;

        /// <summary>開場開始播放。</summary>
        public event Action OnIntroStarted;
        /// <summary>開場結束、輸入已解除；參數表示是不是跳過的。</summary>
        public event Action<bool> OnIntroFinished;
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

        /// <summary>從頭播放開場；skippable = 第二次挑戰起可以長按跳過。</summary>
        public void PlayIntro(bool canSkip)
        {
            if (IsPlaying) return;
            IsPlaying = true;
            skippable = canSkip;
            skipped = false;
            held = lastProgress = 0f;

            if (player != null) player.SetInputLocked(true);
            if (cameraController != null) cameraController.SetInputLocked(true);
            FaceGameplayCamera();

            director.time = 0;
            director.Play();
            OnIntroStarted?.Invoke();
            OnSkipAvailableChanged?.Invoke(skippable);
        }

        void Update()
        {
            if (!CanSkip) return;

            held = skipAction.IsPressed() ? held + Time.unscaledDeltaTime : 0f;
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
            OnIntroFinished?.Invoke(skipped);
        }

        // 第 6 鏡混回的自由視角：玩家背後、面向 Boss（輸入鎖住時視角不會被滑鼠轉走）
        void FaceGameplayCamera()
        {
            if (cameraController == null || faceTarget == null) return;
            if (player != null) player.ClearLockTarget();
            cameraController.ResetBehind(faceTarget.position);
        }

        public void OnNotify(Playable origin, INotification notification, object context)
        {
            if (!IsPlaying || !(notification is CutsceneEventMarker marker)) return;
            if (marker.kind == CutsceneEvent.TitleCard)
                OnTitleCard?.Invoke(new TitleCardInfo(settings.title, settings.bossName, marker.duration));
        }
    }
}
