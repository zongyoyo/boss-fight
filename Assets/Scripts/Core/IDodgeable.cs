namespace BossFight.Core
{
    /// <summary>
    /// 會翻滾閃避的受擊對象（玩家的 PlayerHealth），和 IDamageable 實作在同一個元件上，攻擊方從 Hurtbox.Dodger 拿。
    /// 成功閃避只在被打方這一側結算一次：翻滾無敵中被打到（TakeDamage 回 Evaded）由實作方自己算；
    /// 攻擊方只負責回報「判定窗內目標在範圍裡、正在翻滾，判定窗結束時沒有打中」（NotifyNearMiss），算不算、每次翻滾只算一次也由實作方決定。
    /// 攻擊方不引用 Player，也不自己判斷無敵種類。
    /// </summary>
    public interface IDodgeable
    {
        /// <summary>正在翻滾，而且這次翻滾還能算成功閃避（不是強制無敵、受擊後無敵或已倒下）。攻擊方用它判斷目標是不是在範圍裡翻滾。</summary>
        bool IsDodging { get; }

        /// <summary>攻擊方回報：目標翻滾時在攻擊範圍裡，判定窗結束時沒有被打中。</summary>
        void NotifyNearMiss(in DamageInfo info);
    }
}
