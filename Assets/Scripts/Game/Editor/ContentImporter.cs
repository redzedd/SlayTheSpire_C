using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using STS.Data;
using STS.Data.Dto;
using STS.Game.DataAssets;

namespace STS.Game.Editor
{
    /// <summary>
    /// JSON(單一真相)→ ScriptableObject 批次匯入器。
    /// 既有資產一律「就地更新」保 GUID(場景/prefab 參照不斷);不存在才建新資產。
    /// 先全量驗證(ContentParser.BuildDefs)再落盤——驗證不過就一個資產都不動。
    /// </summary>
    public static class ContentImporter
    {
        private const string SourceDir = "Assets/Data/Source";

        [MenuItem("STS/重新匯入資料(JSON→SO)")]
        public static void Reimport()
        {
            try
            {
                var raw = ContentParser.ParseRaw(
                    ReadSource("cards.json"),
                    ReadSource("enemies.json"),
                    ReadSource("relics.json"),
                    ReadSource("potions.json"),
                    ReadSource("encounters.json"),
                    ReadSource("balance.json"));
                ContentParser.BuildDefs(raw);   // 全量驗證閘門:過不了就中止,不碰任何資產

                var db = LoadOrCreate<GameDatabaseAsset>("Assets/Data/GameDatabase.asset");
                db.cards.Clear();
                db.enemies.Clear();
                db.relics.Clear();
                db.potions.Clear();
                db.encounters.Clear();

                foreach (var dto in raw.Cards.cards)
                {
                    var asset = LoadOrCreate<CardDataAsset>($"Assets/Data/Cards/{dto.id}.asset");
                    ApplyCard(asset, dto);
                    EditorUtility.SetDirty(asset);
                    db.cards.Add(asset);
                }
                foreach (var dto in raw.Enemies.enemies)
                {
                    var asset = LoadOrCreate<EnemyDataAsset>($"Assets/Data/Enemies/{dto.id}.asset");
                    ApplyEnemy(asset, dto);
                    EditorUtility.SetDirty(asset);
                    db.enemies.Add(asset);
                }
                foreach (var dto in raw.Relics.relics)
                {
                    var asset = LoadOrCreate<RelicDataAsset>($"Assets/Data/Relics/{dto.id}.asset");
                    asset.id = dto.id;
                    asset.relicName = dto.name;
                    asset.rarity = ParseEnum<Core.Relics.RelicRarity>(dto.rarity);
                    asset.description = dto.description;
                    EditorUtility.SetDirty(asset);
                    db.relics.Add(asset);
                }
                foreach (var dto in raw.Potions.potions)
                {
                    var asset = LoadOrCreate<PotionDataAsset>($"Assets/Data/Potions/{dto.id}.asset");
                    asset.id = dto.id;
                    asset.potionName = dto.name;
                    asset.needsTarget = dto.needsTarget;
                    asset.steps = ToStepData(dto.steps);
                    EditorUtility.SetDirty(asset);
                    db.potions.Add(asset);
                }
                foreach (var dto in raw.Encounters.encounters)
                {
                    var asset = LoadOrCreate<EncounterAsset>($"Assets/Data/Encounters/{dto.id}.asset");
                    asset.id = dto.id;
                    asset.pool = ParseEnum<Core.Combat.Enemies.EncounterPool>(dto.pool);
                    asset.weight = dto.weight;
                    asset.enemyIds = dto.enemyIds.ToArray();
                    EditorUtility.SetDirty(asset);
                    db.encounters.Add(asset);
                }

                var balanceAsset = LoadOrCreate<BalanceAsset>("Assets/Data/Balance.asset");
                ApplyBalance(balanceAsset, raw.Balance);
                EditorUtility.SetDirty(balanceAsset);
                db.balance = balanceAsset;

                EditorUtility.SetDirty(db);
                AssetDatabase.SaveAssets();
                Debug.Log($"STS 匯入完成:卡 {raw.Cards.cards.Count}、敵 {raw.Enemies.enemies.Count}、遺物 {raw.Relics.relics.Count}、藥水 {raw.Potions.potions.Count}、遭遇 {raw.Encounters.encounters.Count}。");
            }
            catch (ContentValidationException e)
            {
                Debug.LogError($"STS 匯入中止(內容驗證失敗):{e.Message}");
            }
            catch (Exception e)
            {
                Debug.LogError($"STS 匯入中止(非預期錯誤):{e}");
            }
        }

        private static string ReadSource(string fileName)
        {
            string path = Path.Combine(SourceDir, fileName);
            if (!File.Exists(path))
            {
                throw new ContentValidationException($"{fileName}:來源檔不存在——{path}");
            }
            return File.ReadAllText(path);
        }

        private static T LoadOrCreate<T>(string assetPath) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            if (existing != null) return existing;
            EnsureFolder(Path.GetDirectoryName(assetPath).Replace('\\', '/'));
            var created = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(created, assetPath);
            return created;
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }

