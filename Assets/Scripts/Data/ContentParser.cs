using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using STS.Core.Cards;
using STS.Core.Combat.Enemies;
using STS.Core.Combat.Statuses;
using STS.Core.Content;
using STS.Core.Potions;
using STS.Core.Relics;
using STS.Data.Dto;

namespace STS.Data
{
    /// <summary>內容驗證失敗——訊息一定帶出處(哪份檔、哪個 id),讓錯誤可以直接定位。</summary>
    public sealed class ContentValidationException : Exception
    {
        public ContentValidationException(string message) : base(message) { }
    }

    /// <summary>解析+驗證完成的全部內容定義(引擎可直接消費)。</summary>
    public sealed class ParsedContent
    {
        public readonly List<CardDef> Cards = new List<CardDef>();
        public readonly List<EnemyDef> Enemies = new List<EnemyDef>();
        public readonly List<RelicDef> Relics = new List<RelicDef>();
        public readonly List<PotionDef> Potions = new List<PotionDef>();
        public readonly List<EncounterDef> Encounters = new List<EncounterDef>();
        public readonly List<StatusDef> Statuses = new List<StatusDef>();
        public BalanceDef Balance = new BalanceDef();
    }

    /// <summary>
    /// JSON(單一真相)→ DTO → Core def 的解析與驗證。
    /// 嚴格 schema:未知欄位直接報錯(MissingMemberHandling.Error),打字錯誤當場抓。
    /// </summary>
    public static class ContentParser
    {
        private static readonly JsonSerializerSettings Strict = new JsonSerializerSettings
        {
            MissingMemberHandling = MissingMemberHandling.Error
        };

        public static RawContent ParseRaw(string cardsJson, string enemiesJson, string relicsJson,
            string potionsJson, string encountersJson, string balanceJson, string statusesJson = null)
        {
            return new RawContent
            {
                Cards = Deserialize<CardsFileDto>(cardsJson, "cards.json"),
                Enemies = Deserialize<EnemiesFileDto>(enemiesJson, "enemies.json"),
                Relics = Deserialize<RelicsFileDto>(relicsJson, "relics.json"),
                Potions = Deserialize<PotionsFileDto>(potionsJson, "potions.json"),
                Encounters = Deserialize<EncountersFileDto>(encountersJson, "encounters.json"),
                Statuses = string.IsNullOrEmpty(statusesJson)
                    ? new StatusesFileDto()
                    : Deserialize<StatusesFileDto>(statusesJson, "statuses.json"),
                Balance = Deserialize<BalanceDto>(balanceJson, "balance.json")
            };
        }

        private static T Deserialize<T>(string json, string sourceName)
        {
            try
            {
                var result = JsonConvert.DeserializeObject<T>(json, Strict);
                if (result == null) throw new ContentValidationException($"{sourceName}:內容為空");
                return result;
            }
            catch (JsonException e)
            {
                throw new ContentValidationException($"{sourceName}:JSON 解析失敗——{e.Message}");
            }
        }

