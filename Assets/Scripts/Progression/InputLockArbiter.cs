using System.Collections.Generic;
using BossFight.Cameras;
using BossFight.Core;
using BossFight.Player;
using UnityEngine;

namespace BossFight.Progression
{
    /// <summary>
    /// 多個流程共用的輸入鎖與游標仲裁（休息區、序章）：每個流程用自己（呼叫方元件）當 owner 上鎖，不能解除別人的鎖；
    /// 有任何 owner、ESC 選單暫停或換場景中時，鎖住玩家與鏡頭的輸入。游標另外由 owner 要求放開（介面、結算畫面）。
    /// 取得鎖立刻生效；釋放留到 LateUpdate，關對白的同一顆按鍵不會被玩家當成動作（跟 BattleManager 一樣）。
    /// BossArena 仍由 BattleManager 自己上鎖。
    /// </summary>
    [DisallowMultipleComponent]
    public class InputLockArbiter : MonoBehaviour, IPlayerBinding
    {
        [SerializeField] CameraController cameraController;
        [SerializeField] GamePause pause;
        [SerializeField] SceneFlow sceneFlow;

        readonly HashSet<object> inputLocks = new HashSet<object>();
        readonly HashSet<object> cursorOwners = new HashSet<object>();
        PlayerController player;
        bool paused, loading, locked;

        /// <summary>PlayerSpawner 生成（或換）角色時呼叫；鎖著時新角色一樣鎖住。首次 Bind 早於 Awake，不在這裡訂閱。</summary>
        public void BindPlayer(PlayerRig rig)
        {
            player = rig != null ? rig.Controller : null;
            if (player != null && locked) player.SetInputLocked(true);
        }

        void OnEnable()
        {
            if (pause != null) pause.OnPausedChanged += HandlePause;
            if (sceneFlow != null) sceneFlow.OnLoadingChanged += HandleLoading;
        }

        void OnDisable()
        {
            if (pause != null) pause.OnPausedChanged -= HandlePause;
            if (sceneFlow != null) sceneFlow.OnLoadingChanged -= HandleLoading;
        }

        /// <summary>owner 用呼叫方元件；結束及 OnDisable 時釋放自己的鎖。</summary>
        public void SetInputLocked(object owner, bool value)
        {
            if (owner == null) return;
            if (value)
            {
                inputLocks.Add(owner);
                locked = true;
                if (player != null) player.SetInputLocked(true);
                if (cameraController != null) cameraController.SetInputLocked(true);
            }
            else inputLocks.Remove(owner);
        }

        /// <summary>結算介面等 owner 可獨立要求放開游標；不影響輸入鎖。</summary>
        public void SetCursorReleased(object owner, bool value)
        {
            if (owner == null) return;
            if (value) cursorOwners.Add(owner);
            else cursorOwners.Remove(owner);
        }

        void HandlePause(bool value) => paused = value;
        void HandleLoading(bool value) => loading = value;

        void LateUpdate()
        {
            bool next = paused || loading || inputLocks.Count > 0;
            if (next != locked)
            {
                locked = next;
                if (player != null) player.SetInputLocked(locked);
                if (cameraController != null) cameraController.SetInputLocked(locked);
            }
            bool releaseCursor = paused || loading || cursorOwners.Count > 0;
            // 反覆鎖游標會每幀重設鏡頭的滑鼠忽略計數，導致滑鼠永遠不能轉視角
            if (cameraController != null && cameraController.CursorReleased != releaseCursor)
                cameraController.SetCursorReleased(releaseCursor);
        }
    }
}
