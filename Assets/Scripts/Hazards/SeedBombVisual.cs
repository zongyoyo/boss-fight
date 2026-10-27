using UnityEngine;
namespace BossFight.Hazards
{
    /// <summary>附屬於受管理的 AreaStrike；取消、死亡或清場時隨同刪除，不參與傷害。</summary>
    public class SeedBombVisual : MonoBehaviour
    {
        Vector3 start, end;
        float duration, height, elapsed;
        public void Launch(Vector3 from, Vector3 to, float time, float lift)
        { start = from; end = to; duration = time; height = lift; transform.position = from; }
        void Update()
        {
            elapsed += Time.deltaTime;
            float t = duration > 0f ? Mathf.Clamp01(elapsed / duration) : 1f;
            transform.position = Vector3.Lerp(start, end, t) + Vector3.up * (4f * height * t * (1f - t));
            transform.Rotate(Vector3.up, Time.deltaTime * 180f);
        }
    }
}
