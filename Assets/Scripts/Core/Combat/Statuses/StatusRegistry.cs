namespace STS.Core.Combat.Statuses
{
    /// <summary>狀態的回合衰減規則。</summary>
    public enum DecayRule
    {
        None,
        /// <summary>擁有者回合結束 -1(虛弱/易傷/脆弱);同回合剛施加者首次跳過。</summary>
        DecrementAtOwnerTurnEnd,
        /// <summary>擁有者回合結束整個移除(NoDraw)。</summary>
        RemoveAtOwnerTurnEnd
    }

    /// <summary>
    /// 狀態行為註冊表:行為在程式、數值在資料。
    /// 只做「反應」(加狀態/格擋/反傷);傷害修飾固定在 CombatMath/BlockMath,不在這裡。
    /// </summary>
    internal static class StatusRegistry
    {
        internal static DecayRule GetDecayRule(StatusId id)
        {
            switch (id)
            {
                case StatusId.Weak:
                case StatusId.Vulnerable:
                case StatusId.Frail:
                    return DecayRule.DecrementAtOwnerTurnEnd;
                case StatusId.NoDraw:
                    return DecayRule.RemoveAtOwnerTurnEnd;
                default:
                    return DecayRule.None;
            }
        }

        internal static void OnHook(CombatEngine engine, int ownerIndex, StatusInstance status, in HookContext ctx)
        {
            switch (status.Id)
            {
                case StatusId.Ritual:
                    // 儀式:擁有者回合結束獲得等同層數的力量
                    if (IsOwnersTurnEnd(ctx, ownerIndex))
                    {
                        engine.ApplyStatusTo(ownerIndex, StatusId.Strength, status.Stacks);
                    }
                    break;

                case StatusId.Metallicize:
                    // 金屬化:擁有者回合結束獲得等同層數的格擋(活過對方回合,因為清除在自己回合開始)
                    if (IsOwnersTurnEnd(ctx, ownerIndex))
                    {
                        engine.GainBlock(ownerIndex, status.Stacks);
                    }
                    break;

                case StatusId.DemonForm:
                    // 惡魔化:擁有者回合開始獲得等同層數的力量
                    if (IsOwnersTurnStart(ctx, ownerIndex))
                    {
                        engine.ApplyStatusTo(ownerIndex, StatusId.Strength, status.Stacks);
                    }
                    break;

                case StatusId.Enrage:
                    // 激怒:玩家每打出一張技能牌,擁有者(敵)獲得等同層數的力量
                    if (ctx.Point == HookPoint.CardPlayed
                        && ctx.SourceIndex == CombatEngine.PlayerIndex
                        && ctx.CardType == Cards.CardType.Skill
                        && ownerIndex != CombatEngine.PlayerIndex)
                    {
                        engine.ApplyStatusTo(ownerIndex, StatusId.Strength, status.Stacks);
                    }
                    break;

                case StatusId.SharpHide:
                    // 尖刺皮:玩家每打出一張攻擊牌,對玩家造成等同層數的非攻擊傷害
                    if (ctx.Point == HookPoint.CardPlayed
                        && ctx.SourceIndex == CombatEngine.PlayerIndex
                        && ctx.CardType == Cards.CardType.Attack
                        && ownerIndex != CombatEngine.PlayerIndex)
                    {
                        engine.DealNonAttackDamage(ownerIndex, CombatEngine.PlayerIndex, status.Stacks);
                    }
                    break;

                case StatusId.Curl:
                    // 捲曲:首次受到攻擊掉血時獲得等同層數的格擋,之後移除([近似] 蝨子行為,測試鎖定)
                    if (ctx.Point == HookPoint.AttackReceived
                        && ctx.TargetIndex == ownerIndex
                        && ctx.Amount > 0)
                    {
                        int stacks = status.Stacks;
                        engine.GainBlock(ownerIndex, stacks);
                        engine.ApplyStatusTo(ownerIndex, StatusId.Curl, -stacks);
                    }
                    break;

                case StatusId.LoseStrengthAtTurnEnd:
                    // 失力(屈膝型):擁有者回合結束失去等同層數的力量,然後自身移除
                    if (IsOwnersTurnEnd(ctx, ownerIndex))
                    {
                        int stacks = status.Stacks;
                        engine.ApplyStatusTo(ownerIndex, StatusId.Strength, -stacks);
                        engine.ApplyStatusTo(ownerIndex, StatusId.LoseStrengthAtTurnEnd, -stacks);
                    }
                    break;

                // Strength/Dexterity/Weak/Vulnerable/Frail/NoDraw:被動修正或流程旗標,結算住在 CombatMath/BlockMath/抽牌流程
                default:
                    break;
            }
        }

        private static bool IsOwnersTurnEnd(in HookContext ctx, int ownerIndex)
        {
            if (ownerIndex == CombatEngine.PlayerIndex)
            {
                return ctx.Point == HookPoint.PlayerTurnEnd;
            }
            return ctx.Point == HookPoint.EnemyTurnEnd && ctx.SourceIndex == ownerIndex;
        }

        private static bool IsOwnersTurnStart(in HookContext ctx, int ownerIndex)
        {
            if (ownerIndex == CombatEngine.PlayerIndex)
            {
                return ctx.Point == HookPoint.PlayerTurnStart;
            }
            return ctx.Point == HookPoint.EnemyTurnStart && ctx.SourceIndex == ownerIndex;
        }
    }
}
