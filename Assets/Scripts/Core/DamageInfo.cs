using UnityEngine;

namespace BossFight.Core
{
    /// <summary>
    /// 一次攻擊的內容：攻擊方從 ScriptableObject 讀出數值填好，交給被打方的 IDamageable 結算。
    /// 格擋與盾反要看攻擊從哪個方向來，所以帶著來源位置；經過 Hitbox 時由 Hitbox 填入攻擊方的位置。
    /// 失衡值只是跟著攻擊送過去，累積與頭暈由 Boss 端處理（T5）。
    /// </summary>
    public readonly struct DamageInfo
    {
        /// <summary>原始傷害（減傷前）。</summary>
        public readonly float damage;
        /// <summary>可以用盾擋下（預兆白光）。</summary>
        public readonly bool canBlock;
        /// <summary>可以盾反。</summary>
        public readonly bool canParry;
        /// <summary>命中時給目標累積的失衡值。</summary>
        public readonly float imbalance;
        /// <summary>攻擊來源的世界座標，判斷是不是從正面打來。</summary>
        public readonly Vector3 sourcePosition;
        /// <summary>出手的物件，可為 null。</summary>
        public readonly GameObject source;

        public DamageInfo(float damage, bool canBlock, bool canParry, float imbalance,
                          Vector3 sourcePosition = default, GameObject source = null)
        {
            this.damage = damage;
            this.canBlock = canBlock;
            this.canParry = canParry;
            this.imbalance = imbalance;
            this.sourcePosition = sourcePosition;
            this.source = source;
        }

        /// <summary>換掉來源位置與物件，其他不變。</summary>
        public DamageInfo WithSource(Vector3 position, GameObject sourceObject) =>
            new DamageInfo(damage, canBlock, canParry, imbalance, position, sourceObject);
    }

    /// <summary>被打方結算一次攻擊的結果，會回報給攻擊方（例如 Boss 被盾反時加失衡）。</summary>
    public enum DamageResult
    {
        /// <summary>沒有作用（目標已經倒下等）。</summary>
        Ignored,
        /// <summary>無敵中（翻滾前段、受擊後），沒有受傷。</summary>
        Evaded,
        /// <summary>直接命中。</summary>
        Hit,
        /// <summary>被盾擋下，只吃到部分傷害。</summary>
        Blocked,
        /// <summary>擋下了，但耐力歸零而破防。</summary>
        GuardBroken,
        /// <summary>被盾反，完全免傷。</summary>
        Parried,
    }

    /// <summary>Hitbox 打到一個目標後的回報。</summary>
    public readonly struct HitReport
    {
        public readonly Hurtbox hurtbox;
        public readonly IDamageable target;
        /// <summary>實際送出的攻擊（已填入來源位置）。</summary>
        public readonly DamageInfo info;
        public readonly DamageResult result;

        public HitReport(Hurtbox hurtbox, IDamageable target, in DamageInfo info, DamageResult result)
        {
            this.hurtbox = hurtbox;
            this.target = target;
            this.info = info;
            this.result = result;
        }
    }
}