        public static ParsedContent BuildDefs(RawContent raw)
        {
            var content = new ParsedContent();
            var cardIds = new HashSet<string>();

            // ---- 卡牌:base 與 upgrade 各生成一個獨立 CardDef(id 與 id+) ----
            foreach (var dto in raw.Cards.cards)
            {
                Require(!string.IsNullOrEmpty(dto.id), "cards.json:有卡缺 id");
                Require(cardIds.Add(dto.id), $"cards.json:卡 id 重複——{dto.id}");
                Require(dto.@base != null, $"cards.json:{dto.id} 缺 base 組");

                content.Cards.Add(BuildCard(dto, dto.@base, dto.id));
                if (dto.upgrade != null)
                {
                    content.Cards.Add(BuildCard(dto, dto.upgrade, dto.id + "+"));
                }
            }

            // ---- 卡牌步驟的跨卡參照(憤怒複製/火祭塞燒傷)在全卡就緒後驗 ----
            foreach (var card in content.Cards)
            {
                ValidateSteps(card.Steps, $"cards.json:{card.Id}", cardIds, card.CostIsX);
                ValidateSteps(card.TurnEndInHandSteps, $"cards.json:{card.Id}(turnEnd)", cardIds, false);
            }

            // ---- 敵人 ----
            var enemyIds = new HashSet<string>();
            foreach (var dto in raw.Enemies.enemies)
            {
                Require(!string.IsNullOrEmpty(dto.id), "enemies.json:有敵人缺 id");
                Require(enemyIds.Add(dto.id), $"enemies.json:敵人 id 重複——{dto.id}");
                Require(dto.hpMin > 0 && dto.hpMax >= dto.hpMin, $"enemies.json:{dto.id} 血量範圍無效");
                Require(dto.moves.Count > 0, $"enemies.json:{dto.id} 沒有任何招式");

                var moves = new MoveDef[dto.moves.Count];
                var moveIds = new HashSet<string>();
                for (int i = 0; i < dto.moves.Count; i++)
                {
                    var m = dto.moves[i];
                    Require(!string.IsNullOrEmpty(m.id), $"enemies.json:{dto.id} 有招式缺 id");
                    Require(moveIds.Add(m.id), $"enemies.json:{dto.id} 招式 id 重複——{m.id}");
                    var steps = BuildSteps(m.steps, $"enemies.json:{dto.id}.{m.id}");
                    ValidateSteps(steps, $"enemies.json:{dto.id}.{m.id}", cardIds, false);
                    moves[i] = new MoveDef
                    {
                        Id = m.id,
                        Name = m.name,
                        Intent = ParseEnum<IntentType>(m.intent, $"enemies.json:{dto.id}.{m.id}.intent"),
                        Weight = m.weight,
                        MaxConsecutive = m.maxConsecutive,
                        Steps = steps
                    };
                }
                foreach (var refId in dto.openingScript)
                {
                    Require(moveIds.Contains(refId), $"enemies.json:{dto.id} openingScript 參照不存在的招式——{refId}");
                }
                foreach (var refId in dto.loopScript)
                {
                    Require(moveIds.Contains(refId), $"enemies.json:{dto.id} loopScript 參照不存在的招式——{refId}");
                }
                var initial = new StatusStack[dto.initialStatuses.Count];
                for (int i = 0; i < dto.initialStatuses.Count; i++)
                {
                    initial[i] = new StatusStack(
                        ParseEnum<StatusId>(dto.initialStatuses[i].status, $"enemies.json:{dto.id}.initialStatuses"),
                        dto.initialStatuses[i].stacks);
                }
                content.Enemies.Add(new EnemyDef
                {
                    Id = dto.id,
                    Name = dto.name,
                    HpMin = dto.hpMin,
                    HpMax = dto.hpMax,
                    Moves = moves,
                    OpeningScript = dto.openingScript.ToArray(),
                    LoopScript = dto.loopScript.ToArray(),
                    Ai = ParseEnum<AiKind>(dto.ai, $"enemies.json:{dto.id}.ai"),
                    InitialStatuses = initial
                });
            }

            // ---- 遺物:資料層 id 必須有對應的行為實作 ----
            var relicIds = new HashSet<string>();
            var known = new HashSet<string>(RelicIds.All);
            foreach (var dto in raw.Relics.relics)
            {
                Require(!string.IsNullOrEmpty(dto.id), "relics.json:有遺物缺 id");
                Require(relicIds.Add(dto.id), $"relics.json:遺物 id 重複——{dto.id}");
                Require(known.Contains(dto.id), $"relics.json:{dto.id} 沒有對應的行為實作(RelicIds)——資料與程式斷線");
                content.Relics.Add(new RelicDef
                {
                    Id = dto.id,
                    Name = dto.name,
                    Rarity = ParseEnum<RelicRarity>(dto.rarity, $"relics.json:{dto.id}.rarity"),
                    Description = dto.description
                });
            }

            // ---- 藥水:不支援中斷選擇型效果 ----
            foreach (var dto in raw.Potions.potions)
            {
                Require(!string.IsNullOrEmpty(dto.id), "potions.json:有藥水缺 id");
                var steps = BuildSteps(dto.steps, $"potions.json:{dto.id}");
                ValidateSteps(steps, $"potions.json:{dto.id}", cardIds, false);
                foreach (var step in steps)
                {
                    Require(step.Op != EffectOp.ChooseExhaustFromHand,
                        $"potions.json:{dto.id} 使用了藥水不支援的 ChooseExhaustFromHand");
                }
                content.Potions.Add(new PotionDef
                {
                    Id = dto.id,
                    Name = dto.name,
                    Description = dto.description,
                    NeedsTarget = dto.needsTarget,
                    Steps = steps
                });
            }

            // ---- 遭遇 ----
            var encounterIds = new HashSet<string>();
            foreach (var dto in raw.Encounters.encounters)
            {
                Require(!string.IsNullOrEmpty(dto.id), "encounters.json:有遭遇缺 id");
                Require(encounterIds.Add(dto.id), $"encounters.json:遭遇 id 重複——{dto.id}");
                Require(dto.enemyIds.Count > 0, $"encounters.json:{dto.id} 沒有敵人");
                foreach (var enemyId in dto.enemyIds)
                {
                    Require(enemyIds.Contains(enemyId), $"encounters.json:{dto.id} 參照不存在的敵人——{enemyId}");
                }
                content.Encounters.Add(new EncounterDef
                {
                    Id = dto.id,
                    EnemyIds = dto.enemyIds.ToArray(),
                    Pool = ParseEnum<EncounterPool>(dto.pool, $"encounters.json:{dto.id}.pool"),
                    Weight = dto.weight
                });
            }

            // ---- 狀態文字:每個 StatusId(None 除外)都必須有一筆,否則 tooltip 會開天窗 ----
            if (raw.Statuses != null && raw.Statuses.statuses.Count > 0)
            {
                var seenStatuses = new HashSet<StatusId>();
                foreach (var dto in raw.Statuses.statuses)
                {
                    var id = ParseEnum<StatusId>(dto.id, "statuses.json:id");
                    Require(id != StatusId.None, "statuses.json:不得為 None");
                    Require(seenStatuses.Add(id), $"statuses.json:狀態重複——{dto.id}");
                    Require(!string.IsNullOrEmpty(dto.name), $"statuses.json:{dto.id} 缺名稱");
                    content.Statuses.Add(new StatusDef { Id = id, Name = dto.name, Description = dto.description });
                }
                foreach (StatusId id in Enum.GetValues(typeof(StatusId)))
                {
                    if (id == StatusId.None) continue;
                    Require(seenStatuses.Contains(id), $"statuses.json:缺少狀態文字——{id}(程式有這個狀態,資料沒有)");
                }
            }

            // ---- 平衡(起始配置的參照要驗:壞 id 會讓每一輪都開不了局) ----
            var b = raw.Balance;
            foreach (var cardId in b.startingDeckCardIds)
            {
                Require(cardIds.Contains(cardId), $"balance.json:起始卡組參照不存在的卡——{cardId}");
            }
            foreach (var relicId in b.startingRelicIds)
            {
                Require(relicIds.Contains(relicId), $"balance.json:起始遺物參照不存在——{relicId}");
            }
            content.Balance = new BalanceDef
            {
                StartHp = b.startHp, StartGold = b.startGold,
                StartingDeckCardIds = b.startingDeckCardIds.ToArray(),
                StartingRelicIds = b.startingRelicIds.ToArray(),
                StartingPotionIds = b.startingPotionIds.ToArray(),
                ShopCardCommonCost = b.shopCardCommonCost,
                ShopCardUncommonCost = b.shopCardUncommonCost,
                ShopCardRareCost = b.shopCardRareCost,
                ShopRelicCost = b.shopRelicCost,
                ShopPotionCost = b.shopPotionCost,
                ShopClassCardCount = b.shopClassCardCount,
                ShopColorlessCardCount = b.shopColorlessCardCount,
                ShopRelicCount = b.shopRelicCount,
                ShopPotionCount = b.shopPotionCount,
                MapCombatWeight = b.mapCombatWeight,
                MapEliteWeight = b.mapEliteWeight,
                MapRestWeight = b.mapRestWeight,
                MapShopWeight = b.mapShopWeight,
                MapTreasureWeight = b.mapTreasureWeight,
                MapMinRowForElite = b.mapMinRowForElite,
                MapMinRowForRest = b.mapMinRowForRest,
                MapNoRestRow = b.mapNoRestRow,
                NormalGoldMin = b.normalGoldMin, NormalGoldMax = b.normalGoldMax,
                EliteGoldMin = b.eliteGoldMin, EliteGoldMax = b.eliteGoldMax,
                BossGoldMin = b.bossGoldMin, BossGoldMax = b.bossGoldMax,
                CardRewardCommonWeight = b.cardRewardCommonWeight,
                CardRewardUncommonWeight = b.cardRewardUncommonWeight,
                CardRewardRareWeight = b.cardRewardRareWeight,
                PotionDropBasePercent = b.potionDropBasePercent,
                PotionDropDeltaPercent = b.potionDropDeltaPercent,
                ShopRemoveBaseCost = b.shopRemoveBaseCost,
                ShopRemoveCostIncrement = b.shopRemoveCostIncrement,
                RestHealPercent = b.restHealPercent,
                MapColumns = b.mapColumns, MapRows = b.mapRows, MapPathCount = b.mapPathCount,
                WeakPoolFightCount = b.weakPoolFightCount
            };

            return content;
        }

