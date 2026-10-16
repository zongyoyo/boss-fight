using System;
using UnityEngine;

namespace BossFight.Player
{
    /// <summary>
    /// 刀盾狗的耐力。上限與回復速度讀 PlayerStats；目前值存在元件自己身上。
    /// 回復由 PlayerController 每幀呼叫 Regenerate（舉盾時回得比較慢）；格擋扣耐力由 PlayerHealth 呼叫 Drain。UI 訂閱 OnStaminaChanged。
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerStamina : MonoBehaviour
    {
        [SerializeField] PlayerStats stats;

        /// <summary>(目前值, 上限)。</summary>
        public event Action<float, float> OnStaminaChanged;

        public float Current { get; private set; }
        public float Max => stats.maxStamina;

        void Awake()
        {
            Current = stats.maxStamina;
        }

        void Start()
        {
            // 讓先訂閱的 UI 拿到初始值
            OnStaminaChanged?.Invoke(Current, Max);
        }

        /// <summary>耐力大於 0 就成功，扣完最低為 0；耐力已經是 0 則失敗、不扣。</summary>
        public bool TrySpend(float amount)
        {
            if (Current <= 0f) return false;
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

        public void Regenerate(float deltaTime, bool blocking)
        {
            float rate = blocking ? stats.staminaRegenBlocking : stats.staminaRegen;
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
