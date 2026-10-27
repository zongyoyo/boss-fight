using BossFight.Core;
using BossFight.Hazards;
using BossFight.Player;
using UnityEngine;

namespace BossFight.Battle
{
    /// <summary>序章玩家的補充可見回饋；聲音仍由角色既有 PlayerFeedback 播放。</summary>
    public class TutorialPlayerVisual : MonoBehaviour, IPlayerBinding
    {
        [SerializeField] TimedFx slashPrefab;
        [SerializeField] TimedFx ringPrefab;
        [SerializeField] TimedFx sparkPrefab;
        [SerializeField] float bodyHeight = 1f;
        [SerializeField] float slashSize = 0.7f;
        [SerializeField] float rollSize = 0.5f;
        [SerializeField] float hitSize = 0.8f;
        PlayerRig player;
        bool subscribed;
        public void BindPlayer(PlayerRig rig)
        {
            if (subscribed) Unsubscribe();
            player = rig;
            if (subscribed) Subscribe();
        }
        void OnEnable() { subscribed = true; Subscribe(); }
        void OnDisable() { Unsubscribe(); subscribed = false; }
        void Subscribe()
        {
            if (player == null) return;
            player.Attack.OnSwing += Swing;
            player.Controller.OnRollStarted += Roll;
            player.Health.OnDamageTaken += Hurt;
        }
        void Unsubscribe()
        {
            if (player == null) return;
            player.Attack.OnSwing -= Swing;
            player.Controller.OnRollStarted -= Roll;
            player.Health.OnDamageTaken -= Hurt;
        }
        void Swing() => TimedFx.Spawn(slashPrefab, player.transform.position + Vector3.up * bodyHeight + player.transform.forward,
            player.transform.rotation, Color.white, slashSize);
        // 空滾只有灰色動作環；綠色成功閃避由 T26 BossHitFeedback 結算後播放。
        void Roll(Vector3 direction) => TimedFx.Spawn(ringPrefab, player.transform.position, Quaternion.identity,
            Color.gray, rollSize);
        void Hurt(PlayerDamageReport report)
        {
            if (report.result == DamageResult.Hit && report.info.ignoreDefense)
                TimedFx.Spawn(sparkPrefab, player.transform.position + Vector3.up * bodyHeight, Quaternion.identity,
                    new Color(3f, 0.35f, 0.25f), hitSize);
        }
    }
}
