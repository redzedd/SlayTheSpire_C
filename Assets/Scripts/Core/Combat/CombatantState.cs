using System.Collections.Generic;
using STS.Core.Combat.Statuses;

namespace STS.Core.Combat
{
    /// <summary>單一狀態效果的實體。JustApplied:同回合剛施加,首次衰減跳過(M2 使用)。</summary>
    public sealed class StatusInstance
    {
        public StatusId Id;
        public int Stacks;
        public bool JustApplied;
    }

    /// <summary>
    /// 戰鬥單位(玩家或敵人)的執行期狀態。
    /// Statuses 用 List 保插加順序——hook 觸發順序的決定性依賴它,不能換成 Dictionary。
    /// </summary>
    public sealed class CombatantState
    {
        public string Name;
        public int Hp;
        public int MaxHp;
        public int Block;
        public readonly List<StatusInstance> Statuses = new List<StatusInstance>();

        public bool IsAlive => Hp > 0;

        public int GetStatus(StatusId id)
        {
            for (int i = 0; i < Statuses.Count; i++)
            {
                if (Statuses[i].Id == id) return Statuses[i].Stacks;
            }
            return 0;
        }

        public StatusInstance GetStatusInstance(StatusId id)
        {
            for (int i = 0; i < Statuses.Count; i++)
            {
                if (Statuses[i].Id == id) return Statuses[i];
            }
            return null;
        }

        /// <summary>增減狀態層數;歸零(含以下)即移除。回傳變更後層數。</summary>
        public int ModifyStatus(StatusId id, int delta)
        {
            for (int i = 0; i < Statuses.Count; i++)
            {
                if (Statuses[i].Id != id) continue;
                Statuses[i].Stacks += delta;
                if (Statuses[i].Stacks <= 0)
                {
                    Statuses.RemoveAt(i);
                    return 0;
                }
                return Statuses[i].Stacks;
            }
            if (delta <= 0) return 0;
            Statuses.Add(new StatusInstance { Id = id, Stacks = delta, JustApplied = true });
            return delta;
        }
    }
}
