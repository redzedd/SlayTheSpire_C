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
        /// <summary>從手牌選 Amount 張,在本場戰鬥內升級它們(武裝型)。</summary>
        ChooseUpgradeInHand,
        /// <summary>把手上每一張能升的牌都在本場戰鬥內升級(武裝+型)。</summary>
        UpgradeAllInHand,
        /// <summary>從棄牌堆選 Amount 張放到抽牌堆頂(頭槌型)。</summary>
        ChooseFromDiscardToDrawTop,
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
        /// <summary>永久提高最大生命並回滿同等點數(狂宴型);會寫回 run 狀態。</summary>
        GainMaxHp,
        /// <summary>把 Amount 點傷害永久加到「正在打出的這張牌」上,只在本場戰鬥有效(暴走型)。</summary>
        GrowThisCardDamage,
        /// <summary>消耗手上一張隨機攻擊牌,把它的傷害加到正在打出的這張牌上(痛毆型)。</summary>
        AbsorbRandomAttackFromHand,
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
        PerLastExhausted,
        /// <summary>手牌中每有一張攻擊牌(躍躍欲試)。</summary>
        PerAttackInHand
    }

    /// <summary>
    /// 單一步驟的執行條件。不成立就整步跳過,後續步驟照跑——
    /// 「有條件才多打一段」用兩個 Damage 步驟表達,不需要另一套條件式段數機制。
    /// </summary>
    public enum StepCondition
    {
        None,
        /// <summary>目標身上有易傷(拆卸)。</summary>
        TargetIsVulnerable,
        /// <summary>你本回合失去過生命(怨恨)。</summary>
        LostHpThisTurn,
        /// <summary>這張牌前一段攻擊剛好把目標打死(狂宴)。</summary>
        LastAttackKilled,
        /// <summary>你本回合消耗過牌(邪眼/被遺忘的儀式)。</summary>
        ExhaustedThisTurn
    }

    /// <summary>卡片能不能打出的額外條件(能量與目標之外的)。</summary>
    public enum PlayCondition
    {
        None,
        /// <summary>消耗堆至少有 N 張牌(契約終結)。</summary>
        ExhaustPileAtLeast
    }

    /// <summary>Repeat(段數)的解讀方式。</summary>
    public enum RepeatKind
    {
        /// <summary>就是 Repeat 欄位的數字。</summary>
        Fixed,
        /// <summary>段數 = 這張 X 費卡消耗掉的能量(旋風斬型)。</summary>
        XEnergy,
        /// <summary>段數 = Repeat + 本場戰鬥你失去生命的次數(扯碎型)。</summary>
        PerHpLossThisCombat
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
        public readonly RepeatKind RepeatKind;
        public readonly StepCondition Condition;
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
            RepeatKind repeatKind = RepeatKind.Fixed,
            StatusId status = StatusId.None,
            string cardId = null,
            PileType pile = PileType.Discard,
            string customId = null,
            StepCondition condition = StepCondition.None)
        {
            Condition = condition;
            Op = op;
            Target = target;
            Amount = amount;
            AmountKind = amountKind;
            SecondaryAmount = secondaryAmount;
            Repeat = repeat;
            RepeatKind = repeatKind;
            Status = status;
            CardId = cardId;
            Pile = pile;
            CustomId = customId;
        }
    }
}
