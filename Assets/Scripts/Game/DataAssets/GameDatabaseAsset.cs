using System.Collections.Generic;
using UnityEngine;
using STS.Data;

namespace STS.Game.DataAssets
{
    /// <summary>
    /// 全內容的總索引資產。執行期入口:BuildDb() 把所有 SO 轉成純 def 的 ContentDb,
    /// 引擎只看 IContentDb,完全不知道 SO 的存在。
    /// </summary>
    [CreateAssetMenu(menuName = "STS/內容總庫", fileName = "GameDatabase")]
    public sealed class GameDatabaseAsset : ScriptableObject
    {
        [Tooltip("全部卡牌資產")] public List<CardDataAsset> cards = new List<CardDataAsset>();
        [Tooltip("全部敵人資產")] public List<EnemyDataAsset> enemies = new List<EnemyDataAsset>();
        [Tooltip("全部遺物資產")] public List<RelicDataAsset> relics = new List<RelicDataAsset>();
        [Tooltip("全部藥水資產")] public List<PotionDataAsset> potions = new List<PotionDataAsset>();
        [Tooltip("全部遭遇資產")] public List<EncounterAsset> encounters = new List<EncounterAsset>();
        [Tooltip("狀態文字資產(tooltip 用)")] public List<StatusDataAsset> statuses = new List<StatusDataAsset>();
        [Tooltip("平衡參數資產")] public BalanceAsset balance;

        /// <summary>轉出引擎消費的內容庫。每個 Run 開始時呼叫一次即可。</summary>
        public ContentDb BuildDb()
        {
            var content = new ParsedContent();
            foreach (var card in cards)
            {
                if (card != null) card.AddDefs(content.Cards);
            }
            foreach (var enemy in enemies)
            {
                if (enemy != null) content.Enemies.Add(enemy.ToDef());
            }
            foreach (var relic in relics)
            {
                if (relic != null) content.Relics.Add(relic.ToDef());
            }
            foreach (var potion in potions)
            {
                if (potion != null) content.Potions.Add(potion.ToDef());
            }
            foreach (var encounter in encounters)
            {
                if (encounter != null) content.Encounters.Add(encounter.ToDef());
            }
            foreach (var status in statuses)
            {
                if (status != null) content.Statuses.Add(status.ToDef());
            }
            if (balance != null)
            {
                content.Balance = balance.ToDef();
            }
            return ContentDb.From(content);
        }
    }
}
