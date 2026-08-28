using STS.Core.Cards;

namespace STS.Core.Combat
{
    /// <summary>狀態與遺物共用的觸發時機。</summary>
    public enum HookPoint
    {
        CombatStart,
        PlayerTurnStart,
        PlayerTurnEnd,
        EnemyTurnStart,
        EnemyTurnEnd,
        CardPlayed,
        AttackDealt,
        AttackReceived,
        HpLost,
        /// <summary>一張牌被移入消耗堆(無懼疼痛/黑暗之擁)。</summary>
        CardExhausted,
        /// <summary>有人獲得格擋(勢不可當);Amount = 實際獲得量。</summary>
        BlockGained,
        Shuffled,
        CombatVictory,
        EnemyDied
    }

    /// <summary>
    /// hook 觸發時的事件快照。SourceIndex/TargetIndex:-1 = 玩家,0 起 = 敵人,NoIndex = 不適用。
    /// hook 只做「反應」(加狀態/格擋/反傷);傷害與格擋的修飾固定住在 CombatMath/BlockMath,
    /// 不准讓 hook 變成第二套傷害管線。
    /// </summary>
    public readonly struct HookContext
    {
        public const int NoIndex = -2;

        public readonly HookPoint Point;
        /// <summary>事件主體:出牌者/攻擊者/回合所屬者。</summary>
        public readonly int SourceIndex;
        /// <summary>受擊者等;不適用時為 NoIndex。</summary>
        public readonly int TargetIndex;
        /// <summary>CardPlayed 時的卡牌類型。</summary>
        public readonly CardType CardType;
        /// <summary>附帶數值(受擊 hpLost 等)。</summary>
        public readonly int Amount;

        public HookContext(HookPoint point, int sourceIndex = NoIndex, int targetIndex = NoIndex,
            CardType cardType = CardType.Attack, int amount = 0)
        {
            Point = point;
            SourceIndex = sourceIndex;
            TargetIndex = targetIndex;
            CardType = cardType;
            Amount = amount;
        }
    }
}
