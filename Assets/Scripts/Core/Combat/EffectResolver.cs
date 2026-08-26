using System;
using System.Collections.Generic;
using STS.Core.Cards;

namespace STS.Core.Combat
{
    /// <summary>
    /// 效果步驟解讀器。卡牌/藥水/敵招共用同一條結算路。
    /// M1 只支援切片基礎 op;範圍外一律明確拋錯,絕不靜默跳過(假綠比紅更毒)。
    /// </summary>
    internal static class EffectResolver
    {
        // 共用目標緩衝:結算不重入(M1),重入需求出現時再改
        private static readonly List<int> TargetBuffer = new List<int>();

        internal static void Resolve(CombatEngine engine, EffectStep[] steps, int sourceIndex, int chosenTargetIndex)
        {
            if (steps == null) return;
            for (int s = 0; s < steps.Length; s++)
            {
                if (!engine.GetCombatant(sourceIndex).IsAlive) return;

                var step = steps[s];
                if (step.AmountKind != AmountKind.Fixed)
                {
                    throw new NotSupportedException($"AmountKind {step.AmountKind} 屬 M2 範圍,尚未實作");
                }
                if (step.RepeatIsX)
                {
                    throw new NotSupportedException("RepeatIsX(X 費段數)屬 M2 範圍,尚未實作");
                }

                int repeat = step.Repeat <= 1 ? 1 : step.Repeat;
                switch (step.Op)
                {
                    case EffectOp.Damage:
                        ResolveDamage(engine, step, sourceIndex, chosenTargetIndex, repeat);
                        break;
                    case EffectOp.Block:
                        for (int r = 0; r < repeat; r++)
                        {
                            var targets = CollectTargets(engine, sourceIndex, step.Target, chosenTargetIndex);
                            for (int t = 0; t < targets.Count; t++)
                            {
                                engine.GainBlock(targets[t], step.Amount);
                            }
                        }
                        break;
                    case EffectOp.ApplyStatus:
                        for (int r = 0; r < repeat; r++)
                        {
                            var targets = CollectTargets(engine, sourceIndex, step.Target, chosenTargetIndex);
                            for (int t = 0; t < targets.Count; t++)
                            {
                                engine.ApplyStatusTo(targets[t], step.Status, step.Amount);
                            }
                        }
                        break;
                    case EffectOp.Draw:
                        engine.DrawCards(step.Amount);
                        break;
                    case EffectOp.GainEnergy:
                        engine.GainEnergy(step.Amount);
                        break;
                    default:
                        throw new NotSupportedException($"EffectOp {step.Op} 屬 M2+ 範圍,尚未實作");
                }
            }
        }

        /// <summary>多段攻擊:每段重新取目標(隨機目標每段重擲);沒有活目標時剩餘段全跳。</summary>
        private static void ResolveDamage(CombatEngine engine, EffectStep step, int sourceIndex, int chosenTargetIndex, int repeat)
        {
            for (int r = 0; r < repeat; r++)
            {
                var targets = CollectTargets(engine, sourceIndex, step.Target, chosenTargetIndex);
                if (targets.Count == 0) return;
                for (int t = 0; t < targets.Count; t++)
                {
                    engine.DealAttackDamage(sourceIndex, targets[t], step.Amount);
                }
            }
        }

        /// <summary>目標語意相對於施放者:敵人施放時 TargetEnemy/AllEnemies/RandomEnemy 都指玩家。只收活著的目標。</summary>
        private static List<int> CollectTargets(CombatEngine engine, int sourceIndex, EffectTarget target, int chosenTargetIndex)
        {
            TargetBuffer.Clear();
            if (sourceIndex == CombatEngine.PlayerIndex)
            {
                switch (target)
                {
                    case EffectTarget.Self:
                        TargetBuffer.Add(CombatEngine.PlayerIndex);
                        break;
                    case EffectTarget.TargetEnemy:
                        if (chosenTargetIndex >= 0
                            && chosenTargetIndex < engine.State.Enemies.Count
                            && engine.State.Enemies[chosenTargetIndex].IsAlive)
                        {
                            TargetBuffer.Add(chosenTargetIndex);
                        }
                        break;
                    case EffectTarget.AllEnemies:
                        engine.CollectLivingEnemies(TargetBuffer);
                        break;
                    case EffectTarget.RandomEnemy:
                        engine.CollectLivingEnemies(TargetBuffer);
                        if (TargetBuffer.Count > 1)
                        {
                            int picked = TargetBuffer[engine.CombatMiscRng.NextInt(TargetBuffer.Count)];
                            TargetBuffer.Clear();
                            TargetBuffer.Add(picked);
                        }
                        break;
                }
            }
            else
            {
                if (target == EffectTarget.Self)
                {
                    TargetBuffer.Add(sourceIndex);
                }
                else if (engine.State.Player.IsAlive)
                {
                    TargetBuffer.Add(CombatEngine.PlayerIndex);
                }
            }
            return TargetBuffer;
        }
    }
}
