namespace STS.Core.Cards
{
    public enum CardType
    {
        Attack,
        Skill,
        Power,
        Status,
        Curse
    }

    public enum CardRarity
    {
        Starter,
        Common,
        Uncommon,
        Rare
    }

    /// <summary>卡牌自身的費用縮放規則(全域性的折扣走狀態,這裡只放「這張卡自己的算法」)。</summary>
    public enum CostScaling
    {
        None,
        /// <summary>本回合每打出過一張攻擊牌就少 1 費(踩踏)。</summary>
        MinusPerAttackPlayedThisTurn
    }

    /// <summary>
    /// 卡牌定義。升級版是另一個獨立實體(id 慣例:「strike」/「strike+」),不做欄位差分——最笨最穩。
    /// </summary>
    public sealed class CardDef
    {
        public string Id;
        public string Name;
        /// <summary>描述模板,含 {dmg}/{blk} 佔位符,UI 代入計算後的即時值。</summary>
        public string DescriptionTemplate;
        public CardType Type;
        public CardRarity Rarity;
        public int Cost;
        public bool CostIsX;
        public CostScaling CostScaling;
        /// <summary>能量與目標之外的可打出條件(契約終結)。</summary>
        public PlayCondition PlayCondition;
        /// <summary>PlayCondition 的門檻值。</summary>
        public int PlayConditionAmount;
        public bool Unplayable;
        public bool Exhausts;
        public bool Ethereal;
        /// <summary>無色牌:不屬於任何職業,只在商店的無色區與特殊事件出現,不進戰後卡牌獎勵池。</summary>
        public bool Colorless;
        public EffectStep[] Steps = System.Array.Empty<EffectStep>();
        /// <summary>回合結束時仍在手牌才觸發的步驟(燒傷型狀態卡用)。</summary>
        public EffectStep[] TurnEndInHandSteps = System.Array.Empty<EffectStep>();
    }
}
