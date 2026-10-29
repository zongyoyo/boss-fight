using BossFight.Boss;
using BossFight.Player;
using BossFight.Progression;
using UnityEngine;

namespace BossFight.Battle
{
    /// <summary>把生成的玩家交給教學角色；只有此上層接線引用 Player 與 Progression。</summary>
    public class PrologueActorBinding : MonoBehaviour, IPlayerBinding
    {
        [SerializeField] DaisyMentor mentor;
        [SerializeField] TrainingSapling sapling;
        [SerializeField] InputLockArbiter session;
        PlayerRig player;
        bool subscribed, whipLock;

        public void BindPlayer(PlayerRig rig)
        {
            player = rig;
            // SetTarget 只記引用，第一次呼叫時角色自己的 Awake 尚未執行也安全。
            if (mentor != null) mentor.SetTarget(player != null ? player.transform : null);
            if (sapling != null) sapling.SetTarget(player != null ? player.transform : null);
        }
        void OnEnable()
        {
            subscribed = true;
            mentor.OnActionChanged += HandleAction;
        }
        void OnDisable()
        {
            if (subscribed)
            {
                mentor.OnActionChanged -= HandleAction;
            }
            subscribed = false;
            ReleaseWhipLock();
        }

        /// <summary>開發測試用的抽打輸入鎖；T27 也可在自己的對白鎖內直接下 mentor.Whip 命令。</summary>
        public bool WhipPlayer()
        {
            if (!mentor.Whip()) return false;
            whipLock = true;
            if (session != null) session.SetInputLocked(this, true);
            return true;
        }
        void HandleAction(DaisyAction action) { if (action != DaisyAction.Whip) ReleaseWhipLock(); }
        void ReleaseWhipLock()
        {
            if (!whipLock) return;
            whipLock = false;
            if (session != null) session.SetInputLocked(this, false);
        }
    }
}
