namespace STS.Core.Relics
{
    /// <summary>
    /// 遺物 id 的唯一權威清單。資料層(JSON)與行為層(RelicRegistry)都以此對表;
    /// 內容驗證測試靠它抓「資料有 id 但沒有行為」的斷線。
    /// </summary>
    public static class RelicIds
    {
        public const string BurningBlood = "burning_blood";
        public const string Anchor = "anchor";
        public const string Vajra = "vajra";
        public const string BagOfMarbles = "bag_of_marbles";
        public const string BagOfPreparation = "bag_of_preparation";
        public const string BloodVial = "blood_vial";
        public const string Lantern = "lantern";
        public const string Orichalcum = "orichalcum";
        public const string Nunchaku = "nunchaku";
        public const string BronzeScales = "bronze_scales";

        /// <summary>已有行為實作的全部遺物 id。</summary>
        public static readonly string[] All =
        {
            BurningBlood, Anchor, Vajra, BagOfMarbles, BagOfPreparation,
            BloodVial, Lantern, Orichalcum, Nunchaku, BronzeScales
        };
    }
}
