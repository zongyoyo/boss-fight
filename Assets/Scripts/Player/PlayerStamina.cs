using System;
using UnityEngine;

namespace BossFight.Player
{
    /// <summary>
    /// 刀盾狗的耐力。上限與回復速度讀 PlayerStats；目前值存在元件自己身上。
    /// 回復由 PlayerController 每幀呼叫 Regenerate：平常 staminaRegen；舉盾滿 blockRegenDelay 秒後加速成 staminaRegenBlocking（OnRegenBoostChanged 通知 UI）。
    /// 格擋扣耐力由 PlayerHealth 呼叫 Drain。UI 訂閱 OnStaminaChanged。
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerStamina : MonoBehaviour
    {
        [SerializeField] PlayerStats stats;

        /// <summary>(目前值, 上限)。</summary>
        public event Action<float, float> OnStaminaChanged;
        /// <summary>舉盾加速回復開始（true）或結束（false）。</summary>
        public event Action<bool> OnRegenBoostChanged;

        public float Current { get; private set; }
        public float Max => stats.maxStamina;
        /// <summary>正在舉盾加速回復。</summary>
        public bool IsRegenBoosted { get; private set; }

        void Awake()
        {
            Current = stats.maxStamina;
        }

        void Start()
        {
            // 讓先訂閱的 UI 拿到初始值
            OnStaminaChanged?.Invoke(Current, Max);
        }

        /// <summary>換成另一份數值（PlayerRig.ApplyStats 呼叫）：refill 時補滿，否則只把目前值夾到新上限以內。</summary>
        public void SetStats(PlayerStats value, bool refill)
        {
            if (value == null) return;
            stats = value;
            Current = refill ? Max : Mathf.Min(Current, Max);
            OnStaminaChanged?.Invoke(Current, Max);
        }

        /// <summary>耐力大於 0 就成功，扣完最低為 0；耐力已經是 0 則失敗、不扣。</summary>
        public bool TrySpend(float amount)
        {
            if (Current <= 0f) return false;
            Set(Current - amount);
            return true;
        }

        /// <summary>耐力至少有 amount 才成功並扣掉；不夠則失敗、不扣（翻滾用）。</summary>
        public bool TrySpendFull(float amount)
        {
            if (Current < amount) return false;
            Set(Current - amount);
            return true;
        }

        /// <summary>不管剩多少都扣（格擋用），最低扣到 0；回傳實際扣掉的量。</summary>
        public float Drain(float amount)
        {
            float before = Current;
            Set(Current - amount);
            return before - Current;
        }

        /// <param name="blockHeldTime">這次舉盾已經舉了多久（秒）；沒舉盾時傳 blocking = false。</param>
        public void Regenerate(float deltaTime, bool blocking, float blockHeldTime)
        {
            bool boosted = blocking && blockHeldTime >= stats.blockRegenDelay;
            if (boosted != IsRegenBoosted)
            {
                IsRegenBoosted = boosted;
                OnRegenBoostChanged?.Invoke(boosted);
            }
            float rate = boosted ? stats.staminaRegenBlocking : stats.staminaRegen;
            Set(Current + rate * deltaTime);
        }

        void Set(float value)
        {
            value = Mathf.Clamp(value, 0f, Max);
            if (value == Current) return;
            Current = value;
            OnStaminaChanged?.Invoke(Current, Max);
        }
    }
}
