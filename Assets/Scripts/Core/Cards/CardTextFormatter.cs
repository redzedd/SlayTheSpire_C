using STS.Core.Combat;
using STS.Core.Combat.Statuses;

namespace STS.Core.Cards
{
    /// <summary>
    /// 卡牌描述模板的數值代入(純邏輯,UI 直接顯示結果)。
    /// {dmg} = 第一個 Damage 步驟的即時值:含自身力量/虛弱,並在有 target 時含目標易傷;
    /// {blk} = 第一個 Block 步驟含敏捷/脆弱的即時值(與目標無關)。
    /// target 由 UI 決定:場上只剩一個活敵時就是牠;多敵時為拖曳中指向的敵人,未指向則 null。
    /// engine 是戰鬥中才有的即時狀態,成長型卡(完美打擊/灰燼打擊/焚燒)靠它算出真實數字;
    /// 戰鬥外(商店/獎勵/牌組檢視)傳 null,顯示基礎值。
    /// </summary>
    public static class CardTextFormatter
    {
        public static string FormatDescription(CardDef def, CombatantState player, CombatantState target = null,
            CombatEngine engine = null)
        {
            string text = def.DescriptionTemplate ?? string.Empty;
            if (text.Contains("{dmg}"))
            {
                text = text.Replace("{dmg}", ComputeDamagePreview(def, player, target, engine).ToString());
            }
            if (text.Contains("{blk}"))
            {
                text = text.Replace("{blk}", ComputeBlockPreview(def, player).ToString());
            }
            return text;
        }

        private static int ComputeDamagePreview(CardDef def, CombatantState player, CombatantState target,
            CombatEngine engine)
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
                else if (CombatEngine.IsScalingKind(step.AmountKind))
                {
                    // 成長型:走引擎那份計數,預覽與結算不會各算各的。
                    // 易傷層數不必等引擎,手上的 target 就有——所以沒指到敵人時也照樣顯示其他三種成長。
                    int targetVulnerable = target != null ? target.GetStatus(StatusId.Vulnerable) : 0;
                    int count = engine != null
                        ? engine.ScalingCount(step.AmountKind, targetVulnerable)
                        : (step.AmountKind == AmountKind.PerTargetVulnerable ? targetVulnerable : 0);
                    baseAmount = step.Amount + step.SecondaryAmount * count;
                }
                // 與 CombatEngine.DealAttackDamage 同一條公式,預覽才不會跟結算對不上
                return CombatMath.CalculateAttackDamage(
                    baseAmount,
                    strength,
                    player.GetStatus(StatusId.Weak) > 0,
                    target != null && target.GetStatus(StatusId.Vulnerable) > 0);
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
