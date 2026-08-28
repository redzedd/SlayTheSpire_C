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
        /// <summary>消耗抽牌堆最上面 Amount 張(餘燼型)。</summary>
        ExhaustTopOfDraw,
        /// <summary>消耗整手牌(惡魔之焰型);消耗張數記進 LastExhaustedCount 供後續步驟取用。</summary>
        ExhaustHand,
        /// <summary>消耗手牌中所有非攻擊牌(重振精神型);同樣記進 LastExhaustedCount。</summary>
        ExhaustNonAttacksInHand,
        /// <summary>棄掉整手牌,然後抽等量的牌(添柴型)。</summary>
        DiscardHandDrawSame,
        /// <summary>持續抽牌直到抽到一張非攻擊牌(劫掠型)。</summary>
        DrawUntilNonAttack,
        /// <summary>
        /// 自動打出抽牌堆頂部 Amount 張牌(破滅/傾瀉型),不花能量。
        /// Pile 決定打完後去哪:Exhaust = 打完消耗,其餘 = 進棄牌堆。
        /// </summary>
        PlayTopOfDraw,
        /// <summary>把目標身上某個狀態的層數翻倍(熔融之拳型)。</summary>
        DoubleStatus,
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

    /// <summary>
    /// Amount 欄位的解讀方式。
    /// 帶「PerXxx」的四種一律是 <c>Amount + SecondaryAmount × 數量</c>——
    /// 基礎值放 amount、每單位加成放 secondaryAmount,四張成長型卡共用同一條公式。
    /// </summary>
    public enum AmountKind
    {
        Fixed,
        XEnergy,
        CurrentBlock,
        StrengthTimes,
        /// <summary>每有一張牌在消耗堆(灰燼打擊)。</summary>
        PerExhaustedCard,
        /// <summary>目標身上每一層易傷(欺凌/主宰)。</summary>
        PerTargetVulnerable,
        /// <summary>本回合每打出過一張攻擊牌(焚燒)。</summary>
        PerAttackPlayedThisTurn,
        /// <summary>牌組中每有一張名字含「打擊」的牌(完美打擊)。</summary>
        PerStrikeCard,
        /// <summary>同一張牌前一個步驟剛消耗掉的張數(重振精神/惡魔之焰)。</summary>
        PerLastExhausted
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
