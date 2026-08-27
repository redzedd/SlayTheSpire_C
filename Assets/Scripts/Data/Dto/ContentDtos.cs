using System.Collections.Generic;

namespace STS.Data.Dto
{
    // JSON 的 1:1 映射層。欄位名即 JSON 鍵名(小駝峰);列舉一律字串,由 ContentParser 驗證轉換。
    // 這層不做任何邏輯,只承接資料;預設值與 JSON 省略欄位的語意在這裡定義。

    public sealed class StepDto
    {
        public string op;
        public string target = "Self";
        public int amount;
        public string amountKind = "Fixed";
        public int secondaryAmount;
        public int repeat;
        public bool repeatIsX;
        public string status = "None";
        public string cardId;
        public string pile = "Discard";
        public string customId;
    }

    public sealed class CardVariantDto
    {
        public string name;
        public int cost;
        public string description;
        public List<StepDto> steps = new List<StepDto>();
        public List<StepDto> turnEndInHandSteps = new List<StepDto>();
    }

    public sealed class CardDto
    {
        public string id;
        public string type = "Attack";
        public string rarity = "Common";
        public bool costIsX;
        public bool unplayable;
        public bool exhausts;
        public bool ethereal;
        public CardVariantDto @base;
        /// <summary>null = 此卡無升級版(狀態卡)。</summary>
        public CardVariantDto upgrade;
    }

    public sealed class MoveDto
    {
        public string id;
        public string name;
        public string intent = "Special";
        public int weight = 1;
        public int maxConsecutive;
        public List<StepDto> steps = new List<StepDto>();
    }

    public sealed class StatusStackDto
    {
        public string status;
        public int stacks;
    }

    public sealed class EnemyDto
    {
        public string id;
        public string name;
        public int hpMin;
        public int hpMax;
        public string ai = "Loop";
        public List<MoveDto> moves = new List<MoveDto>();
        public List<string> openingScript = new List<string>();
        public List<string> loopScript = new List<string>();
        public List<StatusStackDto> initialStatuses = new List<StatusStackDto>();
    }

    public sealed class RelicDto
    {
        public string id;
        public string name;
        public string rarity = "Common";
        public string description;
    }

    public sealed class PotionDto
    {
        public string id;
        public string name;
        public string description;
        public bool needsTarget;
        public List<StepDto> steps = new List<StepDto>();
    }

    public sealed class StatusDto
    {
        public string id;
        public string name;
        /// <summary>可含 {n} 佔位符,顯示時代入層數。</summary>
        public string description;
    }

    public sealed class EncounterDto
    {
        public string id;
        public string pool = "Normal";
        public int weight = 1;
        public List<string> enemyIds = new List<string>();
    }

    public sealed class CardsFileDto { public List<CardDto> cards = new List<CardDto>(); }
    public sealed class EnemiesFileDto { public List<EnemyDto> enemies = new List<EnemyDto>(); }
    public sealed class RelicsFileDto { public List<RelicDto> relics = new List<RelicDto>(); }
    public sealed class PotionsFileDto { public List<PotionDto> potions = new List<PotionDto>(); }
    public sealed class EncountersFileDto { public List<EncounterDto> encounters = new List<EncounterDto>(); }
    public sealed class StatusesFileDto { public List<StatusDto> statuses = new List<StatusDto>(); }

    public sealed class BalanceDto
    {
        public int startHp = 80;
        public int startGold = 99;
        public List<string> startingDeckCardIds = new List<string>();
        public List<string> startingRelicIds = new List<string>();
        public List<string> startingPotionIds = new List<string>();
        public int shopCardCommonCost = 50;
        public int shopCardUncommonCost = 75;
        public int shopCardRareCost = 150;
        public int shopRelicCost = 150;
        public int shopPotionCost = 50;
        public int mapCombatWeight = 60;
        public int mapEliteWeight = 16;
        public int mapRestWeight = 12;
        public int mapShopWeight = 7;
        public int mapTreasureWeight = 5;
        public int mapMinRowForElite = 5;
        public int mapMinRowForRest = 5;
        public int mapNoRestRow = 13;
        public int normalGoldMin = 10;
        public int normalGoldMax = 20;
        public int eliteGoldMin = 25;
        public int eliteGoldMax = 35;
        public int bossGoldMin = 95;
        public int bossGoldMax = 105;
        public int cardRewardCommonWeight = 77;
        public int cardRewardUncommonWeight = 22;
        public int cardRewardRareWeight = 1;
        public int potionDropBasePercent = 40;
        public int potionDropDeltaPercent = 10;
        public int shopRemoveBaseCost = 75;
        public int shopRemoveCostIncrement = 25;
        public int restHealPercent = 30;
        public int mapColumns = 7;
        public int mapRows = 15;
        public int mapPathCount = 6;
        public int weakPoolFightCount = 3;
    }

    /// <summary>七份 JSON 的原始承接(匯入器直接用它灌 SO)。</summary>
    public sealed class RawContent
    {
        public CardsFileDto Cards;
        public EnemiesFileDto Enemies;
        public RelicsFileDto Relics;
        public PotionsFileDto Potions;
        public EncountersFileDto Encounters;
        public StatusesFileDto Statuses;
        public BalanceDto Balance;
    }
}
