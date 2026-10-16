using System;
using BossFight.Core;
using UnityEngine;

namespace BossFight.Hazards
{
    /// <summary>
    /// 場上判定物件的基底（衝擊波、落雷…）：出招方生成後交給它自己跑，判定走同一個物件上的 Hitbox。
    /// Hitbox 的命中用 OnHit 轉出去（出招的 BossMove 轉成 ReportHit）；跑完或被收掉時送 OnFinished 並刪掉自己。
    /// 數值由出招方從 ScriptableObject 讀出後傳進來，這裡只放純視覺的參數。
    /// </summary>
    public abstract class Hazard : MonoBehaviour
    {
        [Tooltip("攻擊判定（Layer 掃 Hurtbox）")]
        [SerializeField] protected Hitbox hitbox;

        /// <summary>判定打到目標並結算完。</summary>
        public event Action<HitReport> OnHit;
        /// <summary>跑完或被收掉，接著會刪掉自己。</summary>
        public event Action<Hazard> OnFinished;

        bool finished;

        /// <summary>攻擊方的根物件：它底下的 Hurtbox 不會被打到，格擋判定也以它的位置為攻擊來源。</summary>
        public void SetAttacker(Transform attacker) => hitbox.SetAttacker(attacker);

        protected virtual void OnEnable() => hitbox.OnHit += HandleHit;

        protected virtual void OnDisable() => hitbox.OnHit -= HandleHit;

        void HandleHit(HitReport report) => OnHit?.Invoke(report);

        /// <summary>立刻收掉（招式被打斷、轉階段清場）。</summary>
        public void Despawn()
        {
            hitbox.Deactivate();
            Finish();
        }

        /// <summary>結束：送出 OnFinished 並刪掉自己；重複呼叫無效。</summary>
        protected void Finish()
        {
            if (finished) return;
            finished = true;
            OnFinished?.Invoke(this);
            Destroy(gameObject);
        }
    }
}
