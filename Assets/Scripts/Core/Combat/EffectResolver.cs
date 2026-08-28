using System;
using System.Collections.Generic;
using STS.Core.Cards;

namespace STS.Core.Combat
{
    /// <summary>
    /// 效果步驟解讀器。卡牌/藥水/敵招共用同一條結算路。
    /// 範圍外的 op(Custom)一律明確拋錯,絕不靜默跳過(假綠比紅更毒)。
    /// ChooseExhaustFromHand 會中斷結算:剩餘步驟交回引擎暫存,待 ResolveChoice 續跑。
    /// </summary>
    internal static class EffectResolver
    {
        /// <param name="ignoreModifiers">
        /// true = 數值不吃任何增益/減益(力量、虛弱、易傷、敏捷、脆弱)。藥水走這條:
        /// 藥水是瓶裝的固定效果,不該被角色狀態放大或削弱。
        /// </param>
        internal static void Resolve(CombatEngine engine, EffectStep[] steps, int sourceIndex, int chosenTargetIndex,
            bool ignoreModifiers = false)
        {
            ResolveFrom(engine, steps, 0, sourceIndex, chosenTargetIndex, ignoreModifiers);
        }

        internal static void ResolveFrom(CombatEngine engine, EffectStep[] steps, int startIndex, int sourceIndex,
            int chosenTargetIndex, bool ignoreModifiers = false)
        {
            if (steps == null) return;
            for (int s = startIndex; s < steps.Length; s++)
            {
                if (!engine.GetCombatant(sourceIndex).IsAlive) return;   // 施放者已死(尖刺皮/反傷情境)即中止

                var step = steps[s];
                int repeat = step.RepeatIsX ? engine.XEnergySpent : (step.Repeat <= 1 ? 1 : step.Repeat);

                switch (step.Op)
                {
                    case EffectOp.Damage:
                        ResolveDamage(engine, step, sourceIndex, chosenTargetIndex, repeat, ignoreModifiers);
                        break;

                    case EffectOp.Block:
                        for (int r = 0; r < repeat; r++)
                        {
                            var targets = CollectTargets(engine, sourceIndex, step.Target, chosenTargetIndex);
                            for (int t = 0; t < targets.Count; t++)
                            {
                                int amount = ResolveAmount(engine, step, sourceIndex, chosenTargetIndex);
                                if (ignoreModifiers)
                                {
                                    engine.GainBlockRaw(targets[t], amount);
                                }
                                else
                                {
                                    engine.GainBlock(targets[t], amount);
                                }
                            }
                        }
                        break;

                    case EffectOp.ApplyStatus:
                        for (int r = 0; r < repeat; r++)
                        {
                            var targets = CollectTargets(engine, sourceIndex, step.Target, chosenTargetIndex);
                            for (int t = 0; t < targets.Count; t++)
                            {
                                // 層數也可以是成長型(主宰:敵人每有一層易傷就給 1 點力量)
                                engine.ApplyStatusTo(targets[t], step.Status,
                                    ResolveAmount(engine, step, sourceIndex, chosenTargetIndex));
                            }
                        }
                        break;

                    case EffectOp.ExhaustTopOfDraw:
                        engine.ExhaustTopOfDraw(step.Amount <= 0 ? 1 : step.Amount);
                        break;

                    case EffectOp.DoubleStatus:
                        for (int r = 0; r < repeat; r++)
                        {
                            var targets = CollectTargets(engine, sourceIndex, step.Target, chosenTargetIndex);
                            for (int t = 0; t < targets.Count; t++)
                            {
                                int stacks = engine.GetCombatant(targets[t]).GetStatus(step.Status);
                                if (stacks > 0) engine.ApplyStatusTo(targets[t], step.Status, stacks);
                            }
                        }
                        break;

                    case EffectOp.Draw:
                        engine.DrawCards(ResolveAmount(engine, step, sourceIndex, chosenTargetIndex));
                        break;

                    case EffectOp.GainEnergy:
                        engine.GainEnergy(ResolveAmount(engine, step, sourceIndex, chosenTargetIndex));
                        break;

                    case EffectOp.LoseHp:
                        for (int r = 0; r < repeat; r++)
                        {
                            var targets = CollectTargets(engine, sourceIndex, step.Target, chosenTargetIndex);
                            for (int t = 0; t < targets.Count; t++)
                            {
                                engine.LoseHpDirect(targets[t], ResolveAmount(engine, step, sourceIndex, chosenTargetIndex));
                            }
                        }
                        break;

                    case EffectOp.Heal:
                        for (int r = 0; r < repeat; r++)
                        {
                            var targets = CollectTargets(engine, sourceIndex, step.Target, chosenTargetIndex);
                            for (int t = 0; t < targets.Count; t++)
                            {
                                engine.HealHp(targets[t], ResolveAmount(engine, step, sourceIndex, chosenTargetIndex));
                            }
                        }
                        break;

                    case EffectOp.AddCardToPile:
                    {
                        int copies = step.SecondaryAmount <= 0 ? 1 : step.SecondaryAmount;
                        for (int c = 0; c < copies; c++)
                        {
                            engine.AddCardToPile(step.CardId, step.Pile);
                        }
                        break;
                    }

                    case EffectOp.ExhaustRandomFromHand:
                    {
                        int count = step.Amount <= 0 ? 1 : step.Amount;
                        engine.ExhaustRandomFromHand(count);
                        break;
                    }

                    case EffectOp.ChooseExhaustFromHand:
                    {
                        if (engine.State.Hand.Count == 0) break;   // 沒得選就跳過本步,繼續後續步驟
                        int count = step.Amount <= 0 ? 1 : step.Amount;
                        if (count > engine.State.Hand.Count) count = engine.State.Hand.Count;
                        engine.RequestChoice(steps, s + 1, sourceIndex, chosenTargetIndex, count, ignoreModifiers);
                        return;   // 中斷:剩餘步驟由 ResolveChoice 續跑
                    }

                    default:
                        throw new NotSupportedException($"EffectOp {step.Op} 尚未實作(Custom 屬 M3+ 逃生門)");
                }
            }
        }

