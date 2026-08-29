namespace STS.Core.Combat.Statuses
{
    /// <summary>狀態的回合衰減規則。</summary>
    public enum DecayRule
    {
        None,
        /// <summary>擁有者回合結束 -1(虛弱/易傷/脆弱);同回合剛施加者首次跳過。</summary>
        DecrementAtOwnerTurnEnd,
        /// <summary>擁有者回合結束整個移除(NoDraw)。</summary>
        RemoveAtOwnerTurnEnd,
        /// <summary>
        /// 擁有者「下一個回合開始」才整個移除,與格擋同一個生命週期(巨像)。
        /// 要擋對手回合的攻擊,就不能在自己回合結束時就消失。
        /// </summary>
        RemoveAtOwnerTurnStart
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
                case StatusId.Grapple:
                case StatusId.NoEnergyGain:
                    return DecayRule.RemoveAtOwnerTurnEnd;
                case StatusId.Colossus:
                    return DecayRule.RemoveAtOwnerTurnStart;
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

                case StatusId.Aggression:
                    // 好勇鬥狠:回合開始從棄牌堆撈一張攻擊牌到手上並升級
                    if (IsOwnersTurnStart(ctx, ownerIndex) && ownerIndex == CombatEngine.PlayerIndex)
                    {
                        engine.PullRandomAttackFromDiscardAndUpgrade();
                    }
                    break;

                case StatusId.Hellraiser:
                    // 地獄狂徒:抽到名字含「打擊」的牌就立刻打出它(剛抽的牌在手牌尾端)
                    if (ctx.Point == HookPoint.CardDrawn && ownerIndex == CombatEngine.PlayerIndex)
                    {
                        engine.AutoPlayLastDrawnIfNameContains("打擊");
                    }
                    break;

                case StatusId.Stampede:
                    // 驚逃:回合結束隨機打出手上一張攻擊牌
                    if (IsOwnersTurnEnd(ctx, ownerIndex) && ownerIndex == CombatEngine.PlayerIndex)
                    {
                        int index = engine.FindRandomAttackInHand();
                        if (index >= 0) engine.PlayHandCardAuto(index);
                    }
                    break;

                case StatusId.Juggling:
                    // 雜耍:本回合的第三張攻擊牌。計數在出牌結算完才 +1,
                    // 所以 hook 當下的值是「這張之前已打出幾張」——第三張時它剛好是 2。
                    if (ctx.Point == HookPoint.CardPlayed
                        && ownerIndex == CombatEngine.PlayerIndex
                        && ctx.CardType == Cards.CardType.Attack
                        && engine.State.AttacksPlayedThisTurn == 2
                        && !string.IsNullOrEmpty(ctx.CardId))
                    {
                        engine.AddCardToPile(ctx.CardId, Cards.PileType.Hand);
                    }
                    break;

                case StatusId.Plating:
                    // 覆甲:回合結束給等量護甲,然後自己減 1
                    if (IsOwnersTurnEnd(ctx, ownerIndex))
                    {
                        int armor = status.Stacks;
                        engine.GainBlock(ownerIndex, armor);
                        engine.ApplyStatusTo(ownerIndex, StatusId.Plating, -1);
                    }
                    break;

                case StatusId.Grapple:
                    // 擒拿:本回合獲得格擋就追打。走非攻擊傷害,避免與尖刺皮互相觸發
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

                case StatusId.Vicious:
                    // 兇惡:自己對敵人施加易傷就抽牌
                    // 施加者用「現在是玩家回合」判定,而不是把 SourceIndex 一路穿過
                    // ApplyStatusTo 的二十幾個呼叫點——敵人回合上的易傷不算你施加的
                    if (ctx.Point == HookPoint.StatusApplied
                        && ownerIndex == CombatEngine.PlayerIndex
                        && ctx.TargetIndex != CombatEngine.PlayerIndex
                        && ctx.Status == StatusId.Vulnerable
                        && ctx.Amount > 0
                        && engine.IsOwnersTurn(CombatEngine.PlayerIndex))
                    {
                        engine.DrawCards(status.Stacks);
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
