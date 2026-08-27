using System.Collections.Generic;
using STS.Core.Cards;
using STS.Core.Combat.Enemies;
using STS.Core.Content;
using STS.Core.Potions;
using STS.Core.Relics;

namespace STS.Data
{
    /// <summary>
    /// 字典版 IContentDb 實作。來源可以是 JSON 解析結果(測試/匯入器)或 SO 轉出的 def(執行期),
    /// 引擎兩邊看到的是同一個介面。查無 id 直接拋錯附 id,不回 null。
    /// </summary>
    public sealed class ContentDb : IContentDb
    {
        private readonly Dictionary<string, CardDef> _cards = new Dictionary<string, CardDef>();
        private readonly Dictionary<string, EnemyDef> _enemies = new Dictionary<string, EnemyDef>();
        private readonly Dictionary<string, PotionDef> _potions = new Dictionary<string, PotionDef>();
        private readonly Dictionary<string, RelicDef> _relics = new Dictionary<string, RelicDef>();
        private readonly Dictionary<string, EncounterDef> _encounters = new Dictionary<string, EncounterDef>();

        public BalanceDef Balance { get; private set; } = new BalanceDef();
        public IReadOnlyDictionary<string, CardDef> AllCards => _cards;
        public IReadOnlyDictionary<string, RelicDef> AllRelics => _relics;
        public IReadOnlyDictionary<string, PotionDef> AllPotions => _potions;
        public IReadOnlyDictionary<string, EncounterDef> AllEncounters => _encounters;

        public static ContentDb From(ParsedContent content)
        {
            var db = new ContentDb();
            foreach (var card in content.Cards) db._cards.Add(card.Id, card);
            foreach (var enemy in content.Enemies) db._enemies.Add(enemy.Id, enemy);
            foreach (var potion in content.Potions) db._potions.Add(potion.Id, potion);
            foreach (var relic in content.Relics) db._relics.Add(relic.Id, relic);
            foreach (var encounter in content.Encounters) db._encounters.Add(encounter.Id, encounter);
            db.Balance = content.Balance;
            return db;
        }

        public CardDef GetCard(string cardId)
        {
            if (_cards.TryGetValue(cardId, out var def)) return def;
            throw new KeyNotFoundException($"查無卡牌定義:{cardId}");
        }

        public EnemyDef GetEnemy(string enemyId)
        {
            if (_enemies.TryGetValue(enemyId, out var def)) return def;
            throw new KeyNotFoundException($"查無敵人定義:{enemyId}");
        }

        public PotionDef GetPotion(string potionId)
        {
            if (_potions.TryGetValue(potionId, out var def)) return def;
            throw new KeyNotFoundException($"查無藥水定義:{potionId}");
        }

        public RelicDef GetRelic(string relicId)
        {
            if (_relics.TryGetValue(relicId, out var def)) return def;
            throw new KeyNotFoundException($"查無遺物定義:{relicId}");
        }

        public EncounterDef GetEncounter(string encounterId)
        {
            if (_encounters.TryGetValue(encounterId, out var def)) return def;
            throw new KeyNotFoundException($"查無遭遇定義:{encounterId}");
        }
    }
}
