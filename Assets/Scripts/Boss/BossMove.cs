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
    /// 攻擊判定打到東西時呼叫 ReportHit，BossController 會用 OnMoveHit 轉發（命中火花、Debug 面板）；SpawnHazard 放的 Hazard 已經自動接好。
    /// </summary>
    public abstract class BossMove : MonoBehaviour
    {
        [Tooltip("招式數值；招式特有的數值放在 BossMoveData 的子類別")]
        [SerializeField] BossMoveData data;

        public BossMoveData Data => data;
        public string DisplayName => data != null ? data.displayName : name;
        /// <summary>招式自身的候選資格（例如召喚未滿額）；預設維持原本選招。</summary>
        public virtual bool CanChoose => true;

        /// <summary>出這招的 Boss（BossController.Awake 時綁定）。</summary>
        protected BossController Boss { get; private set; }

        /// <summary>預兆時間（秒）：BossMoveData.telegraphTime 加上難度修正；需要依情況變動的招式可以覆寫。</summary>
        public virtual float TelegraphTime => data.telegraphTime + (Boss != null ? Boss.Difficulty.telegraphBonus : 0f);
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

        /// <summary>
        /// 讓 Boss 自己累積失衡值（例如龍捲風結束時 +40，數值放在招式的 BossMoveData）。
        /// 加滿會馬上進入頭暈、打斷目前這招（呼叫這招的 Interrupt），呼叫之後要先確認自己還是不是 Boss 的 CurrentMove。
        /// </summary>
        protected void AddImbalance(float amount)
        {
            if (Boss.Stagger != null) Boss.Stagger.AddImbalance(amount);
        }

        /// <summary>Boss 無敵（攻擊都當成閃過），例如大迴旋浮空期間；招式結束或被打斷時要關掉。</summary>
        protected void SetInvulnerable(bool on) => Boss.SetMoveInvulnerable(on);

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

        /// <summary>
        /// 招式中途預告下一下（例如三連拍第三下改成不可擋、紅光）：Boss 身上的預兆閃光換成這個顏色，閃 duration 秒後最亮再淡掉。
        /// 招式開頭的預兆由 BossController 依 BossMoveData.canBlock 自動送，不用呼叫。
        /// </summary>
        protected void Telegraph(bool canBlock, float duration) => Boss.NotifyTelegraph(this, canBlock, duration);

        /// <summary>送出一次衝擊時刻（拍下去、撞牆、落地…），打擊回饋的音效與鏡頭震動用。</summary>
        protected void Impact(BossImpactKind kind, Vector3 position) => Boss.NotifyImpact(kind, position);

        /// <summary>收掉這招放到場上、還沒跑完的 Hazard（被打斷時，或轉階段時由 BossController.ClearAllHazards 呼叫）。</summary>
        public void ClearHazards()
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
