using System.Collections.Generic;
using BossFight.Core;
using BossFight.Hazards;
using UnityEngine;

namespace BossFight.Boss
{
    /// <summary>
    /// 招式基底類別：每招一個子類別，掛在 Boss 的 Moves 子物件上，依 BossMoveData 執行。
    /// 狀態與時間由 BossController 管：預兆 TelegraphTime 秒 → 出招（TickActive 回傳 true 才結束）→ 破綻 RecoveryTime 秒 → End。
    /// 被頭暈、轉階段、倒下打斷時改呼叫 Interrupt，子類別要在 End／Interrupt 收掉 Hitbox；
    /// 用 SpawnHazard 放到場上的 Hazard 在 End 時照常跑完，Interrupt 時一起收掉。
    /// 攻擊判定打到東西時呼叫 ReportHit，BossController 會用 OnMoveHit 轉發（T5 的盾反加失衡值）；SpawnHazard 放的 Hazard 已經自動接好。
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

        /// <summary>地面高度（場地中心的 y），地面提示與落點都貼在這個高度。</summary>
        protected float GroundY => Boss.ArenaCenter.y;

        readonly List<Hazard> hazards = new List<Hazard>();

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

        /// <summary>破綻結束，招式正常收尾；已經放出去的 Hazard 照常跑完。</summary>
        public virtual void End() { }

        /// <summary>預兆、出招或破綻中被打斷（頭暈、轉階段、倒下）：和 End 一樣收尾，並收掉場上這招放出的 Hazard。</summary>
        public virtual void Interrupt()
        {
            End();
            ClearHazards();
        }

        /// <summary>依 Data 組出一下攻擊（失衡值是給玩家的，Boss 的攻擊不帶）。</summary>
        protected DamageInfo CreateDamage() => new DamageInfo(data.damage, data.canBlock, data.canParry, 0f);

        /// <summary>攻擊判定打到目標並結算完，交給 BossController 轉發。</summary>
        protected void ReportHit(HitReport report) => Boss.NotifyMoveHit(this, report);

        /// <summary>生成一個 Hazard：攻擊方設為 Boss，命中轉成 ReportHit，被打斷時會一起收掉。</summary>
        protected T SpawnHazard<T>(T prefab, Vector3 position, Quaternion rotation) where T : Hazard
        {
            var hazard = Instantiate(prefab, position, rotation);
            hazard.SetAttacker(Boss.transform);
            hazard.OnHit += ReportHit;
            hazard.OnFinished += ForgetHazard;
            hazards.Add(hazard);
            return hazard;
        }

        /// <summary>收掉這招放到場上、還沒跑完的 Hazard。</summary>
        protected void ClearHazards()
        {
            for (int i = hazards.Count - 1; i >= 0; i--)
            {
                if (hazards[i] != null) hazards[i].Despawn();
            }
            hazards.Clear();
        }

        void ForgetHazard(Hazard hazard) => hazards.Remove(hazard);

        /// <summary>只留水平分量。</summary>
        protected static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        /// <summary>把位置貼到地面高度。</summary>
        protected Vector3 OnGround(Vector3 v) => new Vector3(v.x, GroundY, v.z);
    }
}
