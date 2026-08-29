namespace STS.Core.Combat
{
    /// <summary>
    /// 這次格擋是誰給的。堅定不移只翻倍「來自卡牌」的格擋,所以 GainBlock 必須帶著來源,
    /// 而且刻意沒有預設值——新增獲得格擋的地方一定要表態,不能默默走成卡牌來源。
    /// </summary>
    internal enum BlockSource
    {
        /// <summary>玩家打出的牌(含自動打出與重複生效)給的格擋。</summary>
        Card,
        /// <summary>敵人招式給自己的格擋。</summary>
        EnemyMove,
        /// <summary>狀態、遺物、延後結算等其餘來源(金屬化、覆甲、青銅鱗片、捲曲……)。</summary>
        Other,
    }
}
