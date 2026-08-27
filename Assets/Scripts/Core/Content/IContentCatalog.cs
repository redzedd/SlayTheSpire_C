using System.Collections.Generic;
using STS.Core.Cards;
using STS.Core.Combat.Enemies;
using STS.Core.Combat.Statuses;
using STS.Core.Potions;
using STS.Core.Relics;

namespace STS.Core.Content
{
    /// <summary>
    /// 全內容目錄介面——Run 層(獎勵池/商店/遭遇抽選)需要的完整視野。
    /// 戰鬥引擎仍只依賴 IContentDb(單筆查詢);目錄視野只給 Run 層,職責分離。
    /// </summary>
    public interface IContentCatalog : IContentDb
    {
        IEnumerable<CardDef> AllCardDefs { get; }
        IEnumerable<RelicDef> AllRelicDefs { get; }
        IEnumerable<PotionDef> AllPotionDefs { get; }
        IEnumerable<EncounterDef> AllEncounterDefs { get; }
        BalanceDef Balance { get; }
        RelicDef GetRelicDef(string relicId);
        EncounterDef GetEncounter(string encounterId);
        /// <summary>狀態的名稱/說明(tooltip 用);查無定義回 null,由呼叫端決定退路。</summary>
        StatusDef GetStatusDef(StatusId statusId);
    }
}
