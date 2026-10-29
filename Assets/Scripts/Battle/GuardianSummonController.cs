using BossFight.Boss;
using BossFight.Boss.Moves;
using BossFight.Player;
using UnityEngine;
namespace BossFight.Battle
{
    /// <summary>只管理本戰鬥池，不把教學樹精或已退場實例算進召喚名額。</summary>
    public class GuardianSummonController : MonoBehaviour, IPlayerBinding
    {
        [SerializeField] GuardianPhaseController guardian;
        [SerializeField] CallChildrenMove move;
        [SerializeField] TrainingSaplingData data;
        [SerializeField] TrainingSapling[] children;
        [Tooltip("生成點避障的效能取樣密度")]
        [Min(8), SerializeField] int searchDirections = 128;
        PlayerRig player;
        CallChildrenData Spec => (CallChildrenData)move.Data;
        public void BindPlayer(PlayerRig rig)
        {
            player = rig;
            foreach (var child in children) if (child != null) child.SetTarget(rig != null ? rig.transform : null);
        }
        void OnEnable()
        {
            move.OnSummonRequested += Summon; guardian.OnStageChanged += Stage;
        }
        void OnDisable()
        {
            move.OnSummonRequested -= Summon; guardian.OnStageChanged -= Stage;
            Clear();
        }
        void Stage(GuardianStage value) { if (value == GuardianStage.Stopped) Clear(); }
        void Update() => RefreshSlots();
        void RefreshSlots()
        {
            int live = 0; foreach (var c in children) if (c != null && c.IsAlive) live++;
            move.AvailableSlots = guardian.Stage == GuardianStage.Phase2 ? Mathf.Max(0, Mathf.Min(Spec.maxChildren, children.Length) - live) : 0;
        }
        void Summon()
        {
            if (player == null || player.Health.IsDead || guardian.Stage != GuardianStage.Phase2) return;
            RefreshSlots(); int ordinal = 0; bool spawned = false;
            foreach (var child in children)
            {
                if (child.IsAlive || move.AvailableSlots <= 0) continue;
                if (!FindPosition(ordinal++, out Vector3 p)) continue;
                child.transform.position = p;
                child.Configure(data, player.transform); child.Appear();
                spawned = true;
                RefreshSlots();
            }
            if (spawned) Physics.SyncTransforms(); // 整批移位後同步一次，當幀的揮刀不掃到池物件舊位置。
        }
        bool FindPosition(int ordinal, out Vector3 position)
        {
            var boss = guardian.Boss;
            for (int i = 0; i < searchDirections; i++)
            {
                int offset = i % 2 == 0 ? -i / 2 : (i + 1) / 2;
                float a = (ordinal % 2 == 0 ? -90f : 90f) + offset * 360f / searchDirections;
                Vector3 p = player.transform.position + Quaternion.Euler(0f, a, 0f) * player.transform.forward * Spec.spawnDistance;
                p.y = boss.ArenaCenter.y;
                if (Vector3.ProjectOnPlane(p - boss.ArenaCenter, Vector3.up).magnitude + Spec.spawnClearance > boss.Arena.radius) continue;
                if (Physics.CheckSphere(p + Vector3.up * Spec.spawnClearance, Spec.spawnClearance, 1 << LayerMask.NameToLayer("ArenaWall"), QueryTriggerInteraction.Ignore)) continue;
                // 守衛本體不在 ArenaWall layer（D6），另外用樹幹半徑避開
                if (Vector3.ProjectOnPlane(p - boss.transform.position, Vector3.up).magnitude < guardian.Data.bodyRadius + Spec.spawnClearance) continue;
                bool overlaps = false;
                foreach (var c in children) if (c.IsAlive && Vector3.Distance(c.transform.position, p) < Spec.spawnClearance * 2f) overlaps = true;
                if (!overlaps) { position = p; return true; }
            }
            position = default; return false;
        }
        public void Clear()
        {
            foreach (var c in children) if (c != null && c.IsAlive) c.Despawn();
            if (move != null) move.AvailableSlots = 0;
        }
    }
}
