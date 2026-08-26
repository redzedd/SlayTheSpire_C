namespace STS.Core.Combat
{
    /// <summary>
    /// 格擋獲得公式的純邏輯:敏捷加算 → 脆弱 ×0.75,浮點乘後無條件捨去,不低於 0。
    /// 與 CombatMath(攻擊面)成對;傷害/格擋修飾只住這兩個類,hook 不得另起爐灶。
    /// </summary>
    public static class BlockMath
    {
        public const float FrailMultiplier = 0.75f;

        public static int CalculateBlockGain(int baseBlock, int dexterity, bool isFrail)
        {
            float block = baseBlock + dexterity;
            if (isFrail)
            {
                block *= FrailMultiplier;
            }
            int result = (int)System.Math.Floor(block);
            return result < 0 ? 0 : result;
        }
    }
}
