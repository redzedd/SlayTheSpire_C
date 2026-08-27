using System.IO;
using NUnit.Framework;
using STS.Core.Combat.Enemies;
using STS.Core.Relics;
using STS.Data;

namespace STS.Content.Tests
{
    /// <summary>
    /// 內容資料的守門測試:六份 JSON 解析得過、參照完整、資料與程式沒有斷線。
    /// 讀的是 Assets/Data/Source 的真實檔案——這裡紅了代表資料壞了,不是程式壞了。
    /// </summary>
    public class ContentValidationTests
    {
        private static ParsedContent _content;
        private static ContentDb _db;

        [OneTimeSetUp]
        public void 載入全部內容()
        {
            _content = ContentLoader.Load();
            _db = ContentDb.From(_content);
        }

        [Test]
        public void 六份JSON_解析與驗證全數通過()
        {
            Assert.IsNotNull(_content);
            Assert.Greater(_content.Cards.Count, 0);
            Assert.Greater(_content.Enemies.Count, 0);
            Assert.Greater(_content.Relics.Count, 0);
            Assert.Greater(_content.Potions.Count, 0);
            Assert.Greater(_content.Encounters.Count, 0);
        }

        [Test]
        public void 卡池規模_符合切片目標()
        {
            int playable = 0;
            foreach (var card in _content.Cards)
            {
                if (!card.Id.EndsWith("+") && card.Type != Core.Cards.CardType.Status) playable++;
            }
            Assert.GreaterOrEqual(playable, 26, "切片目標:至少 26 張可入卡組的卡");
        }

        [Test]
        public void 非狀態卡_都有升級版_id慣例正確()
        {
            foreach (var card in _content.Cards)
            {
                if (card.Id.EndsWith("+") || card.Type == Core.Cards.CardType.Status) continue;
                Assert.DoesNotThrow(() => _db.GetCard(card.Id + "+"), $"{card.Id} 缺升級版定義");
            }
        }

        [Test]
        public void 起始卡組成分_存在()
        {
            Assert.DoesNotThrow(() => _db.GetCard("strike"));
            Assert.DoesNotThrow(() => _db.GetCard("defend"));
            Assert.DoesNotThrow(() => _db.GetCard("bash"));
        }

        [Test]
        public void 遺物資料_與行為註冊表完全對齊()
        {
            Assert.AreEqual(RelicIds.All.Length, _content.Relics.Count, "資料筆數與 RelicIds 不一致");
            foreach (var id in RelicIds.All)
            {
                Assert.DoesNotThrow(() => _db.GetRelic(id), $"RelicIds 有 {id} 但資料層沒有");
            }
        }

        [Test]
        public void 守護者_招式表齊全_供自訂AI使用()
        {
            var guardian = _db.GetEnemy("guardian");
            Assert.AreEqual(AiKind.Custom, guardian.Ai);
            string[] required =
            {
                "guardian_charge", "guardian_bash", "guardian_vent",
                "guardian_whirl", "guardian_roll", "guardian_twin"
            };
            foreach (var moveId in required)
            {
                Assert.DoesNotThrow(() => guardian.GetMove(moveId), $"守護者缺自訂 AI 需要的招式 {moveId}");
            }
        }

        [Test]
        public void 遭遇表_四個池都有內容()
        {
            bool weak = false, normal = false, elite = false, boss = false;
            foreach (var pair in _db.AllEncounters)
            {
                switch (pair.Value.Pool)
                {
                    case EncounterPool.Weak: weak = true; break;
                    case EncounterPool.Normal: normal = true; break;
                    case EncounterPool.Elite: elite = true; break;
                    case EncounterPool.Boss: boss = true; break;
                }
                foreach (var enemyId in pair.Value.EnemyIds)
                {
                    Assert.DoesNotThrow(() => _db.GetEnemy(enemyId), $"{pair.Key} 參照不存在的敵人 {enemyId}");
                }
            }
            Assert.IsTrue(weak && normal && elite && boss, "弱/普/精英/Boss 四池必須都有遭遇");
        }

        [Test]
        public void 壞資料_未知欄位_被嚴格模式擋下()
        {
            string bad = "{ \"cards\": [ { \"id\": \"x\", \"typo_field\": 1, \"base\": { \"name\": \"x\", \"cost\": 0, \"steps\": [] } } ] }";
            Assert.Throws<ContentValidationException>(() => ContentParser.ParseRaw(
                bad, "{\"enemies\":[]}", "{\"relics\":[]}", "{\"potions\":[]}", "{\"encounters\":[]}", "{}"));
        }

        [Test]
        public void 壞資料_非法列舉與非法組合_被驗證擋下()
        {
            // 非法列舉值
            string badEnum = "{ \"cards\": [ { \"id\": \"x\", \"type\": \"NotAType\", \"base\": { \"name\": \"x\", \"cost\": 0, \"steps\": [] } } ] }";
            var raw1 = ContentParser.ParseRaw(badEnum, "{\"enemies\":[]}", "{\"relics\":[]}", "{\"potions\":[]}", "{\"encounters\":[]}", "{}");
            Assert.Throws<ContentValidationException>(() => ContentParser.BuildDefs(raw1));

            // X 型欄位出現在非 X 費卡
            string badX = "{ \"cards\": [ { \"id\": \"x\", \"base\": { \"name\": \"x\", \"cost\": 1, \"steps\": [ { \"op\": \"Damage\", \"target\": \"TargetEnemy\", \"amount\": 5, \"repeatIsX\": true } ] } } ] }";
            var raw2 = ContentParser.ParseRaw(badX, "{\"enemies\":[]}", "{\"relics\":[]}", "{\"potions\":[]}", "{\"encounters\":[]}", "{}");
            Assert.Throws<ContentValidationException>(() => ContentParser.BuildDefs(raw2));

            // AddCardToPile 參照不存在的卡
            string badRef = "{ \"cards\": [ { \"id\": \"x\", \"base\": { \"name\": \"x\", \"cost\": 0, \"steps\": [ { \"op\": \"AddCardToPile\", \"target\": \"Self\", \"cardId\": \"ghost\" } ] } } ] }";
            var raw3 = ContentParser.ParseRaw(badRef, "{\"enemies\":[]}", "{\"relics\":[]}", "{\"potions\":[]}", "{\"encounters\":[]}", "{}");
            Assert.Throws<ContentValidationException>(() => ContentParser.BuildDefs(raw3));
        }
    }

    /// <summary>從專案真實路徑載入六份 JSON(editor 測試的工作目錄是專案根)。</summary>
    internal static class ContentLoader
    {
        private const string Dir = "Assets/Data/Source";

        internal static ParsedContent Load()
        {
            var raw = ContentParser.ParseRaw(
                File.ReadAllText(Path.Combine(Dir, "cards.json")),
                File.ReadAllText(Path.Combine(Dir, "enemies.json")),
                File.ReadAllText(Path.Combine(Dir, "relics.json")),
                File.ReadAllText(Path.Combine(Dir, "potions.json")),
                File.ReadAllText(Path.Combine(Dir, "encounters.json")),
                File.ReadAllText(Path.Combine(Dir, "balance.json")));
            return ContentParser.BuildDefs(raw);
        }
    }
}
