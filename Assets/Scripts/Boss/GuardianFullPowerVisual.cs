using BossFight.Core;
using UnityEngine;

namespace BossFight.Boss
{
    /// <summary>
    /// 大樹守衛二階段（全力）的外觀：樹冠轉紅、裂縫紅光轉熾白（底色與 Emission 一起換），以及每次自損時剝落的樹皮碎片與樹皮聲（CombatFeedbackData.barkPeel）。
    /// 什麼時候進入全力由上層決定（轉階段演出的 Power cue、GuardianPhaseTwo.CompleteTransition 都會呼叫 SetFullPower，重複呼叫沒關係）。
    /// 只訂閱 BossHealth.OnSelfCost，不參與傷害。
    /// </summary>
    [RequireComponent(typeof(FeedbackEmitter))]
    public class GuardianFullPowerVisual : MonoBehaviour
    {
        [SerializeField] BossHealth health;
        [SerializeField] Renderer[] crown;
        [SerializeField] Renderer[] cracks;
        [SerializeField] ParticleSystem barkPrefab;
        [Tooltip("全力時的樹冠顏色")]
        [SerializeField] Color crownColor = new Color(0.65f, 0.015f, 0.01f);
        [Tooltip("全力時的裂縫底色")]
        [SerializeField] Color crackColor = new Color(1f, 0.9f, 0.8f);
        [Tooltip("全力時的裂縫發光（HDR）")]
        [ColorUsage(false, true)]
        [SerializeField] Color crackEmission = new Color(2.4f, 2.1f, 1.8f);
        [Tooltip("樹皮碎片出現的高度範圍 (m)")]
        [SerializeField] Vector2 chipHeight = new Vector2(2f, 5f);
        [Tooltip("樹皮碎片離樹幹中心的距離 (m)")]
        [Min(0f)] [SerializeField] float chipRadius = 1.2f;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        MaterialPropertyBlock block;
        FeedbackEmitter emitter;
        bool fullPower;

        void Awake()
        {
            block = new MaterialPropertyBlock();
            emitter = GetComponent<FeedbackEmitter>();
        }

        void OnEnable() => health.OnSelfCost += HandleSelfCost;
        void OnDisable() => health.OnSelfCost -= HandleSelfCost;

        public void SetFullPower() => fullPower = true;

        void LateUpdate()
        {
            if (!fullPower) return;
            Tint(crown, crownColor, null);
            Tint(cracks, crackColor, crackEmission);
        }

        void Tint(Renderer[] renderers, Color color, Color? emission)
        {
            foreach (var r in renderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(block);
                block.SetColor(BaseColorId, color);
                if (emission.HasValue) block.SetColor(EmissionColorId, emission.Value);
                r.SetPropertyBlock(block);
            }
        }

        void HandleSelfCost(float before, float after, float max)
        {
            Vector3 direction = Quaternion.Euler(0f, Random.value * 360f, 0f) * Vector3.forward;
            if (barkPrefab != null)
            {
                Vector3 at = transform.position + direction * chipRadius + Vector3.up * Random.Range(chipHeight.x, chipHeight.y);
                Instantiate(barkPrefab, at, Quaternion.LookRotation(direction));
            }
            if (emitter.Data != null) emitter.Play(emitter.Data.barkPeel, transform.position);
        }
    }
}
