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
        public bool Unplayable;
        public bool Exhausts;
        public bool Ethereal;
        public EffectStep[] Steps = System.Array.Empty<EffectStep>();
        /// <summary>回合結束時仍在手牌才觸發的步驟(燒傷型狀態卡用)。</summary>
        public EffectStep[] TurnEndInHandSteps = System.Array.Empty<EffectStep>();
    }
}
