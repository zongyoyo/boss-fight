using UnityEngine;

namespace BossFight.Core
{
    /// <summary>
    /// 受擊範圍：掛在 Hurtbox Layer 的 trigger collider 上，Hitbox 掃到它就把攻擊轉給 owner（實作 IDamageable 的元件）。
    /// 一個 owner 可以有好幾個 Hurtbox，同一次攻擊只會結算一次。
    /// </summary>
    [RequireComponent(typeof(Collider))]
    [DisallowMultipleComponent]
    public class Hurtbox : MonoBehaviour
    {
        [Tooltip("受傷的對象，必須實作 IDamageable（玩家的 PlayerHealth、木樁…）")]
        [SerializeField] MonoBehaviour owner;

        public IDamageable Owner { get; private set; }

        void Awake()
        {
            Owner = owner as IDamageable;
            if (Owner == null) Debug.LogError($"[Damage] {name} 的 Hurtbox 沒有指定實作 IDamageable 的 owner。", this);
        }

        // 編輯器加元件時，自動接上父物件中第一個 IDamageable
        void Reset()
        {
            foreach (var mb in GetComponentsInParent<MonoBehaviour>(true))
            {
                if (mb is IDamageable)
                {
                    owner = mb;
                    break;
                }
            }
        }

        void OnValidate()
        {
            if (owner != null && !(owner is IDamageable))
            {
                Debug.LogWarning($"[Damage] {owner.GetType().Name} 沒有實作 IDamageable，不能當 Hurtbox 的 owner。", this);
                owner = null;
            }
        }
    }
}