        private static CardDef BuildCard(CardDto dto, CardVariantDto variant, string defId)
        {
            return new CardDef
            {
                Id = defId,
                Name = variant.name,
                DescriptionTemplate = variant.description,
                Type = ParseEnum<CardType>(dto.type, $"cards.json:{dto.id}.type"),
                Rarity = ParseEnum<CardRarity>(dto.rarity, $"cards.json:{dto.id}.rarity"),
                Cost = variant.cost,
                CostIsX = dto.costIsX,
                Unplayable = dto.unplayable,
                Exhausts = dto.exhausts,
                Ethereal = dto.ethereal,
                Colorless = dto.colorless,
                CostScaling = ParseEnum<CostScaling>(dto.costScaling, $"cards.json:{dto.id}.costScaling"),
                Steps = BuildSteps(variant.steps, $"cards.json:{defId}"),
                TurnEndInHandSteps = BuildSteps(variant.turnEndInHandSteps, $"cards.json:{defId}(turnEnd)")
            };
        }

        private static EffectStep[] BuildSteps(List<StepDto> dtos, string context)
        {
            if (dtos == null || dtos.Count == 0) return Array.Empty<EffectStep>();
            var steps = new EffectStep[dtos.Count];
            for (int i = 0; i < dtos.Count; i++)
            {
                var s = dtos[i];
                Require(!string.IsNullOrEmpty(s.op), $"{context}:第 {i} 步缺 op");
                steps[i] = new EffectStep(
                    ParseEnum<EffectOp>(s.op, $"{context}.op"),
                    ParseEnum<EffectTarget>(s.target, $"{context}.target"),
                    s.amount,
                    ParseEnum<AmountKind>(s.amountKind, $"{context}.amountKind"),
                    s.secondaryAmount,
                    s.repeat,
                    s.repeatIsX,
                    ParseEnum<StatusId>(s.status, $"{context}.status"),
                    s.cardId,
                    ParseEnum<PileType>(s.pile, $"{context}.pile"),
                    s.customId);
            }
            return steps;
        }

