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
                case StatusId.Rage:
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
                    // 尖刺皮:每被攻擊命中一次就反彈一次(多段攻擊打幾下就反彈幾下)
                    if (ctx.Point == HookPoint.AttackDealt
                        && ctx.SourceIndex == CombatEngine.PlayerIndex
                        && ctx.TargetIndex == ownerIndex)
                    {
                        engine.DealNonAttackDamage(ownerIndex, CombatEngine.PlayerIndex, status.Stacks);
                    }
                    break;

                case StatusId.Curl:
                    // 捲曲:首次受到攻擊掉血時觸發,但格擋要等整張牌(或藥水)打完才上——
                    // 立刻上盾會讓同一張多段攻擊的後續段被自己觸發的盾擋掉,傷害就算錯了。
                    // 狀態當場移除,避免同一張牌的後續段重複觸發。
                    if (ctx.Point == HookPoint.AttackReceived
                        && ctx.TargetIndex == ownerIndex
                        && ctx.Amount > 0)
                    {
                        int stacks = status.Stacks;
                        engine.ApplyStatusTo(ownerIndex, StatusId.Curl, -stacks);
                        engine.DeferBlock(ownerIndex, stacks);
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

                case StatusId.FeelNoPain:
                    // 無懼疼痛:每有一張牌被消耗就補盾
                    if (ctx.Point == HookPoint.CardExhausted && ownerIndex == CombatEngine.PlayerIndex)
                    {
                        engine.GainBlock(ownerIndex, status.Stacks);
                    }
                    break;

                case StatusId.DarkEmbrace:
                    // 黑暗之擁:每有一張牌被消耗就抽牌
                    if (ctx.Point == HookPoint.CardExhausted && ownerIndex == CombatEngine.PlayerIndex)
                    {
                        engine.DrawCards(status.Stacks);
                    }
                    break;

                case StatusId.Juggernaut:
                    // 勢不可當:自己獲得格擋就砸隨機敵人。走非攻擊傷害——否則會與尖刺皮互相觸發成迴圈
                    if (ctx.Point == HookPoint.BlockGained
                        && ctx.SourceIndex == ownerIndex
                        && ownerIndex == CombatEngine.PlayerIndex)
                    {
                        int enemyIndex = engine.PickRandomLivingEnemy();
                        if (enemyIndex >= 0)
                        {
                            engine.DealNonAttackDamage(ownerIndex, enemyIndex, status.Stacks);
                        }
                    }
                    break;

                case StatusId.Rupture:
                    // 撕裂:只在自己的回合失血才算(敵人打你不算)
                    if (ctx.Point == HookPoint.HpLost
                        && ctx.TargetIndex == ownerIndex
                        && engine.IsOwnersTurn(ownerIndex)
                        && ctx.Amount > 0)
                    {
                        engine.ApplyStatusTo(ownerIndex, StatusId.Strength, status.Stacks);
                    }
                    break;

                case StatusId.Inferno:
                    if (IsOwnersTurnStart(ctx, ownerIndex))
                    {
                        engine.LoseHpDirect(ownerIndex, 1);
                    }
                    else if (ctx.Point == HookPoint.HpLost
                        && ctx.TargetIndex == ownerIndex
                        && engine.IsOwnersTurn(ownerIndex)
                        && ctx.Amount > 0)
                    {
                        engine.DamageAllEnemiesNonAttack(ownerIndex, status.Stacks);
                    }
                    break;

                case StatusId.Pyre:
                    if (IsOwnersTurnStart(ctx, ownerIndex))
                    {
                        engine.GainEnergy(status.Stacks);
                    }
                    break;

                case StatusId.DrumOfBattle:
                    if (IsOwnersTurnStart(ctx, ownerIndex))
                    {
                        engine.ExhaustTopOfDraw(status.Stacks);
                    }
                    break;

                case StatusId.HowlFromBeyond:
                    // 走真正的攻擊傷害:力量/易傷要照算,尖刺皮也該反彈——這就是「再打一次」
                    if (IsOwnersTurnStart(ctx, ownerIndex) && ownerIndex == CombatEngine.PlayerIndex)
                    {
                        engine.DamageAllEnemiesAttack(ownerIndex, status.Stacks);
                    }
                    break;

                case StatusId.CrimsonMantle:
                    if (IsOwnersTurnStart(ctx, ownerIndex))
                    {
                        engine.LoseHpDirect(ownerIndex, 1);
                        engine.GainBlock(ownerIndex, status.Stacks);
                    }
                    break;

                case StatusId.Rage:
                    // 狂怒:本回合每打出一張攻擊牌就補盾
                    if (ctx.Point == HookPoint.CardPlayed
                        && ctx.SourceIndex == ownerIndex
                        && ctx.CardType == Cards.CardType.Attack)
                    {
                        engine.GainBlock(ownerIndex, status.Stacks);
                    }
                    break;

                // Strength/Dexterity/Weak/Vulnerable/Frail/NoDraw/Barricade:
                // 被動修正或流程旗標,結算住在 CombatMath/BlockMath/抽牌流程/清格擋處
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