        /// <summary>多段攻擊:每段重新取目標(隨機目標每段重擲);沒有活目標時剩餘段全跳。</summary>
        private static void ResolveDamage(CombatEngine engine, EffectStep step, int sourceIndex, int chosenTargetIndex,
            int repeat, bool ignoreModifiers)
        {
            for (int r = 0; r < repeat; r++)
            {
                var targets = CollectTargets(engine, sourceIndex, step.Target, chosenTargetIndex);
                if (targets.Count == 0) return;
                int baseAmount = ResolveDamageBase(engine, step, sourceIndex, targets[0]);
                for (int t = 0; t < targets.Count; t++)
                {
                    if (ignoreModifiers)
                    {
                        // 固定傷害:吃格擋,但不吃力量/虛弱/易傷,也不觸發攻擊 hook
                        engine.DealNonAttackDamage(sourceIndex, targets[t], baseAmount);
                    }
                    else
                    {
                        engine.DealAttackDamage(sourceIndex, targets[t], baseAmount);
                    }
                }
            }
        }

        /// <summary>非 Damage op 的數值解讀。StrengthTimes 只對 Damage 有意義,其他 op 用到就是資料錯。</summary>
        private static int ResolveAmount(CombatEngine engine, EffectStep step, int sourceIndex, int targetIndex)
        {
            switch (step.AmountKind)
            {
                case AmountKind.Fixed:
                    return step.Amount;
                case AmountKind.XEnergy:
                    return engine.XEnergySpent;
                case AmountKind.CurrentBlock:
                    return engine.GetCombatant(sourceIndex).Block;
                case AmountKind.PerExhaustedCard:
                case AmountKind.PerTargetVulnerable:
                case AmountKind.PerAttackPlayedThisTurn:
                case AmountKind.PerStrikeCard:
                    return step.Amount + step.SecondaryAmount
                        * engine.ScalingCount(step.AmountKind, TargetVulnerable(engine, targetIndex));
                default:
                    throw new NotSupportedException($"AmountKind {step.AmountKind} 不適用於 {step.Op}");
            }
        }

