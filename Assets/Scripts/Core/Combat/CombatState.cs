using System.Collections.Generic;
using STS.Core.Cards;

namespace STS.Core.Combat
{
    public enum CombatPhase
    {
        NotStarted,
        PlayerTurn,
        AwaitingChoice,
        EnemyTurn,
        Victory,
        Defeat
    }

    /// <summary>戰鬥的全部可變狀態。純資料聚合,禁止藏引擎邏輯或 UI 參照。</summary>
    public sealed class CombatState
    {
        public const int HandLimit = 10;

        public CombatPhase Phase = CombatPhase.NotStarted;
        /// <summary>第一個玩家回合 = 1。</summary>
        public int TurnNumber;
        public int Energy;
        public int MaxEnergy = 3;
        public CombatantState Player;
        public readonly List<CombatantState> Enemies = new List<CombatantState>();
        public readonly List<CardInstance> DrawPile = new List<CardInstance>();
        public readonly List<CardInstance> Hand = new List<CardInstance>();
        public readonly List<CardInstance> DiscardPile = new List<CardInstance>();
        public readonly List<CardInstance> ExhaustPile = new List<CardInstance>();
    }
}
