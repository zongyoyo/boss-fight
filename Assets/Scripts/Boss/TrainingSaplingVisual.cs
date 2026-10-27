using BossFight.Core;
using BossFight.Hazards;
using UnityEngine;

namespace BossFight.Boss
{
    /// <summary>樹精佔位姿勢與落葉；只訂閱戰鬥事件，不參與傷害判定。</summary>
    public class TrainingSaplingVisual : MonoBehaviour
    {
        [SerializeField] TrainingSapling sapling;
        [SerializeField] Transform model;
        [SerializeField] Transform branch;
        [SerializeField] Transform stars;
        [SerializeField] Renderer[] renderers;
        [SerializeField] ParticleSystem leavesPrefab;
        [Tooltip("沿用 OIIA 的地面預告；依 SO 的攻擊範圍與預兆時間填滿")]
        [SerializeField] GroundTelegraph telegraphPrefab;
        [SerializeField] float emergeTime = 0.4f;
        [SerializeField] float hurtTime = 0.25f;
        [SerializeField] [ColorUsage(true, true)] Color blockableGlow = Color.white;
        [SerializeField] [ColorUsage(true, true)] Color unblockableGlow = new Color(1f, 0.12f, 0.08f, 1f);
        Vector3 rest;
        Quaternion branchRest;
        MaterialPropertyBlock block;
        float clock, telegraphLeft, activeLeft, hurtLeft, emergeLeft;
        bool stunned;
        GroundTelegraph telegraph;
        bool warning;
        float telegraphStartedAt;
        Color attackGlow = Color.white;
        static readonly int Emission = Shader.PropertyToID("_EmissionColor");

        void Awake()
        {
            block = new MaterialPropertyBlock();
            rest = model.localPosition;
            branchRest = branch.localRotation;
            model.gameObject.SetActive(false);
            stars.gameObject.SetActive(false);
            if (telegraphPrefab != null)
            {
                telegraph = Instantiate(telegraphPrefab);
                telegraph.name = name + "_Telegraph";
            }
        }
        void OnEnable()
        {
            sapling.OnAppeared += HandleAppeared;
            sapling.OnViewChanged += HandleView;
            sapling.OnAttackWarning += HandleTelegraph;
            sapling.OnAttackWindowOpened += HandleWindow;
            sapling.OnAttackResolved += HandleResolved;
            sapling.OnDamaged += HandleDamaged;
            sapling.OnDefeated += HandleDefeated;
        }
        void OnDisable()
        {
            HideTelegraph();
            sapling.OnAppeared -= HandleAppeared;
            sapling.OnViewChanged -= HandleView;
            sapling.OnAttackWarning -= HandleTelegraph;
            sapling.OnAttackWindowOpened -= HandleWindow;
            sapling.OnAttackResolved -= HandleResolved;
            sapling.OnDamaged -= HandleDamaged;
            sapling.OnDefeated -= HandleDefeated;
        }
        void OnDestroy()
        {
            if (telegraph != null) Destroy(telegraph.gameObject);
        }
        void HandleAppeared()
        {
            HideTelegraph();
            emergeLeft = emergeTime;
            clock = hurtLeft = telegraphLeft = activeLeft = 0f;
        }
        void HandleView(SaplingView view)
        {
            model.gameObject.SetActive(view.visible);
            stunned = view.stunned && view.visible;
            stars.gameObject.SetActive(stunned);
            if (!view.visible || view.stunned || (view.mode != SaplingMode.Attack && view.mode != SaplingMode.DodgeAttack)) HideTelegraph();
        }
        void HandleTelegraph(SaplingTelegraph info)
        {
            telegraphLeft = info.duration;
            telegraphStartedAt = Time.time;
            attackGlow = info.canBlock ? blockableGlow : unblockableGlow;
            warning = true;
            if (telegraph != null)
                telegraph.ShowRect(GroundPosition, transform.forward, info.reach, info.width, info.canBlock);
        }
        void HandleWindow(int _)
        {
            warning = false;
            telegraphLeft = 0f;
            activeLeft = sapling.Data.activeTime;
            if (telegraph == null) return;
            telegraph.SetFill(1f);
            telegraph.Strike();
            telegraph.Hide();
        }
        void HandleResolved(SaplingAttackReport report)
        {
            telegraphLeft = activeLeft = 0f;
            warning = false;
            if (telegraph == null) return;
            if (report.cancelled) telegraph.HideImmediate();
            else telegraph.Hide();
        }
        Vector3 GroundPosition => new Vector3(transform.position.x, 0f, transform.position.z);
        void HideTelegraph()
        {
            warning = false;
            if (telegraph != null) telegraph.HideImmediate();
        }
        void HandleDamaged(SaplingDamageReport hit) { if (hit.result == DamageResult.Hit) hurtLeft = hurtTime; }
        void HandleDefeated(SaplingDefeatReason _)
        {
            if (leavesPrefab != null)
            {
                var leaves = Instantiate(leavesPrefab, transform.position + Vector3.up * (0.45f * model.lossyScale.y), Quaternion.identity);
                leaves.transform.localScale = model.lossyScale;
            }
        }
        void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || !model.gameObject.activeSelf) return;
            if (warning && telegraph != null)
            {
                // 與 OIIA 相同：範圍跟著預兆轉向，填滿那一刻就是出手；不受模型兩倍縮放影響。
                telegraph.MoveRect(GroundPosition, transform.forward, sapling.Data.reach, sapling.Data.width);
                telegraph.SetFill(sapling.Data.telegraphTime > 0f ? (Time.time - telegraphStartedAt) / sapling.Data.telegraphTime : 1f);
            }
            clock += dt;
            emergeLeft = Mathf.Max(0f, emergeLeft - dt);
            hurtLeft = Mathf.Max(0f, hurtLeft - dt);
            float rise = emergeTime > 0f ? Mathf.SmoothStep(0f, 1f, emergeLeft / emergeTime) : 0f;
            model.localPosition = rest - Vector3.up * (rise * model.localScale.y);
            float hurt = hurtTime > 0f ? hurtLeft / hurtTime : 0f;
            float lean = stunned ? Mathf.Sin(clock * 5f) * 9f : Mathf.Sin(clock * 2f) * 2f;
            model.localRotation = Quaternion.Euler(-hurt * 18f, 0f, lean);
            float angle = 0f;
            float glow = 0f;
            if (telegraphLeft > 0f)
            {
                telegraphLeft = Mathf.Max(0f, telegraphLeft - dt);
                float k = sapling.Data.telegraphTime > 0f ? 1f - telegraphLeft / sapling.Data.telegraphTime : 1f;
                angle = -100f * k;
                glow = k;
            }
            else if (activeLeft > 0f)
            {
                activeLeft = Mathf.Max(0f, activeLeft - dt);
                float k = sapling.Data.activeTime > 0f ? 1f - activeLeft / sapling.Data.activeTime : 1f;
                angle = Mathf.Lerp(-100f, 70f, k);
                glow = 1f;
            }
            branch.localRotation = branchRest * Quaternion.Euler(0f, angle, stunned ? -35f : 0f);
            if (stunned) stars.Rotate(0f, 220f * dt, 0f, Space.Self);
            foreach (var renderer in renderers)
            {
                if (renderer == null) continue;
                renderer.GetPropertyBlock(block);
                block.SetColor(Emission, attackGlow * glow + Color.white * hurt);
                renderer.SetPropertyBlock(block);
            }
        }
    }
}
