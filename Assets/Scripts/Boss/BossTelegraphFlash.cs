using UnityEngine;

namespace BossFight.Boss
{
    /// <summary>
    /// Boss 身上的發光：預兆閃光（企劃書的預兆顏色規則：可擋的招閃白光，不可擋的閃紅光）與受擊閃光。
    /// 預兆只訂閱 BossController 的事件：OnMoveTelegraph 決定顏色與時間，一明一暗地越閃越亮，出手那一刻（預兆時間到、或進入出招）最亮再淡掉；
    /// 招式中途的預兆（BossMove.Telegraph，例如三連拍第三下紅光）也是同一套。破綻、頭暈等其他狀態熄滅。
    /// 受擊閃光由 BossDamageFeedback 呼叫 FlashHit，疊加在預兆的亮度上（兩者都改 _EmissionColor，所以由這裡統一寫入）。
    /// 材質要開 Emission；顏色、亮度、閃爍頻率都是純視覺參數。
    /// </summary>
    [DisallowMultipleComponent]
    public class BossTelegraphFlash : MonoBehaviour
    {
        [SerializeField] BossController boss;
        [Tooltip("發光的 Renderer（材質要開 Emission）")]
        [SerializeField] Renderer[] renderers;
        [SerializeField] Color blockableColor = Color.white;
        [SerializeField] Color unblockableColor = new Color(1f, 0.15f, 0.1f);
        [Tooltip("出招那一刻的亮度")]
        [Min(0f)] [SerializeField] float peakIntensity = 2.5f;
        [Tooltip("預兆期間每秒閃幾下")]
        [Min(0f)] [SerializeField] float blinkRate = 4f;
        [Tooltip("出招後亮光淡掉的時間（秒）")]
        [Min(0.01f)] [SerializeField] float fadeTime = 0.3f;

        static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        MaterialPropertyBlock block;
        enum Glow { Off, Blink, Fade }

        Glow glow;
        Color color, hitColor, written;
        float telegraphTime, timer, hitTime, hitTimer;

        void Awake()
        {
            block = new MaterialPropertyBlock();
            SetEmission(Color.black);
        }

        /// <summary>受擊閃一下：color（HDR）在 duration 秒內淡掉，疊在預兆的亮度上。</summary>
        public void FlashHit(Color flashColor, float duration)
        {
            hitColor = flashColor;
            hitTime = hitTimer = Mathf.Max(1e-3f, duration);
        }

        void OnEnable()
        {
            boss.OnMoveTelegraph += HandleMoveTelegraph;
            boss.OnStateChanged += HandleStateChanged;
        }

        void OnDisable()
        {
            boss.OnMoveTelegraph -= HandleMoveTelegraph;
            boss.OnStateChanged -= HandleStateChanged;
        }

        void HandleMoveTelegraph(MoveTelegraph telegraph)
        {
            color = telegraph.canBlock ? blockableColor : unblockableColor;
            telegraphTime = telegraph.duration;
            SetGlow(Glow.Blink);
        }

        void HandleStateChanged(BossState next)
        {
            // 進入預兆時 OnMoveTelegraph 緊接著送；進入出招時還在閃就直接最亮；其他狀態熄滅
            if (next == BossState.Active) { if (glow == Glow.Blink) SetGlow(Glow.Fade); }
            else if (next != BossState.Telegraph) SetGlow(Glow.Off);
        }

        void SetGlow(Glow next)
        {
            glow = next;
            timer = 0f;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            Color emission = Color.black;
            if (glow == Glow.Blink && timer >= telegraphTime) SetGlow(Glow.Fade); // 招式中途的預兆：時間到就是出手
            if (glow == Glow.Fade && timer >= fadeTime) SetGlow(Glow.Off);
            if (glow != Glow.Off)
            {
                timer += dt;
                float intensity;
                if (glow == Glow.Blink)
                {
                    float k = telegraphTime > 0f ? Mathf.Clamp01(timer / telegraphTime) : 1f;
                    float blink = 0.5f + 0.5f * Mathf.Cos(timer * blinkRate * Mathf.PI * 2f);
                    intensity = peakIntensity * k * Mathf.Lerp(blink, 1f, k * k); // 越接近出招越亮、越不閃
                }
                else
                {
                    intensity = peakIntensity * (1f - Mathf.Clamp01(timer / fadeTime));
                }
                emission = color * intensity;
            }
            if (hitTimer > 0f)
            {
                hitTimer = Mathf.Max(0f, hitTimer - dt);
                float h = hitTimer / hitTime;
                emission += hitColor * (h * h); // 一開始最亮，很快淡掉
            }
            if (emission != written) SetEmission(emission);
        }

        void SetEmission(Color emission)
        {
            written = emission;
            if (renderers == null) return;
            foreach (var r in renderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(block);
                block.SetColor(EmissionColorId, emission);
                r.SetPropertyBlock(block);
            }
        }
    }
}
