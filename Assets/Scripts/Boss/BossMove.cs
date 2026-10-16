using BossFight.Core;
using UnityEngine;

namespace BossFight.Boss
{
    /// <summary>
    /// 招式基底類別：每招一個子類別，掛在 Boss 的 Moves 子物件上，依 BossMoveData 執行。
    /// 狀態與時間由 BossController 管：預兆 TelegraphTime 秒 → 出招（TickActive 回傳 true 才結束）→ 破綻 RecoveryTime 秒 → End。
    /// 被頭暈、轉階段、倒下打斷時改呼叫 Interrupt，子類別要在 End／Interrupt 收掉 Hitbox 與場上的 Hazard。
    /// 攻擊判定打到東西時呼叫 ReportHit，BossController 會用 OnMoveHit 轉發（T5 的盾反加失衡值）。
    /// </summary>
    public abstract class BossMove : MonoBehaviour
    {
        [Tooltip("招式數值；招式特有的數值放在 BossMoveData 的子類別")]
        [SerializeField] BossMoveData data;

        public BossMoveData Data => data;
        public string DisplayName => data != null ? data.displayName : name;

        /// <summary>出這招的 Boss（BossController.Awake 時綁定）。</summary>
        protected BossController Boss { get; private set; }

        /// <summary>預兆時間（秒）；需要依情況變動的招式可以覆寫。</summary>
        public virtual float TelegraphTime => data.telegraphTime;
        /// <summary>破綻時間（秒）；例如衝撞撞牆與否可以覆寫成不同的值。</summary>
        public virtual float RecoveryTime => data.recoveryTime;

        internal void Bind(BossController boss) => Boss = boss;

        /// <summary>進入預兆。</summary>
        public virtual void BeginTelegraph() { }

        /// <summary>預兆中每幀呼叫；預設轉向玩家。</summary>
        public virtual void TickTelegraph(float dt) => Boss.FaceTarget(dt);

        /// <summary>預兆結束、開始出招。</summary>
        public virtual void BeginActive() { }

        /// <summary>出招中每幀呼叫。</summary>
        /// <returns>出招結束、進入破綻。</returns>
        public abstract bool TickActive(float dt);

        /// <summary>進入破綻。</summary>
        public virtual void BeginRecovery() { }

        /// <summary>破綻中每幀呼叫。</summary>
        public virtual void TickRecovery(float dt) { }

        /// <summary>破綻結束，招式正常收尾。</summary>
        public virtual void End() { }

        /// <summary>預兆、出招或破綻中被打斷（頭暈、轉階段、倒下）；預設和 End 一樣收尾。</summary>
        public virtual void Interrupt() => End();

        /// <summary>依 Data 組出一下攻擊（失衡值是給玩家的，Boss 的攻擊不帶）。</summary>
        protected DamageInfo CreateDamage() => new DamageInfo(data.damage, data.canBlock, data.canParry, 0f);

        /// <summary>攻擊判定打到目標並結算完，交給 BossController 轉發。</summary>
        protected void ReportHit(HitReport report) => Boss.NotifyMoveHit(this, report);
    }
}
