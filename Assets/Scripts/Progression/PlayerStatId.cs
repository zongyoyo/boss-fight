namespace BossFight.Progression
{
    /// <summary>
    /// StatModifier 能改的玩家數值（企劃書「數值套用管線」的第一批）。每一項對應 PlayerStats 的欄位，對應寫在 PlayerLoadout.Modify 的 switch；
    /// 新增時在這裡加一項、Modify 與 Read 的 switch 各加一行。資產裡存的是數字，所以只能往後加，不要插在中間或改順序。
    /// </summary>
    public enum PlayerStatId
    {
        /// <summary>最大血量（maxHealth，最少 1）。</summary>
        MaxHealth,
        /// <summary>最大耐力（maxStamina，最少 1）。</summary>
        MaxStamina,
        /// <summary>耐力回復（staminaRegen；舉盾加速回復 staminaRegenBlocking 不受影響）。</summary>
        StaminaRegen,
        /// <summary>跑速（moveSpeed）。</summary>
        MoveSpeed,
        /// <summary>翻滾耐力消耗（rollStaminaCost）。</summary>
        RollStaminaCost,
        /// <summary>格擋減傷（blockDamageReduction，0..1）。</summary>
        BlockDamageReduction,
        /// <summary>盾反判定時間（parryWindow）。</summary>
        ParryWindow,
        /// <summary>盾反失衡值（parryImbalance）。</summary>
        ParryImbalance,
        /// <summary>骨頭持有上限（boneCount，四捨五入成整數）。</summary>
        BoneCapacity,
        /// <summary>骨頭回血量（boneHealAmount）。</summary>
        BoneHealAmount,
        /// <summary>輕攻擊傷害（lightCombo 三段一起）。</summary>
        LightAttackDamage,
        /// <summary>重擊傷害（heavyAttack.damage）。</summary>
        HeavyAttackDamage,
        /// <summary>處決傷害（executeAttack.damage）。</summary>
        ExecutionDamage,
    }
}
