using UnityEngine;

namespace BossFight.Boss
{
    /// <summary>
    /// 預兆閃光（企劃書的預兆顏色規則）：可擋的招閃白光，不可擋的閃紅光。
    /// 只訂閱 BossController 的事件：選到招時記下顏色，預兆期間一明一暗地越閃越亮，出招那一刻最亮再淡掉，其他狀態熄滅。
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
        BossState state;
        Color color;
        float telegraphTime, timer;

        void Awake()
        {
            block = new MaterialPropertyBlock();
            SetEmission(Color.black);
        }

        void OnEnable()
        {
            boss.OnMoveChosen += HandleMoveChosen;
            boss.OnStateChanged += HandleStateChanged;
        }

        void OnDisable()
        {
            boss.OnMoveChosen -= HandleMoveChosen;
            boss.OnStateChanged -= HandleStateChanged;
        }

        void HandleMoveChosen(BossMoveChoice choice)
        {
            color = choice.move.Data.canBlock ? blockableColor : unblockableColor;
            telegraphTime = choice.move.TelegraphTime;
        }

        void HandleStateChanged(BossState next)
        {
            state = next;
            timer = 0f;
            if (next != BossState.Telegraph && next != BossState.Active) SetEmission(Color.black);
        }

        void Update()
        {
            if (state != BossState.Telegraph && state != BossState.Active) return;
            timer += Time.deltaTime;

            float intensity;
            if (state == BossState.Telegraph)
            {
                float k = telegraphTime > 0f ? Mathf.Clamp01(timer / telegraphTime) : 1f;
                float blink = 0.5f + 0.5f * Mathf.Cos(timer * blinkRate * Mathf.PI * 2f);
                intensity = peakIntensity * k * Mathf.Lerp(blink, 1f, k * k); // 越接近出招越亮、越不閃
            }
            else
            {
                intensity = peakIntensity * (1f - Mathf.Clamp01(timer / fadeTime));
            }
            SetEmission(color * intensity);
        }

        void SetEmission(Color emission)
        {
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
