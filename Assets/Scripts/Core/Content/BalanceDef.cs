namespace STS.Core.Content
{
    /// <summary>
    /// 全域平衡參數。全部從 JSON 灌入,程式不寫死數值——校正 = 改資料。
    /// 數值皆為 [近似] 重建,M5(Run 層)開始消費,M7 校正。
    /// </summary>
    public sealed class BalanceDef
    {
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

        // 燈火
        public int RestHealPercent = 30;

        // 地圖
        public int MapColumns = 7;
        public int MapRows = 15;
        public int MapPathCount = 6;

        // 遭遇池
        public int WeakPoolFightCount = 3;
    }
}
