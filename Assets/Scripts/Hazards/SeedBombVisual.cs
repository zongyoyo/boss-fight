using UnityEngine;
namespace BossFight.Hazards
{
    /// <summary>
    /// 附屬於受管理的 AreaStrike；取消、死亡或清場時隨同刪除，不參與傷害。
    /// 飛行時自轉；落地後停住（序章修正 C8），在引信時間內越脹越大、脈動越來越快，爆炸由 AreaStrike 開判定時放特效。
    /// 這裡的數字全是視覺參數。
    /// </summary>
    public class SeedBombVisual : MonoBehaviour
    {
        [Tooltip("飛行時的自轉速度（度/秒）")]
        [SerializeField] float spinSpeed = 180f;
        [Tooltip("爆炸前脹大到原本的幾倍")]
        [Min(1f)] [SerializeField] float swellScale = 1.6f;
        [Tooltip("落地時與爆炸前的脈動頻率（次/秒）")]
        [SerializeField] Vector2 pulseRate = new Vector2(2f, 9f);
        [Tooltip("脈動的幅度（大小倍率）")]
        [Range(0f, 1f)] [SerializeField] float pulseAmount = 0.12f;

        Vector3 start, end, baseScale;
        float duration, height, fuse, elapsed, pulsePhase;

        /// <param name="fuseTime">落地到爆炸的秒數。</param>
        public void Launch(Vector3 from, Vector3 to, float time, float lift, float fuseTime)
        {
            start = from; end = to; duration = time; height = lift; fuse = fuseTime;
            transform.position = from;
            baseScale = transform.localScale;
        }

        void Update()
        {
            elapsed += Time.deltaTime;
            float t = duration > 0f ? Mathf.Clamp01(elapsed / duration) : 1f;
            transform.position = Vector3.Lerp(start, end, t) + Vector3.up * (4f * height * t * (1f - t));
            if (t < 1f)
            {
                transform.Rotate(Vector3.up, Time.deltaTime * spinSpeed);
                return;
            }
            float u = fuse > 0f ? Mathf.Clamp01((elapsed - duration) / fuse) : 1f;
            pulsePhase += Time.deltaTime * Mathf.Lerp(pulseRate.x, pulseRate.y, u) * Mathf.PI * 2f;
            float scale = Mathf.Lerp(1f, swellScale, u * u) * (1f + Mathf.Sin(pulsePhase) * pulseAmount);
            transform.localScale = baseScale * scale;
        }
    }
}
