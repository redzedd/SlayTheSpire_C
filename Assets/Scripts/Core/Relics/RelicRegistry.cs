using STS.Core.Combat;
using STS.Core.Combat.Statuses;

namespace STS.Core.Relics
{
    /// <summary>
    /// 遺物行為註冊表(切片 10 顆):行為在程式、名稱/描述在資料層(M3)。
    /// 觸發順序由 HookBus 保證:遺物按獲得順序,先於所有狀態。
    /// 效果數值為 [近似] 重建,校正期集中修這裡的常數或搬進資料。
    /// </summary>
    internal static class RelicRegistry
    {
        internal static void OnHook(CombatEngine engine, RelicInstance relic, in HookContext ctx)
        {
            switch (relic.Id)
            {
                case RelicIds.BurningBlood:
                    if (ctx.Point == HookPoint.CombatVictory)
                    {
                        engine.HealHp(CombatEngine.PlayerIndex, 6);
                    }
                    break;

                case RelicIds.Anchor:
                    if (ctx.Point == HookPoint.CombatStart)
                    {
                        engine.GainBlock(CombatEngine.PlayerIndex, 10, BlockSource.Other);
                    }
                    break;

                case RelicIds.Vajra:
                    if (ctx.Point == HookPoint.CombatStart)
                    {
                        engine.ApplyStatusTo(CombatEngine.PlayerIndex, StatusId.Strength, 1);
                    }
                    break;

                case RelicIds.BagOfMarbles:
                    if (ctx.Point == HookPoint.CombatStart)
                    {
                        for (int i = 0; i < engine.State.Enemies.Count; i++)
                        {
                            if (engine.State.Enemies[i].IsAlive)
                            {
                                engine.ApplyStatusTo(i, StatusId.Vulnerable, 1);
                            }
                        }
                    }
                    break;

                case RelicIds.BagOfPreparation:
                    if (ctx.Point == HookPoint.CombatStart)
                    {
                        engine.DrawCards(2);
                    }
                    break;

                case RelicIds.BloodVial:
                    if (ctx.Point == HookPoint.CombatStart)
                    {
                        engine.HealHp(CombatEngine.PlayerIndex, 2);
                    }
                    break;

                case RelicIds.Lantern:
                    if (ctx.Point == HookPoint.CombatStart)
                    {
                        engine.GainEnergy(1);
                    }
                    break;

                case RelicIds.Orichalcum:
                    if (ctx.Point == HookPoint.PlayerTurnEnd && engine.State.Player.Block == 0)
                    {
                        engine.GainBlock(CombatEngine.PlayerIndex, 6, BlockSource.Other);
                    }
                    break;

                case RelicIds.Nunchaku:
                    if (ctx.Point == HookPoint.CardPlayed
                        && ctx.SourceIndex == CombatEngine.PlayerIndex
                        && ctx.CardType == Cards.CardType.Attack)
                    {
                        relic.Counter++;
                        if (relic.Counter >= 10)
                        {
                            relic.Counter = 0;
                            engine.GainEnergy(1);
                        }
                    }
                    break;

                case RelicIds.BronzeScales:
                    if (ctx.Point == HookPoint.AttackReceived
                        && ctx.TargetIndex == CombatEngine.PlayerIndex
                        && ctx.SourceIndex != CombatEngine.PlayerIndex
                        && ctx.SourceIndex != HookContext.NoIndex)
                    {
                        engine.DealNonAttackDamage(CombatEngine.PlayerIndex, ctx.SourceIndex, 3);
                    }
                    break;

                default:
                    // 未知遺物 id:資料層(M3)驗證會抓;此處不拋錯避免壞資料癱瘓整場戰鬥
                    break;
            }
        }
    }
}
