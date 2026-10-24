using System;
using BossFight.Core;
using UnityEngine;

namespace BossFight.Player
{
    /// <summary>
    /// 執行一刀：照 AttackSpec 在判定時間開關刀的 Hitbox，照踏步時間往前移動。
    /// 自己沒有 Update，由 PlayerController 呼叫 Begin（處決用 BeginExecution）→ 每幀 Tick → 結束或被取消時 Stop。
    /// 處決這一刀的 DamageInfo 標 isExecution（固定傷害，打到就結束目標的頭暈）。
    /// 揮出去用 OnSwing、命中結果用 OnAttackHit 轉出去（Debug 顯示、PlayerFeedback 的音效與震動）。
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerAttack : MonoBehaviour
    {
        [Tooltip("攻擊判定的高度")]
        [SerializeField] PlayerStats stats;
        [Tooltip("刀的攻擊判定，放在角色根物件底下；形狀在每次出手時依 AttackSpec 設定")]
        [SerializeField] Hitbox swordHitbox;

        /// <summary>出手：(連擊第幾段，從 0 起；是否為重擊)。處決也會送（0, false），看 IsExecution 分辨。</summary>
        public event Action<int, bool> OnAttackStarted;
        /// <summary>刀的判定打開（揮出去的那一刻，打不打得到都會送；揮刀音效用）。</summary>
        public event Action OnSwing;
        public event Action<HitReport> OnAttackHit;

        public bool IsAttacking { get; private set; }
        public bool IsHeavy { get; private set; }
        /// <summary>這一刀是處決。</summary>
        public bool IsExecution { get; private set; }
        /// <summary>連擊第幾段（從 0 起）；重擊、處決為 0。</summary>
        public int ComboIndex { get; private set; }
        /// <summary>這一刀是輕攻擊連擊的最後一段（第三段）。</summary>
        public bool IsLastComboStep => IsAttacking && !IsHeavy && !IsExecution && ComboIndex >= stats.lightCombo.Length - 1;
        public AttackSpec Current => spec;
        public float Elapsed { get; private set; }
        /// <summary>動作進度 0..1。</summary>
        public float Progress => IsAttacking ? Mathf.Clamp01(Elapsed / spec.duration) : 0f;
        public bool CanCancel => IsAttacking && Elapsed >= spec.cancelTime;
        public bool IsFinished => Elapsed >= spec.duration;

        AttackSpec spec;
        bool windowOpened;

        /// <summary>換成另一份數值（PlayerRig.ApplyStats 呼叫）。</summary>
        public void SetStats(PlayerStats value)
        {
            if (value != null) stats = value;
        }
        float lunged;

        void OnEnable()
        {
            if (swordHitbox != null) swordHitbox.OnHit += HandleHit;
        }

        void OnDisable()
        {
            if (swordHitbox != null) swordHitbox.OnHit -= HandleHit;
            Stop();
        }

        public void Begin(in AttackSpec attack, int comboIndex, bool heavy) => Begin(attack, comboIndex, heavy, false);

        /// <summary>處決這一刀：DamageInfo 標 isExecution。</summary>
        public void BeginExecution(in AttackSpec attack) => Begin(attack, 0, false, true);

        void Begin(in AttackSpec attack, int comboIndex, bool heavy, bool execution)
        {
            spec = attack;
            ComboIndex = comboIndex;
            IsHeavy = heavy;
            IsExecution = execution;
            Elapsed = 0f;
            windowOpened = false;
            lunged = 0f;
            IsAttacking = true;

            // 角色前方的長方體：從腳底到 attackHitboxHeight、從中心往前 reach
            float h = stats.attackHitboxHeight;
            swordHitbox.Deactivate();
            swordHitbox.SetBox(new Vector3(0f, h * 0.5f, spec.reach * 0.5f), new Vector3(spec.width, h, spec.reach));
            OnAttackStarted?.Invoke(comboIndex, heavy);
        }

        /// <returns>這一幀踏步的位移（世界座標）。</returns>
        public Vector3 Tick(float dt)
        {
            if (!IsAttacking) return Vector3.zero;
            float prev = Elapsed;
            Elapsed += dt;

            // 判定至少開一幀：就算一幀跨過整段判定時間，也會掃到一次
            if (!windowOpened && Elapsed >= spec.hitStart)
            {
                windowOpened = true;
                swordHitbox.Activate(new DamageInfo(spec.damage, false, false, spec.imbalance, isExecution: IsExecution));
                OnSwing?.Invoke();
            }
            else if (swordHitbox.IsActive && prev >= spec.hitEnd)
            {
                swordHitbox.Deactivate();
            }

            return LungeStep();
        }

        public void Stop()
        {
            IsAttacking = false;
            if (swordHitbox != null) swordHitbox.Deactivate();
        }

        // 依踏步時間算出這一幀該走的距離，總和剛好是 lungeDistance（沒撞到東西的話）
        Vector3 LungeStep()
        {
            if (spec.lungeDistance <= 0f || spec.lungeEnd <= spec.lungeStart) return Vector3.zero;
            float t = Mathf.Clamp01((Elapsed - spec.lungeStart) / (spec.lungeEnd - spec.lungeStart));
            float d = Mathf.SmoothStep(0f, spec.lungeDistance, t);
            Vector3 forward = transform.forward;
            forward.y = 0f;
            Vector3 delta = forward.normalized * (d - lunged);
            lunged = d;
            return delta;
        }

        void HandleHit(HitReport report)
        {
            OnAttackHit?.Invoke(report);
        }
    }
}
