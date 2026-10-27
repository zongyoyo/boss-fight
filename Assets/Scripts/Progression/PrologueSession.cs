using System.Collections.Generic;
using BossFight.Cameras;
using BossFight.Core;
using BossFight.Player;
using UnityEngine;

namespace BossFight.Progression
{
    /// <summary>序章共用的暫停與輸入鎖。教學及演出各用自己的 owner，不能解除別人的鎖。</summary>
    [DisallowMultipleComponent]
    public class PrologueSession : MonoBehaviour, IPlayerBinding
    {
        [SerializeField] CameraController cameraController;
        [SerializeField] GamePause pause;
        [SerializeField] GameOptions options;
        [SerializeField] SceneFlow sceneFlow;

        readonly HashSet<object> inputLocks = new HashSet<object>();
        PlayerController player;
        bool paused, loading, subscribed, locked;

        public void BindPlayer(PlayerRig rig)
        {
            player = rig != null ? rig.Controller : null;
            if (player != null && locked) player.SetInputLocked(true);
        }

        void OnEnable()
        {
            subscribed = true;
            if (pause != null) pause.OnPausedChanged += HandlePause;
            if (sceneFlow != null) sceneFlow.OnLoadingChanged += HandleLoading;
            if (options != null)
            {
                options.OnSkipPrologueChanged += HandleSkip;
                options.OnDeveloperModeChanged += HandleDeveloperMode;
            }
        }

        void OnDisable()
        {
            subscribed = false;
            if (pause != null) pause.OnPausedChanged -= HandlePause;
            if (sceneFlow != null) sceneFlow.OnLoadingChanged -= HandleLoading;
            if (options != null)
            {
                options.OnSkipPrologueChanged -= HandleSkip;
                options.OnDeveloperModeChanged -= HandleDeveloperMode;
            }
        }

        /// <summary>owner 用呼叫方元件；結束及 OnDisable 時釋放自己的鎖。首次 Bind 早於 Awake，不在 Bind 訂閱。</summary>
        public void SetInputLocked(object owner, bool value)
        {
            if (owner == null) return;
            if (value)
            {
                inputLocks.Add(owner);
                // 取得鎖立刻生效；釋放留到 LateUpdate，避免關對白的同一顆按鍵觸發玩家動作。
                locked = true;
                if (player != null) player.SetInputLocked(true);
                if (cameraController != null) cameraController.SetInputLocked(true);
            }
            else inputLocks.Remove(owner);
        }

        void HandlePause(bool value) => paused = value;
        void HandleLoading(bool value) => loading = value;
        void HandleSkip(bool _) => TrySkip();
        void HandleDeveloperMode(bool _) => TrySkip();

        void TrySkip()
        {
            if (subscribed && options != null && options.CanSkipPrologue && sceneFlow != null)
                sceneFlow.SkipPrologue();
        }

        void LateUpdate()
        {
            bool next = paused || loading || inputLocks.Count > 0;
            if (next != locked)
            {
                locked = next;
                if (player != null) player.SetInputLocked(locked);
                if (cameraController != null) cameraController.SetInputLocked(locked);
            }
            bool releaseCursor = paused || loading;
            // 反覆鎖游標會每幀重設鏡頭的滑鼠忽略計數，導致滑鼠永遠不能轉視角。
            if (cameraController != null && cameraController.CursorReleased != releaseCursor)
                cameraController.SetCursorReleased(releaseCursor);
        }
    }
}
