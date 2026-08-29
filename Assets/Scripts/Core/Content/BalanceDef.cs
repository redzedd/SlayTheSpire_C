namespace STS.Core.Content
{
    /// <summary>
    /// 全域平衡參數。全部從 JSON 灌入,程式不寫死數值——校正 = 改資料。
    /// 數值皆為 [近似] 重建,M5(Run 層)開始消費,M7 校正。
    /// </summary>
    public sealed class BalanceDef
    {
        // 起始配置
        public int StartHp = 80;
        public int StartGold = 99;
        public string[] StartingDeckCardIds = System.Array.Empty<string>();
        public string[] StartingRelicIds = System.Array.Empty<string>();
        public string[] StartingPotionIds = System.Array.Empty<string>();

        // 戰後金幣
        public int NormalGoldMin = 10;
        public int NormalGoldMax = 20;
        public int EliteGoldMin = 25;
        public int EliteGoldMax = 35;
        public int BossGoldMin = 95;
        public int BossGoldMax = 105;

        // 卡牌獎勵稀有度權重
        public int CardRewardCommonWeight = 77;
        public int CardRewardUncommonWeight = 22;
        public int CardRewardRareWeight = 1;

        // 藥水掉落
        public int PotionDropBasePercent = 40;
        public int PotionDropDeltaPercent = 10;

        // 商店
        public int ShopRemoveBaseCost = 75;
        public int ShopRemoveCostIncrement = 25;
        public int ShopCardCommonCost = 50;
        public int ShopCardUncommonCost = 75;
        public int ShopCardRareCost = 150;
        public int ShopRelicCost = 150;
        public int ShopPotionCost = 50;
        // 商店貨架數量(版面直接照這幾個數字排:上排職業牌、下左無色牌、中下遺物/藥水)
        public int ShopClassCardCount = 5;
        public int ShopColorlessCardCount = 2;
        public int ShopRelicCount = 3;
        public int ShopPotionCount = 3;

        // 燈火
        public int RestHealPercent = 30;

        // 地圖結構
        public int MapColumns = 7;
        public int MapRows = 15;
        public int MapPathCount = 6;

        // 地圖節點型別權重(固定列以外)與限制
        public int MapCombatWeight = 60;
        public int MapEliteWeight = 16;
        public int MapRestWeight = 12;
        public int MapShopWeight = 7;
        public int MapTreasureWeight = 5;
        public int MapMinRowForElite = 5;
        public int MapMinRowForRest = 5;
        public int MapNoRestRow = 13;

        // 遭遇池
        public int WeakPoolFightCount = 3;
    }
}