        /// <summary>op/欄位組合白名單——資料層就把非法組合擋下,不留到執行期炸。</summary>
        private static void ValidateSteps(EffectStep[] steps, string context, HashSet<string> cardIds, bool cardIsX)
        {
            foreach (var step in steps)
            {
                if (step.AmountKind == AmountKind.StrengthTimes)
                {
                    Require(step.Op == EffectOp.Damage, $"{context}:StrengthTimes 只能配 Damage");
                }
                if (step.RepeatIsX || step.AmountKind == AmountKind.XEnergy)
                {
                    Require(cardIsX, $"{context}:X 型欄位只能出現在 costIsX 的卡上");
                }
                if (step.Op == EffectOp.ApplyStatus || step.Op == EffectOp.DoubleStatus)
                {
                    Require(step.Status != StatusId.None, $"{context}:{step.Op} 缺 status");
                }
                if (step.AmountKind == AmountKind.PerExhaustedCard
                    || step.AmountKind == AmountKind.PerTargetVulnerable
                    || step.AmountKind == AmountKind.PerAttackPlayedThisTurn
                    || step.AmountKind == AmountKind.PerStrikeCard)
                {
                    // 成長型公式是 amount + secondaryAmount × 數量;沒有 secondaryAmount 就永遠不成長,是資料寫錯
                    Require(step.SecondaryAmount != 0, $"{context}:{step.AmountKind} 需要 secondaryAmount(每單位加成)");
                }
                if (step.Op == EffectOp.AddCardToPile)
                {
                    Require(!string.IsNullOrEmpty(step.CardId), $"{context}:AddCardToPile 缺 cardId");
                    Require(cardIds.Contains(step.CardId), $"{context}:AddCardToPile 參照不存在的卡——{step.CardId}");
                }
                if (step.Op == EffectOp.Custom)
                {
                    Require(!string.IsNullOrEmpty(step.CustomId), $"{context}:Custom 缺 customId");
                }
            }
        }

        private static T ParseEnum<T>(string value, string context) where T : struct
        {
            if (Enum.TryParse(value, false, out T result)) return result;
            throw new ContentValidationException($"{context}:無效的列舉值「{value}」(合法值:{string.Join("/", Enum.GetNames(typeof(T)))})");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new ContentValidationException(message);
        }
    }
}
