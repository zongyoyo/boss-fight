using System;
using BossFight.Core;
using BossFight.Hazards;
using UnityEngine;

namespace BossFight.Boss
{
    /// <summary>
    /// 幻影分身的一隻幻影（佔位）：半透明的 OIIA 貓，跟本體一樣會伏低扭屁股、飛撲（幻影分身招式用 PounceRunner 驅動這裡的 Poser 與 Hitbox）。
    /// 身上有自己的 Hurtbox：被打到任何一下就碎掉（OnShattered），不扣 Boss 的血、不加失衡。Appear／Vanish 淡入淡出，平常整隻藏起來。
    /// Telegraph 讓幻影自己閃一下預兆光（Boss 身上的 BossTelegraphFlash 只管本體）。
    /// 顏色用 MaterialPropertyBlock 改 _BaseColor（半透明的 Particles/Unlit 材質）。這裡的數字全是視覺參數。
    /// </summary>
    [DisallowMultipleComponent]
    public class BossPhantom : MonoBehaviour, IDamageable
    {
        [SerializeField] OiiaCatPoser poser;
        [Tooltip("飛撲落地的判定；攻擊方是這隻幻影（格擋方向以它為準）")]
        [SerializeField] Hitbox hitbox;
        [Tooltip("受擊範圍的 trigger（藏起來時關掉）")]
        [SerializeField] Collider hurtCollider;
        [Tooltip("模型的根（藏起來時整個關掉）")]
        [SerializeField] GameObject model;
        [SerializeField] Renderer[] renderers;
        [SerializeField] [ColorUsage(true, true)] Color color = new Color(1.6f, 0.15f, 0.12f, 0.42f);
        [Tooltip("預兆最亮時乘在顏色上的倍率")]
        [Min(1f)] [SerializeField] float glowIntensity = 2.5f;
        [Tooltip("預兆期間每秒閃幾下")]
        [Min(0f)] [SerializeField] float blinkRate = 4f;
        [Tooltip("淡入淡出時間（秒）")]
        [Min(0.01f)] [SerializeField] float fadeTime = 0.25f;
        [Tooltip("碎掉時的特效（可留空）")]
        [SerializeField] TimedFx shatterPrefab;
        [SerializeField] [ColorUsage(true, true)] Color shatterColor = new Color(2.4f, 0.5f, 0.4f, 1f);
        [Min(0f)] [SerializeField] float shatterSize = 2.2f;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        /// <summary>被打碎了。</summary>
        public event Action<BossPhantom> OnShattered;

        public OiiaCatPoser Poser => poser;
        public Hitbox Hitbox => hitbox;
        /// <summary>看得到、還沒碎（會飛撲、打得到）。</summary>
        public bool IsAlive => shown && !shattered;

        MaterialPropertyBlock block;
        bool shown, shattered;
        float alpha, telegraphTime, telegraphTimer;

        void Awake()
        {
            block = new MaterialPropertyBlock();
            SetVisible(false);
        }

        /// <summary>在 position 淡入出現。</summary>
        public void Appear(Vector3 position, Quaternion rotation)
        {
            transform.SetPositionAndRotation(position, rotation);
            shown = true;
            shattered = false;
            alpha = 0f;
            telegraphTimer = 0f;
            poser.ResetPose();
            SetVisible(true);
            Apply();
        }

        /// <summary>淡出後藏起來（招式結束）。</summary>
        public void Vanish()
        {
            shown = false;
            hitbox.Deactivate();
            if (hurtCollider != null) hurtCollider.enabled = false;
        }

        /// <summary>預兆光：duration 秒內一明一暗越閃越亮。</summary>
        public void Telegraph(float duration)
        {
            telegraphTime = telegraphTimer = Mathf.Max(1e-3f, duration);
        }

        /// <summary>水平轉向 point，每秒最多 degreesPerSecond 度。</summary>
        public void FaceToward(Vector3 point, float degreesPerSecond, float dt)
        {
            Vector3 to = point - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude < 1e-4f) return;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(to), degreesPerSecond * dt);
        }

        public DamageOutcome TakeDamage(in DamageInfo info)
        {
            if (!IsAlive) return DamageResult.Ignored;
            shattered = true;
            alpha = 0f;
            hitbox.Deactivate();
            SetVisible(false);
            TimedFx.Spawn(shatterPrefab, transform.position + Vector3.up, Quaternion.identity, shatterColor, shatterSize);
            OnShattered?.Invoke(this);
            return DamageResult.Hit;
        }

        void Update()
        {
            if (!model.activeSelf) return;
            float dt = Time.deltaTime;
            alpha = Mathf.MoveTowards(alpha, shown ? 1f : 0f, dt / fadeTime);
            if (telegraphTimer > 0f) telegraphTimer = Mathf.Max(0f, telegraphTimer - dt);
            Apply();
            if (!shown && alpha <= 0f) SetVisible(false);
        }

        void Apply()
        {
            float glow = 1f;
            if (telegraphTimer > 0f)
            {
                float u = 1f - telegraphTimer / telegraphTime;
                float blink = 0.5f + 0.5f * Mathf.Cos(u * telegraphTime * blinkRate * Mathf.PI * 2f);
                glow = Mathf.Lerp(1f, glowIntensity, u * blink);
            }
            Color c = color * glow;
            c.a = color.a * alpha;
            foreach (var r in renderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(block);
                block.SetColor(BaseColorId, c);
                r.SetPropertyBlock(block);
            }
        }

        void SetVisible(bool on)
        {
            if (model != null) model.SetActive(on);
            if (hurtCollider != null) hurtCollider.enabled = on && !shattered;
        }
    }
}
