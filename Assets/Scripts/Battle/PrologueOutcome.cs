using System;
using BossFight.Player;
using BossFight.Progression;
using UnityEngine;
namespace BossFight.Battle
{
    /// <summary>序章結局的唯一入口；T28 只攔截倒下，不建立一般敗北與存檔副作用。</summary>
    public class PrologueOutcome : MonoBehaviour, IPlayerBinding
    {
        [SerializeField] GuardianPhaseController guardian;
        [SerializeField] PrologueSession session;
        public event Action OnKnockdownRequested;
        public bool HasOutcome { get; private set; }
        PlayerHealth health;
        bool subscribed;
        public void BindPlayer(PlayerRig rig)
        {
            if (subscribed && health != null) health.OnDied -= Died;
            health = rig != null ? rig.Health : null;
            if (subscribed && health != null) health.OnDied += Died;
        }
        void OnEnable() { subscribed = true; if (health != null) health.OnDied += Died; }
        void OnDisable() { if (health != null) health.OnDied -= Died; subscribed = false; session.SetInputLocked(this, false); }
        void Died()
        {
            if (HasOutcome || guardian.Stage == GuardianStage.Dormant) return;
            HasOutcome = true; session.SetInputLocked(this, true); guardian.StopBattle();
            OnKnockdownRequested?.Invoke(); Debug.Log("[Prologue] 玩家倒下：交接擊倒演出（T30），不顯示一般敗北、不存檔。", this);
        }
    }
}
