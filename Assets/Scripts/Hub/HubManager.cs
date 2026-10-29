using System;
using BossFight.Cameras;
using BossFight.Core;
using BossFight.Player;
using BossFight.Progression;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

namespace BossFight.Hub
{
    /// <summary>
    /// 休息區的狀態（T20）：自由走動，或某個站點的介面開著。
    /// 自由走動時玩家在站點範圍內（InteractionScanner 的焦點）按 Player/Interact（按一下就觸發）打開那個站點：
    /// 站點鏡頭的優先度調高（Cinemachine 混過去）、玩家用 ScriptedMoveTo 以 HubLayoutData.approachSpeed 走到站位點並面向站點，送 OnStationOpened。
    /// 介面（HubStationUI）讀 UI/Cancel（Esc、手把 B）後呼叫 CloseStation：鏡頭優先度還原、自由視角擺到玩家背後面向站點，送 OnStationClosed。
    /// 輸入鎖定：介面開著時經 InputLockArbiter（owner 是自己）鎖住玩家與鏡頭、放開游標；ESC 選單暫停由 arbiter 鎖。
    /// 釋放在 arbiter 的 LateUpdate，解除那一幀玩家的 Update 已經跑完，關介面用的手把 B（翻滾）不會被當成動作。介面開著時 ESC 選單不開（PauseMenuUI 訂閱這裡的事件）。
    /// 也提供 ESC 選單秘技的解鎖（UnlockDeveloperOptions，記在 BattleSession，跟 Boss 戰共用）。
    /// 開場劇情（T24）：存檔沒看過時，Start 後等 VillageChief.IntroDelay 秒（期間就鎖住輸入），自由視角轉到玩家背後看著村長、打開村長站點；
    /// 站點還沒 ReadyToTalk（村長走過來的路上）時不切站點鏡頭、玩家站著面向他，準備好了才切鏡頭、照常走到站位點。
    /// </summary>
    [DisallowMultipleComponent]
    public class HubManager : MonoBehaviour, IPlayerBinding
    {
        [SerializeField] HubLayoutData layout;
        [SerializeField] InteractionScanner scanner;
        [SerializeField] CameraController cameraController;
        [SerializeField] GamePause pause;
        [Tooltip("輸入鎖與游標的仲裁（休息區的介面、開場劇情與醒來演出共用）")]
        [FormerlySerializedAs("presentationSession")]
        [SerializeField] InputLockArbiter inputLock;
        [Tooltip("村長：存檔沒看過開場劇情時，進休息區後自動走過來講話（T24）；留空時沒有開場劇情")]
        [SerializeField] VillageChief chief;
        [Tooltip("介面開著時站點鏡頭的優先度（要比自由視角、鎖定鏡頭高）")]
        [SerializeField] int stationCameraPriority = 20;

        /// <summary>打開某個站點的介面。</summary>
        public event Action<Interactable> OnStationOpened;
        /// <summary>關掉某個站點的介面。</summary>
        public event Action<Interactable> OnStationClosed;
        /// <summary>這次執行在 ESC 選單輸入過秘技（Start 時也會送一次）。</summary>
        public event Action OnDeveloperOptionsUnlocked;

        /// <summary>目前開著的站點；自由走動時 null。</summary>
        public Interactable Current { get; private set; }
        public bool IsStationOpen => Current != null;

        PlayerRig rig;
        PlayerController player;
        InputAction interactAction;
        bool locked, cameraRaised, introPending;
        int savedPriority;
        float introAt;
        bool introDeferred;
        /// <summary>醒來演出持有自己的 owner 鎖；Hub 先不開始村長對話。</summary>
        public void DeferIntro(bool value) => introDeferred = value;
        public void ResumeIntroAfterWake()
        { introDeferred = false; introPending = chief != null && chief.WantsIntro; introAt = Time.time; }

        /// <summary>PlayerSpawner 生成（或換）角色時呼叫；介面開著時換的新角色一樣鎖住。</summary>
        public void BindPlayer(PlayerRig rig)
        {
            this.rig = rig;
            player = rig != null ? rig.Controller : null;
        }

