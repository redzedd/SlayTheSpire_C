namespace STS.Core.Combat
{
    /// <summary>
    /// 一次攻擊套用到目標後的結算結果。
    /// </summary>
    public readonly struct AttackResult
    {
        /// <summary>被格擋吸收的傷害量。</summary>
        public readonly int BlockConsumed;
        /// <summary>實際扣除的生命值。</summary>
        public readonly int HpLost;
        /// <summary>結算後剩餘格擋。</summary>
        public readonly int RemainingBlock;
        /// <summary>結算後剩餘生命。</summary>
        public readonly int RemainingHp;

        public AttackResult(int blockConsumed, int hpLost, int remainingBlock, int remainingHp)
        {
            BlockConsumed = blockConsumed;
            HpLost = hpLost;
            RemainingBlock = remainingBlock;
            RemainingHp = remainingHp;
        }
    }

    /// <summary>
    /// 戰鬥數值結算的純邏輯(不依賴 UnityEngine,可直接單元測試)。
    /// 結算順序依 Slay the Spire 規則:力量加算 → 虛弱(×0.75) → 易傷(×1.5),浮點連乘後最終無條件捨去。
    /// </summary>
    public static class CombatMath
    {
        public const float VulnerableMultiplier = 1.5f;
        public const float WeakMultiplier = 0.75f;
        /// <summary>巨像:易傷的攻擊者對你只造成一半傷害。</summary>
        public const float ColossusMultiplier = 0.5f;

        /// <summary>
        /// 計算一次攻擊的最終傷害值(尚未扣格擋)。
        /// </summary>
        /// <param name="baseDamage">卡牌基礎傷害。</param>
        /// <param name="attackerStrength">攻擊者力量(可為負,如衰弱效果)。</param>
        /// <param name="attackerWeak">攻擊者是否處於虛弱。</param>
        /// <param name="targetVulnerable">目標是否處於易傷。</param>
        /// <param name="vulnerableBonusPercent">
        /// 攻擊者對易傷目標的額外加成百分比(殘酷)。只在目標易傷時生效,直接加在 1.5 倍上。
        /// </param>
        /// <param name="halveFromVulnerableAttacker">
        /// 受擊方減半(巨像):攻擊者自己處於易傷時,這次傷害砍半。由呼叫端判定條件是否成立。
        /// </param>
        public static int CalculateAttackDamage(int baseDamage, int attackerStrength, bool attackerWeak,
            bool targetVulnerable, int vulnerableBonusPercent = 0, bool halveFromVulnerableAttacker = false)
        {
            float damage = baseDamage + attackerStrength;
            if (attackerWeak)
            {
                damage *= WeakMultiplier;
            }
            if (targetVulnerable)
            {
                damage *= VulnerableMultiplier + vulnerableBonusPercent / 100f;
            }
            if (halveFromVulnerableAttacker)
            {
                damage *= ColossusMultiplier;
            }
            int result = (int)System.Math.Floor(damage);
            return result < 0 ? 0 : result;
        }

        /// <summary>
        /// 把最終傷害套用到目標的格擋與生命上:格擋先吸收,剩餘才扣血。
        /// </summary>
        public static AttackResult ResolveAttack(int incomingDamage, int currentBlock, int currentHp)
        {
            if (incomingDamage < 0) incomingDamage = 0;
            int blockConsumed = incomingDamage < currentBlock ? incomingDamage : currentBlock;
            int hpLost = incomingDamage - blockConsumed;
            if (hpLost > currentHp) hpLost = currentHp;
            return new AttackResult(
                blockConsumed,
                hpLost,
                currentBlock - blockConsumed,
                currentHp - hpLost);
        }
    }
}
