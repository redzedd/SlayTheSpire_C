using STS.Core.Combat.Statuses;

namespace STS.Core.Cards
{
    /// <summary>效果操作的種類。切片範圍外的 op 由 EffectResolver 明確拋錯,不靜默忽略。</summary>
    public enum EffectOp
    {
        Damage,
        Block,
        ApplyStatus,
        Draw,
        GainEnergy,
        LoseHp,
        Heal,
        AddCardToPile,
        ExhaustRandomFromHand,
        ChooseExhaustFromHand,
        Custom
    }

    /// <summary>效果的目標。語意相對於施放者:敵人施放時 TargetEnemy 指玩家。</summary>
    public enum EffectTarget
    {
        Self,
        TargetEnemy,
        AllEnemies,
        RandomEnemy
    }

    /// <summary>Amount 欄位的解讀方式。</summary>
    public enum AmountKind
    {
        Fixed,
        XEnergy,
        CurrentBlock,
        StrengthTimes
    }

    public enum PileType
    {
        Draw,
        Hand,
        Discard,
        Exhaust
    }

    /// <summary>
    /// 卡牌/藥水/敵招的單一效果步驟。純資料,直接對應 JSON;行為由 EffectResolver 解讀。
    /// </summary>
    public readonly struct EffectStep
    {
        public readonly EffectOp Op;
        public readonly EffectTarget Target;
        public readonly int Amount;
        public readonly AmountKind AmountKind;
        public readonly int SecondaryAmount;
        /// <summary>段數;0 或 1 = 單次。</summary>
        public readonly int Repeat;
        /// <summary>段數 = 消耗的能量(旋風斬型)。</summary>
        public readonly bool RepeatIsX;
        public readonly StatusId Status;
        public readonly string CardId;
        public readonly PileType Pile;
        public readonly string CustomId;

        public EffectStep(
            EffectOp op,
            EffectTarget target,
            int amount = 0,
            AmountKind amountKind = AmountKind.Fixed,
            int secondaryAmount = 0,
            int repeat = 0,
            bool repeatIsX = false,
            StatusId status = StatusId.None,
            string cardId = null,
            PileType pile = PileType.Discard,
            string customId = null)
        {
            Op = op;
            Target = target;
            Amount = amount;
            AmountKind = amountKind;
            SecondaryAmount = secondaryAmount;
            Repeat = repeat;
            RepeatIsX = repeatIsX;
            Status = status;
            CardId = cardId;
            Pile = pile;
            CustomId = customId;
        }
    }
}
