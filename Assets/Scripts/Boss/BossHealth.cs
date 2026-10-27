using System;
using BossFight.Core;
using UnityEngine;

namespace BossFight.Boss
{
    /// <summary>Boss 結算一次攻擊後的紀錄，給 BossController、BossStagger（失衡值）、UI 與音效。</summary>
    public readonly struct BossDamageReport
    {
        public readonly DamageInfo info;
        public readonly DamageResult result;
        /// <summary>實際扣掉的血量（已乘上受傷倍率）。</summary>
        public readonly float healthLost;
        /// <summary>這一下套用的受傷倍率（頭暈中 &gt; 1）；處決、無敵時為 1。</summary>
        public readonly float multiplier;

        public BossDamageReport(in DamageInfo info, DamageResult result, float healthLost, float multiplier = 1f)
        {
            this.info = info;
            this.result = result;
            this.healthLost = healthLost;
            this.multiplier = multiplier;
        }
    }

    /// <summary>
    /// OIIA 貓的血量與受傷結算。身上的 Hurtbox 把攻擊轉到 TakeDamage；Boss 不會格擋，無敵（轉階段）時回傳 Evaded，其他都是直接命中。
    /// 扣血 = 傷害 × DamageMultiplier（頭暈中由 BossStagger 設定）；處決（info.isExecution）是固定傷害，不乘倍率。
    /// 血量上限讀 BossStats，目前血量存在元件自己身上。失衡值跟著 OnDamaged 的 info.imbalance 送出，累積由 BossStagger 處理。
    /// 事件順序：OnBossHealthChanged → OnDamaged → OnDefeated。
    /// </summary>
    [DisallowMultipleComponent]
    public class BossHealth : MonoBehaviour, IDamageable
    {
        [SerializeField] BossStats stats;

        /// <summary>(目前血量, 上限)。開打時也會送一次初始值。</summary>
        public event Action<float, float> OnBossHealthChanged;
        public event Action<BossDamageReport> OnDamaged;
        /// <summary>血量歸零。</summary>
        public event Action OnDefeated;

        public float Current { get; private set; }
        public float Max => stats.maxHealth;
        public bool IsDefeated => Current <= 0f;
        /// <summary>無敵（轉階段中由 BossController 設定）：攻擊都當成閃過。</summary>
        public bool Invulnerable { get; set; }
        /// <summary>外部流程血量下限；扣血前夾住，超額傷害不計。預設不鎖血。</summary>
        public float HealthFloor { get; set; }
        /// <summary>受到傷害的倍率（頭暈中由 BossStagger 設定，平常為 1）；處決不吃倍率。</summary>
        public float DamageMultiplier { get; set; } = 1f;

        void Awake()
        {
            Current = stats.maxHealth;
        }

        void Start()
        {
            // 讓先訂閱的 UI 拿到初始值
            OnBossHealthChanged?.Invoke(Current, Max);
        }

        public DamageOutcome TakeDamage(in DamageInfo info)
        {
            if (IsDefeated) return DamageResult.Ignored;
            if (Invulnerable)
            {
                OnDamaged?.Invoke(new BossDamageReport(info, DamageResult.Evaded, 0f));
                return DamageResult.Evaded;
            }

            float multiplier = info.isExecution ? 1f : DamageMultiplier;
            float lost = -SetHealth(Current - info.damage * multiplier);
            OnDamaged?.Invoke(new BossDamageReport(info, DamageResult.Hit, lost, multiplier));
            if (IsDefeated) OnDefeated?.Invoke();
            return DamageResult.Hit;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD || BOSSFIGHT_DEV_TOOLS
        /// <summary>Debug 用：直接設定血量（F1）。不會觸發 OnDamaged；設到 0 會倒下。</summary>
        public void DebugSetHealth(float value)
        {
            if (IsDefeated) return;
            SetHealth(value);
            if (IsDefeated) OnDefeated?.Invoke();
        }
#endif

        /// <returns>血量的變化量（扣血為負）。</returns>
        float SetHealth(float value)
        {
            value = Mathf.Clamp(value, Mathf.Clamp(HealthFloor, 0f, Max), Max);
            float delta = value - Current;
            if (delta == 0f) return 0f;
            Current = value;
            OnBossHealthChanged?.Invoke(Current, Max);
            return delta;
        }
    }
}