        void Awake()
        {
            var actions = InputSystem.actions;
            if (actions != null) interactAction = actions.FindAction("Player/Interact", true);
            else Debug.LogError("[Hub] 找不到 Project-wide Input Actions，站點不能互動。", this);
        }

        void OnEnable() => interactAction?.Enable();

        void Start()
        {
            if (BattleSession.DeveloperOptionsUnlocked) OnDeveloperOptionsUnlocked?.Invoke();
            if (chief != null && chief.WantsIntro)
            {
                introPending = true;
                introAt = Time.time + chief.IntroDelay;
            }
        }

        void Update()
        {
            if (introDeferred) return;
            if (Current != null)
            {
                Approach();
                return;
            }
            if (introPending)
            {
                if (Time.time < introAt || player == null || player.IsDead || (pause != null && pause.IsPaused)) return;
                introPending = false;
                chief.BeginIntro(player.transform.position);
                Open(chief);
                return;
            }
            if (interactAction == null || !interactAction.WasPressedThisFrame()) return;
            if ((pause != null && pause.IsPaused) || player == null || player.IsDead || !player.IsGrounded) return;
            var target = scanner != null ? scanner.Focus : null;
            if (target != null) Open(target);
        }

        // 介面開著、開場劇情與醒來演出時鎖住輸入（暫停由 InputLockArbiter 自己鎖），介面開著時放開游標
        void LateUpdate()
        {
            if (inputLock == null) return;
            bool shouldLock = Current != null || introPending || introDeferred;
            if (shouldLock != locked)
            {
                locked = shouldLock;
                inputLock.SetInputLocked(this, locked);
            }
            inputLock.SetCursorReleased(this, Current != null);
        }

        void Open(Interactable target)
        {
            Current = target;
            cameraRaised = false;
            player.ClearLockTarget();
            if (target.ReadyToTalk) RaiseStationCamera();
            else if (cameraController != null) cameraController.ResetBehind(target.transform.position); // 站在玩家背後看著走過來的村長
            target.Opened(rig);
            OnStationOpened?.Invoke(target);
        }

        /// <summary>關掉目前的介面（HubStationUI 按返回時呼叫）；沒有開著時不做事。</summary>
        public void CloseStation()
        {
            if (Current == null) return;
            var closed = Current;
            Current = null;
            var cam = closed.StationCamera;
            if (cam != null && cameraRaised) cam.Priority = savedPriority;
            cameraRaised = false;
            if (cameraController != null && player != null) cameraController.ResetBehind(closed.transform.position);
            closed.Closed(rig);
            OnStationClosed?.Invoke(closed);
        }

        /// <summary>ESC 選單的秘技：記在 BattleSession（Boss 戰與休息區共用，重開遊戲要重輸）。</summary>
        public void UnlockDeveloperOptions()
        {
            if (BattleSession.DeveloperOptionsUnlocked) return;
            BattleSession.MarkDeveloperOptionsUnlocked();
            OnDeveloperOptionsUnlocked?.Invoke();
        }

        void RaiseStationCamera()
        {
            cameraRaised = true;
            var cam = Current.StationCamera;
            if (cam == null) return;
            savedPriority = cam.Priority.Value;
            cam.Priority = stationCameraPriority;
        }

        // 照腳本走到站位點、面向站點（輸入在 LateUpdate 鎖住後才生效）；走到了就告訴站點（營火坐下）。
        // 站點還沒準備好（村長走過來的路上）時站著面向它
        void Approach()
        {
            if (player == null || layout == null) return;
            if (!Current.ReadyToTalk)
            {
                Vector3 here = player.transform.position;
                player.ScriptedMoveTo(here, Current.transform.position - here);
                Current.Tick(rig, false);
                return;
            }
            if (!cameraRaised) RaiseStationCamera();
            Vector3 stand = Current.StandPoint.position;
            Vector3 from = player.transform.position;
            Vector3 flat = new Vector3(from.x, stand.y, from.z);
            float step = layout.approachSpeed * Time.deltaTime;
            bool arrived = (flat - stand).sqrMagnitude <= step * step;
            Vector3 next = Vector3.MoveTowards(flat, stand, step);
            next.y = from.y;
            player.ScriptedMoveTo(next, Current.transform.position - stand);
            Current.Tick(rig, arrived);
        }
    }
}
