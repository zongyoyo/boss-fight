namespace BossFight.Core
{
    /// <summary>
    /// 會受傷的對象（玩家、Boss、木樁），由身上的 Hurtbox 轉呼叫。
    /// 無敵、格擋、盾反都由實作方自己結算，回傳結果讓攻擊方知道發生了什麼。
    /// </summary>
    public interface IDamageable
    {
        DamageResult TakeDamage(in DamageInfo info);
    }
}
