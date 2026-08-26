using System.Collections.Generic;
using STS.Core.Combat.Statuses;
using STS.Core.Relics;

namespace STS.Core.Combat
{
    /// <summary>
    /// hook 派發匯流排。觸發順序是決定性的鐵律:
    /// 玩家遺物(獲得順序)→ 玩家狀態(施加順序)→ 各敵人狀態(敵序×施加順序)。
    /// 每層先快照再迭代:handler 可能增刪狀態(捲曲自移除、儀式加力),不能邊走邊改原清單。
    /// </summary>
    internal static class HookBus
    {
        internal static void Fire(CombatEngine engine, in HookContext ctx)
        {
            var relics = engine.Relics;
            if (relics.Count > 0)
            {
                var relicSnapshot = new RelicInstance[relics.Count];
                relics.CopyTo(relicSnapshot);
                for (int i = 0; i < relicSnapshot.Length; i++)
                {
                    RelicRegistry.OnHook(engine, relicSnapshot[i], ctx);
                }
            }

            FireStatuses(engine, CombatEngine.PlayerIndex, ctx);
            for (int i = 0; i < engine.State.Enemies.Count; i++)
            {
                if (engine.State.Enemies[i].IsAlive)
                {
                    FireStatuses(engine, i, ctx);
                }
            }
        }

        private static void FireStatuses(CombatEngine engine, int ownerIndex, in HookContext ctx)
        {
            var statuses = engine.GetCombatant(ownerIndex).Statuses;
            if (statuses.Count == 0) return;

            var snapshot = new List<StatusInstance>(statuses);
            for (int i = 0; i < snapshot.Count; i++)
            {
                // handler 執行中可能已被移除(捲曲):以層數為準,拿到殭屍實體就跳過
                if (snapshot[i].Stacks <= 0) continue;
                StatusRegistry.OnHook(engine, ownerIndex, snapshot[i], ctx);
            }
        }
    }
}
