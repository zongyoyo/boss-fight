using BossFight.Core;
using UnityEngine;
namespace BossFight.Boss
{
    /// <summary>二階段樹冠與裂縫、自損樹皮碎片；只訂閱事件，不參與傷害。</summary>
    [RequireComponent(typeof(FeedbackEmitter))]
    public class GuardianFullPowerVisual : MonoBehaviour
    {
        [SerializeField] BossController boss;
        [SerializeField] BossHealth health;
        [SerializeField] Renderer[] crown;
        [SerializeField] Renderer[] cracks;
        [SerializeField] ParticleSystem barkPrefab;
        [SerializeField] Color crownColor = new Color(0.65f, 0.015f, 0.01f);
        [SerializeField] Color crackColor = new Color(1f, 0.9f, 0.8f);
        [SerializeField] Vector2 chipHeight = new Vector2(2f, 5f);
        [SerializeField] float chipRadius = 1.2f;
        MaterialPropertyBlock block;
        FeedbackEmitter emitter;
        bool fullPower;
        void Awake() { block = new MaterialPropertyBlock(); emitter = GetComponent<FeedbackEmitter>(); }
        void OnEnable() { boss.OnPhaseChanged += Phase; health.OnSelfCost += Cost; }
        void OnDisable() { boss.OnPhaseChanged -= Phase; health.OnSelfCost -= Cost; }
        void Phase(BossPhaseData phase) { if (phase.movePool == 2) SetFullPower(); }
        public void SetFullPower() => fullPower = true;
        void LateUpdate()
        {
            if (!fullPower) return;
            Tint(crown, crownColor); Tint(cracks, crackColor);
        }
        void Tint(Renderer[] renderers, Color color)
        {
            foreach (var r in renderers)
            { if (r == null) continue; r.GetPropertyBlock(block); block.SetColor("_BaseColor", color); r.SetPropertyBlock(block); }
        }
        void Cost(float before, float after, float max)
        {
            Vector3 direction = Quaternion.Euler(0f, Random.value * 360f, 0f) * Vector3.forward;
            if (barkPrefab != null) Instantiate(barkPrefab, transform.position + direction * chipRadius + Vector3.up * Random.Range(chipHeight.x, chipHeight.y), Quaternion.LookRotation(direction));
            if (emitter.Data != null) emitter.Play(emitter.Data.hitDeflected, transform.position);
        }
    }
}
