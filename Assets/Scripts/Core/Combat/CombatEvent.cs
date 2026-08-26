using STS.Core.Combat.Statuses;

namespace STS.Core.Combat
{
    public enum EventKind
    {
        CardDrawn,
        CardPlayed,
        CardDiscarded,
        CardExhausted,
        PileShuffled,
        DamageDealt,
        BlockGained,
        BlockCleared,
        HpLost,
        HpHealed,
        StatusChanged,
        EnergyChanged,
        TurnStarted,
        IntentShown,
        EnemyMoveStarted,
        EnemyDied,
        CardAddedToPile,
        ChoiceRequired,
        CombatEnded
    }

    /// <summary>
    /// 引擎輸出給 UI 的事件快照。扁平 struct、欄位帶滿播放所需資訊——UI 只照序播放,不回查引擎。
    /// SourceIndex/TargetIndex:-1 = 玩家,0 起 = 敵人索引。
    /// 各 Kind 的欄位語意:
    ///   DamageDealt: Amount=最終傷害, Amount2=被格擋量, HpLost=實際扣血, Remaining*=目標結算後
    ///   BlockGained: Amount=獲得量, RemainingBlock=結算後
    ///   StatusChanged: Amount=變化量, Amount2=變更後層數
    ///   EnergyChanged: Amount=目前能量, Amount2=上限
    ///   TurnStarted: SourceIndex=誰的回合, Amount=回合數
    ///   PileShuffled: Amount=洗入後抽牌堆張數
    /// </summary>
    public readonly struct CombatEvent
    {
        public readonly EventKind Kind;
        public readonly int SourceIndex;
        public readonly int TargetIndex;
        public readonly int Amount;
        public readonly int Amount2;
        public readonly int HpLost;
        public readonly int RemainingBlock;
        public readonly int RemainingHp;
        public readonly string CardId;
        public readonly int CardInstanceId;
        public readonly StatusId Status;

        public CombatEvent(
            EventKind kind,
            int sourceIndex = -1,
            int targetIndex = -1,
            int amount = 0,
            int amount2 = 0,
            int hpLost = 0,
            int remainingBlock = 0,
            int remainingHp = 0,
            string cardId = null,
            int cardInstanceId = 0,
            StatusId status = StatusId.None)
        {
            Kind = kind;
            SourceIndex = sourceIndex;
            TargetIndex = targetIndex;
            Amount = amount;
            Amount2 = amount2;
            HpLost = hpLost;
            RemainingBlock = remainingBlock;
            RemainingHp = remainingHp;
            CardId = cardId;
            CardInstanceId = cardInstanceId;
            Status = status;
        }
    }
}
