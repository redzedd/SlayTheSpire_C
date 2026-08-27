using STS.Core.Combat;
using STS.Core.Combat.Statuses;

namespace STS.Core.Cards
{
    /// <summary>
    /// 卡牌描述模板的數值代入(純邏輯,UI 直接顯示結果)。
    /// {dmg} = 第一個 Damage 步驟含力量/虛弱的即時值(不含目標易傷——未選目標時的基準顯示);
    /// {blk} = 第一個 Block 步驟含敏捷/脆弱的即時值。
    /// </summary>
    public static class CardTextFormatter
    {
        public static string FormatDescription(CardDef def, CombatantState player)
        {
            string text = def.DescriptionTemplate ?? string.Empty;
            if (text.Contains("{dmg}"))
            {
                text = text.Replace("{dmg}", ComputeDamagePreview(def, player).ToString());
            }
            if (text.Contains("{blk}"))
            {
                text = text.Replace("{blk}", ComputeBlockPreview(def, player).ToString());
            }
            return text;
        }

        private static int ComputeDamagePreview(CardDef def, CombatantState player)
        {
            foreach (var step in def.Steps)
            {
                if (step.Op != EffectOp.Damage) continue;
                int strength = player.GetStatus(StatusId.Strength);
                int baseAmount = step.Amount;
                if (step.AmountKind == AmountKind.CurrentBlock)
                {
                    baseAmount = player.Block;
                }
                else if (step.AmountKind == AmountKind.StrengthTimes)
                {
                    int multiplier = step.SecondaryAmount <= 1 ? 1 : step.SecondaryAmount;
                    baseAmount = step.Amount + strength * (multiplier - 1);
                }
                return CombatMath.CalculateAttackDamage(
                    baseAmount, strength, player.GetStatus(StatusId.Weak) > 0, false);
            }
            return 0;
        }

        private static int ComputeBlockPreview(CardDef def, CombatantState player)
        {
            foreach (var step in def.Steps)
            {
                if (step.Op != EffectOp.Block) continue;
                int baseAmount = step.AmountKind == AmountKind.CurrentBlock ? player.Block : step.Amount;
                return BlockMath.CalculateBlockGain(
                    baseAmount, player.GetStatus(StatusId.Dexterity), player.GetStatus(StatusId.Frail) > 0);
            }
            return 0;
        }
    }
}
