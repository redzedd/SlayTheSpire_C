using UnityEngine;
using STS.Core.Content;

namespace STS.Game.DataAssets
{
    /// <summary>全域平衡參數資產。單一真相在 balance.json;數值皆為近似重建,校正=改資料。</summary>
    [CreateAssetMenu(menuName = "STS/平衡參數", fileName = "Balance")]
    public sealed class BalanceAsset : ScriptableObject
    {
        [Header("起始配置")]
        [Tooltip("起始生命")] public int startHp = 80;
        [Tooltip("起始金幣")] public int startGold = 99;
        [Tooltip("起始卡組(卡 id 列表)")] public string[] startingDeckCardIds = System.Array.Empty<string>();
        [Tooltip("起始遺物 id 列表")] public string[] startingRelicIds = System.Array.Empty<string>();
        [Tooltip("起始藥水 id 列表")] public string[] startingPotionIds = System.Array.Empty<string>();

        [Header("商店定價")]
        [Tooltip("普通卡售價")] public int shopCardCommonCost = 50;
        [Tooltip("罕見卡售價")] public int shopCardUncommonCost = 75;
        [Tooltip("稀有卡售價")] public int shopCardRareCost = 150;
        [Tooltip("遺物售價")] public int shopRelicCost = 150;
        [Tooltip("藥水售價")] public int shopPotionCost = 50;

        [Header("商店貨架數量(直接決定版面)")]
        [Tooltip("上排職業牌張數")] public int shopClassCardCount = 5;
        [Tooltip("下左無色牌張數")] public int shopColorlessCardCount = 2;
        [Tooltip("遺物格數")] public int shopRelicCount = 3;
        [Tooltip("藥水格數")] public int shopPotionCount = 3;

        [Header("地圖節點型別權重與限制")]
        [Tooltip("戰鬥權重")] public int mapCombatWeight = 60;
        [Tooltip("精英權重")] public int mapEliteWeight = 16;
        [Tooltip("燈火權重")] public int mapRestWeight = 12;
        [Tooltip("商店權重")] public int mapShopWeight = 7;
        [Tooltip("寶箱權重")] public int mapTreasureWeight = 5;
        [Tooltip("精英最早出現列")] public int mapMinRowForElite = 5;
        [Tooltip("燈火最早出現列")] public int mapMinRowForRest = 5;
        [Tooltip("不放燈火的列(Boss 前一列)")] public int mapNoRestRow = 13;

        [Header("戰後金幣")]
        [Tooltip("普通戰金幣下限")] public int normalGoldMin = 10;
        [Tooltip("普通戰金幣上限")] public int normalGoldMax = 20;
        [Tooltip("精英戰金幣下限")] public int eliteGoldMin = 25;
        [Tooltip("精英戰金幣上限")] public int eliteGoldMax = 35;
        [Tooltip("Boss 戰金幣下限")] public int bossGoldMin = 95;
        [Tooltip("Boss 戰金幣上限")] public int bossGoldMax = 105;

        [Header("卡牌獎勵稀有度權重")]
        [Tooltip("普通卡權重")] public int cardRewardCommonWeight = 77;
        [Tooltip("罕見卡權重")] public int cardRewardUncommonWeight = 22;
        [Tooltip("稀有卡權重")] public int cardRewardRareWeight = 1;

        [Header("藥水掉落")]
        [Tooltip("基礎掉落率(%)")] public int potionDropBasePercent = 40;
        [Tooltip("未掉+/掉了- 的浮動(%)")] public int potionDropDeltaPercent = 10;

        [Header("商店")]
        [Tooltip("移除卡牌服務起價")] public int shopRemoveBaseCost = 75;
        [Tooltip("每次移除後漲價")] public int shopRemoveCostIncrement = 25;

        [Header("燈火")]
        [Tooltip("休息回血比例(%,以最大生命計)")] public int restHealPercent = 30;

        [Header("地圖")]
        [Tooltip("地圖欄數")] public int mapColumns = 7;
        [Tooltip("地圖列數")] public int mapRows = 15;
        [Tooltip("生成路徑條數")] public int mapPathCount = 6;

        [Header("遭遇池")]
        [Tooltip("前 N 場普通戰使用弱池")] public int weakPoolFightCount = 3;

        public BalanceDef ToDef()
        {
            return new BalanceDef
            {
                StartHp = startHp, StartGold = startGold,
                StartingDeckCardIds = startingDeckCardIds,
                StartingRelicIds = startingRelicIds,
                StartingPotionIds = startingPotionIds,
                ShopCardCommonCost = shopCardCommonCost,
                ShopCardUncommonCost = shopCardUncommonCost,
                ShopCardRareCost = shopCardRareCost,
                ShopRelicCost = shopRelicCost,
                ShopPotionCost = shopPotionCost,
                ShopClassCardCount = shopClassCardCount,
                ShopColorlessCardCount = shopColorlessCardCount,
                ShopRelicCount = shopRelicCount,
                ShopPotionCount = shopPotionCount,
                MapCombatWeight = mapCombatWeight,
                MapEliteWeight = mapEliteWeight,
                MapRestWeight = mapRestWeight,
                MapShopWeight = mapShopWeight,
                MapTreasureWeight = mapTreasureWeight,
                MapMinRowForElite = mapMinRowForElite,
                MapMinRowForRest = mapMinRowForRest,
                MapNoRestRow = mapNoRestRow,
                NormalGoldMin = normalGoldMin, NormalGoldMax = normalGoldMax,
                EliteGoldMin = eliteGoldMin, EliteGoldMax = eliteGoldMax,
                BossGoldMin = bossGoldMin, BossGoldMax = bossGoldMax,
                CardRewardCommonWeight = cardRewardCommonWeight,
                CardRewardUncommonWeight = cardRewardUncommonWeight,
                CardRewardRareWeight = cardRewardRareWeight,
                PotionDropBasePercent = potionDropBasePercent,
                PotionDropDeltaPercent = potionDropDeltaPercent,
                ShopRemoveBaseCost = shopRemoveBaseCost,
                ShopRemoveCostIncrement = shopRemoveCostIncrement,
                RestHealPercent = restHealPercent,
                MapColumns = mapColumns, MapRows = mapRows, MapPathCount = mapPathCount,
                WeakPoolFightCount = weakPoolFightCount
            };
        }
    }
}
