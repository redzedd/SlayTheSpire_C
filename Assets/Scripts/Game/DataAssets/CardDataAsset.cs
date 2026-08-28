using System.Collections.Generic;
using UnityEngine;
using STS.Core.Cards;

namespace STS.Game.DataAssets
{
    /// <summary>
    /// 卡牌資料資產。base 與 upgrade 兩組欄位各轉出一個獨立 CardDef(id 與 id+)。
    /// 資料的單一真相在 Assets/Data/Source/cards.json;本資產由匯入器生成/就地更新,手改會被下次匯入覆蓋。
    /// </summary>
    [CreateAssetMenu(menuName = "STS/卡牌定義", fileName = "Card")]
    public sealed class CardDataAsset : ScriptableObject
    {
        [Tooltip("卡牌 id(全域唯一,升級版自動為 id+)")] public string id;
        [Tooltip("卡牌類型")] public CardType type = CardType.Attack;
        [Tooltip("稀有度")] public CardRarity rarity = CardRarity.Common;
        [Tooltip("X 費卡(打出時消耗全部能量)")] public bool costIsX;
        [Tooltip("不可打出(狀態/詛咒卡)")] public bool unplayable;
        [Tooltip("打出後消耗")] public bool exhausts;
        [Tooltip("虛無(回合結束在手即消耗)")] public bool ethereal;
        [Tooltip("無色牌:不進戰後卡牌獎勵池,只在商店的無色區出現")] public bool colorless;

        [Header("基礎版")]
        [Tooltip("顯示名稱")] public string baseName;
        [Tooltip("能量費用")] public int baseCost;
        [Tooltip("描述模板({dmg}/{blk} 會代入即時值)")] [TextArea] public string baseDescription;
        [Tooltip("效果步驟")] public EffectStepData[] baseSteps = System.Array.Empty<EffectStepData>();
        [Tooltip("回合結束在手才觸發的步驟(燒傷型)")] public EffectStepData[] baseTurnEndSteps = System.Array.Empty<EffectStepData>();

        [Header("升級版")]
        [Tooltip("是否有升級版(狀態卡通常沒有)")] public bool hasUpgrade;
        [Tooltip("升級版名稱")] public string upgradeName;
        [Tooltip("升級版費用")] public int upgradeCost;
        [Tooltip("升級版描述模板")] [TextArea] public string upgradeDescription;
        [Tooltip("升級版效果步驟")] public EffectStepData[] upgradeSteps = System.Array.Empty<EffectStepData>();
        [Tooltip("升級版回合結束在手步驟")] public EffectStepData[] upgradeTurnEndSteps = System.Array.Empty<EffectStepData>();

        public void AddDefs(List<CardDef> into)
        {
            into.Add(BuildDef(id, baseName, baseCost, baseDescription, baseSteps, baseTurnEndSteps));
            if (hasUpgrade)
            {
                into.Add(BuildDef(id + "+", upgradeName, upgradeCost, upgradeDescription, upgradeSteps, upgradeTurnEndSteps));
            }
        }

        private CardDef BuildDef(string defId, string defName, int cost, string description,
            EffectStepData[] steps, EffectStepData[] turnEndSteps)
        {
            return new CardDef
            {
                Id = defId,
                Name = defName,
                DescriptionTemplate = description,
                Type = type,
                Rarity = rarity,
                Cost = cost,
                CostIsX = costIsX,
                Unplayable = unplayable,
                Exhausts = exhausts,
                Ethereal = ethereal,
                Colorless = colorless,
                Steps = EffectStepData.ToSteps(steps),
                TurnEndInHandSteps = EffectStepData.ToSteps(turnEndSteps)
            };
        }
    }
}
