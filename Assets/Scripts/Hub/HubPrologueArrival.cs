using BossFight.Cutscene;
using BossFight.Player;
using BossFight.Progression;
using UnityEngine;

namespace BossFight.Hub
{
    /// <summary>只認到場事件的 Hub 端協調；不引用序章戰鬥、不再次消耗 arrival。</summary>
    public class HubPrologueArrival : MonoBehaviour, IPlayerBinding
    {
        [SerializeField] SceneFlow sceneFlow;
        [SerializeField] SceneFlowData flowData;
        [SerializeField] HubManager hub;
        [SerializeField] VillageChief chief;
        [SerializeField] InputLockArbiter session;
        [SerializeField] PrologueCinematicStage stage;
        [SerializeField] CutsceneDirector wake;
        PlayerRig rig;
        bool waking;
        public void BindPlayer(PlayerRig player) => rig = player;
        void OnEnable()
        { sceneFlow.OnArrived += Arrived; wake.OnCutsceneFinished += Finished; wake.OnCutsceneCancelled += Cancel; sceneFlow.OnLoadingChanged += Loading; }
        void OnDisable()
        {
            sceneFlow.OnArrived -= Arrived; wake.OnCutsceneFinished -= Finished; wake.OnCutsceneCancelled -= Cancel; sceneFlow.OnLoadingChanged -= Loading;
            Cancel();
        }
        void Arrived(string id)
        {
            if (id != flowData.wakeArrival || rig == null || waking) return;
            waking = true; hub.DeferIntro(true); session.SetInputLocked(this, true);
            chief.PlaceForWake(rig.transform.position); stage.AlignToPlayer();
            stage.Apply(CinematicValue.Lying, 1f); stage.Apply(CinematicValue.Black, 1f);
            sceneFlow.HoldArrivalFade(); sceneFlow.TransferArrivalFade();
            wake.Play(false);
        }
        void Finished(bool skipped)
        {
            if (!waking || skipped) { Cancel(); return; }
            waking = false;
            // 先交給 Hub 的開場鎖再釋放醒來的鎖，避免關鍵幀讀進玩家操作。
            hub.ResumeIntroAfterWake();
            stage.ResetTransient(); sceneFlow.RevealArrival();
            session.SetInputLocked(this, false);
        }
        void Loading(bool loading) { if (loading) Cancel(); }
        void Cancel()
        {
            if (!waking) return;
            waking = false; if (wake != null && wake.IsPlaying) wake.Cancel();
            if (stage != null) stage.ResetTransient();
            if (session != null) session.SetInputLocked(this, false);
            if (hub != null) hub.DeferIntro(false);
            // 中止不冒充醒來完成或對白完成；換場景時黑幕仍由 SceneFlow 持有。
        }
    }
}