        private static int TargetVulnerable(CombatEngine engine, int targetIndex)
        {
            if (targetIndex < 0 || targetIndex >= engine.State.Enemies.Count) return 0;
            return engine.GetCombatant(targetIndex).GetStatus(Statuses.StatusId.Vulnerable);
        }

        /// <summary>
        /// Damage 的基礎值解讀。StrengthTimes:力量以 SecondaryAmount 倍計——
        /// 基礎值先加上 力量×(倍率-1),再走 CombatMath(那裡會再加一次力量),總計恰為 N 倍。
        /// </summary>
        private static int ResolveDamageBase(CombatEngine engine, EffectStep step, int sourceIndex, int targetIndex)
        {
            switch (step.AmountKind)
            {
                case AmountKind.Fixed:
                    return step.Amount;
                case AmountKind.XEnergy:
                    return engine.XEnergySpent;
                case AmountKind.CurrentBlock:
                    return engine.GetCombatant(sourceIndex).Block;
                case AmountKind.PerExhaustedCard:
                case AmountKind.PerTargetVulnerable:
                case AmountKind.PerAttackPlayedThisTurn:
                case AmountKind.PerStrikeCard:
                    return step.Amount + step.SecondaryAmount
                        * engine.ScalingCount(step.AmountKind, TargetVulnerable(engine, targetIndex));
                case AmountKind.StrengthTimes:
                {
                    int multiplier = step.SecondaryAmount <= 1 ? 1 : step.SecondaryAmount;
                    int strength = engine.GetCombatant(sourceIndex).GetStatus(Statuses.StatusId.Strength);
                    return step.Amount + strength * (multiplier - 1);
                }
                default:
                    throw new NotSupportedException($"AmountKind {step.AmountKind} 尚未實作");
            }
        }

        /// <summary>目標語意相對於施放者:敵人施放時 TargetEnemy/AllEnemies/RandomEnemy 都指玩家。只收活著的目標。</summary>
        private static List<int> CollectTargets(CombatEngine engine, int sourceIndex, EffectTarget target, int chosenTargetIndex)
        {
            // 每次配置新緩衝:hook 反應可能巢狀觸發結算,共用靜態緩衝會被污染(邏輯層,不在每幀熱路徑)
            var buffer = new List<int>(4);
            if (sourceIndex == CombatEngine.PlayerIndex)
            {
                switch (target)
                {
                    case EffectTarget.Self:
                        buffer.Add(CombatEngine.PlayerIndex);
                        break;
                    case EffectTarget.TargetEnemy:
                        if (chosenTargetIndex >= 0
                            && chosenTargetIndex < engine.State.Enemies.Count
                            && engine.State.Enemies[chosenTargetIndex].IsAlive)
                        {
                            buffer.Add(chosenTargetIndex);
                        }
                        break;
                    case EffectTarget.AllEnemies:
                        engine.CollectLivingEnemies(buffer);
                        break;
                    case EffectTarget.RandomEnemy:
                        engine.CollectLivingEnemies(buffer);
                        if (buffer.Count > 1)
                        {
                            int picked = buffer[engine.CombatMiscRng.NextInt(buffer.Count)];
                            buffer.Clear();
                            buffer.Add(picked);
                        }
                        break;
                }
            }
            else
            {
                if (target == EffectTarget.Self)
                {
                    buffer.Add(sourceIndex);
                }
                else if (engine.State.Player.IsAlive)
                {
                    buffer.Add(CombatEngine.PlayerIndex);
                }
            }
            return buffer;
        }
    }
}