        private static void ApplyCard(CardDataAsset asset, CardDto dto)
        {
            asset.id = dto.id;
            asset.type = ParseEnum<Core.Cards.CardType>(dto.type);
            asset.rarity = ParseEnum<Core.Cards.CardRarity>(dto.rarity);
            asset.costIsX = dto.costIsX;
            asset.unplayable = dto.unplayable;
            asset.exhausts = dto.exhausts;
            asset.ethereal = dto.ethereal;
            asset.baseName = dto.@base.name;
            asset.baseCost = dto.@base.cost;
            asset.baseDescription = dto.@base.description;
            asset.baseSteps = ToStepData(dto.@base.steps);
            asset.baseTurnEndSteps = ToStepData(dto.@base.turnEndInHandSteps);
            asset.hasUpgrade = dto.upgrade != null;
            if (dto.upgrade != null)
            {
                asset.upgradeName = dto.upgrade.name;
                asset.upgradeCost = dto.upgrade.cost;
                asset.upgradeDescription = dto.upgrade.description;
                asset.upgradeSteps = ToStepData(dto.upgrade.steps);
                asset.upgradeTurnEndSteps = ToStepData(dto.upgrade.turnEndInHandSteps);
            }
            else
            {
                asset.upgradeName = string.Empty;
                asset.upgradeCost = 0;
                asset.upgradeDescription = string.Empty;
                asset.upgradeSteps = Array.Empty<EffectStepData>();
                asset.upgradeTurnEndSteps = Array.Empty<EffectStepData>();
            }
        }

        private static void ApplyEnemy(EnemyDataAsset asset, EnemyDto dto)
        {
            asset.id = dto.id;
            asset.enemyName = dto.name;
            asset.hpMin = dto.hpMin;
            asset.hpMax = dto.hpMax;
            asset.ai = ParseEnum<Core.Combat.Enemies.AiKind>(dto.ai);
            var moves = new MoveData[dto.moves.Count];
            for (int i = 0; i < dto.moves.Count; i++)
            {
                var m = dto.moves[i];
                moves[i] = new MoveData
                {
                    id = m.id,
                    moveName = m.name,
                    intent = ParseEnum<Core.Combat.Enemies.IntentType>(m.intent),
                    weight = m.weight,
                    maxConsecutive = m.maxConsecutive,
                    steps = ToStepData(m.steps)
                };
            }
            asset.moves = moves;
            asset.openingScript = dto.openingScript.ToArray();
            asset.loopScript = dto.loopScript.ToArray();
            var initial = new StatusStackData[dto.initialStatuses.Count];
            for (int i = 0; i < dto.initialStatuses.Count; i++)
            {
                initial[i] = new StatusStackData
                {
                    status = ParseEnum<Core.Combat.Statuses.StatusId>(dto.initialStatuses[i].status),
                    stacks = dto.initialStatuses[i].stacks
                };
            }
            asset.initialStatuses = initial;
        }

        private static void ApplyBalance(BalanceAsset asset, BalanceDto dto)
        {
            asset.normalGoldMin = dto.normalGoldMin;
            asset.normalGoldMax = dto.normalGoldMax;
            asset.eliteGoldMin = dto.eliteGoldMin;
            asset.eliteGoldMax = dto.eliteGoldMax;
            asset.bossGoldMin = dto.bossGoldMin;
            asset.bossGoldMax = dto.bossGoldMax;
            asset.cardRewardCommonWeight = dto.cardRewardCommonWeight;
            asset.cardRewardUncommonWeight = dto.cardRewardUncommonWeight;
            asset.cardRewardRareWeight = dto.cardRewardRareWeight;
            asset.potionDropBasePercent = dto.potionDropBasePercent;
            asset.potionDropDeltaPercent = dto.potionDropDeltaPercent;
            asset.shopRemoveBaseCost = dto.shopRemoveBaseCost;
            asset.shopRemoveCostIncrement = dto.shopRemoveCostIncrement;
            asset.restHealPercent = dto.restHealPercent;
            asset.mapColumns = dto.mapColumns;
            asset.mapRows = dto.mapRows;
            asset.mapPathCount = dto.mapPathCount;
            asset.weakPoolFightCount = dto.weakPoolFightCount;
        }

        private static EffectStepData[] ToStepData(List<StepDto> dtos)
        {
            if (dtos == null || dtos.Count == 0) return Array.Empty<EffectStepData>();
            var result = new EffectStepData[dtos.Count];
            for (int i = 0; i < dtos.Count; i++)
            {
                var s = dtos[i];
                result[i] = new EffectStepData
                {
                    op = ParseEnum<Core.Cards.EffectOp>(s.op),
                    target = ParseEnum<Core.Cards.EffectTarget>(s.target),
                    amount = s.amount,
                    amountKind = ParseEnum<Core.Cards.AmountKind>(s.amountKind),
                    secondaryAmount = s.secondaryAmount,
                    repeat = s.repeat,
                    repeatIsX = s.repeatIsX,
                    status = ParseEnum<Core.Combat.Statuses.StatusId>(s.status),
                    cardId = s.cardId,
                    pile = ParseEnum<Core.Cards.PileType>(s.pile),
                    customId = s.customId
                };
            }
            return result;
        }

        // 已過 ContentParser 驗證,這裡的解析不會失敗;失敗即程式錯誤,直接炸出來
        private static T ParseEnum<T>(string value) where T : struct
        {
            return (T)Enum.Parse(typeof(T), value, false);
        }
    }
}
