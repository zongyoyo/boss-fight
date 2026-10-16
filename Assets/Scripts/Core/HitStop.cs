using UnityEngine;

namespace BossFight.Core
{
    /// <summary>
    /// 畫面停頓：把 Time.timeScale 設為 0，經過指定的真實時間後還原成停頓前的值；重疊時以較晚的結束時間為準。
    /// 停頓長度由呼叫方從 ScriptableObject 讀（例如盾反的 PlayerStats.parryHitStop）。
    /// </summary>
    [DisallowMultipleComponent]
    public class HitStop : MonoBehaviour
    {
        public bool IsStopped { get; private set; }

        float restoreScale = 1f;
        float endTime;

        public void Play(float duration)
        {
            if (duration <= 0f) return;

            float end = Time.unscaledTime + duration;
            if (IsStopped)
            {
                endTime = Mathf.Max(endTime, end);
                return;
            }
            restoreScale = Time.timeScale;
            endTime = end;
            IsStopped = true;
            Time.timeScale = 0f;
        }

        void Update()
        {
            if (IsStopped && Time.unscaledTime >= endTime) Restore();
        }

        void OnDisable()
        {
            if (IsStopped) Restore();
        }

        void Restore()
        {
            Time.timeScale = restoreScale;
            IsStopped = false;
        }
    }
}
