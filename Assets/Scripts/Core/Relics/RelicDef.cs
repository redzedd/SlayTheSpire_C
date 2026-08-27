namespace STS.Core.Relics
{
    public enum RelicRarity
    {
        Starter,
        Common,
        Uncommon,
        Rare,
        Boss
    }

    /// <summary>遺物的資料面定義(名稱/稀有度/描述,供 UI 與獎勵池);行為住在 RelicRegistry。</summary>
    public sealed class RelicDef
    {
        public string Id;
        public string Name;
        public RelicRarity Rarity;
        public string Description;
    }
}
