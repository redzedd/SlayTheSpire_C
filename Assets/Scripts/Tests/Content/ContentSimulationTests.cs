using System.Collections.Generic;
using NUnit.Framework;
using STS.Core.Cards;
using STS.Core.Combat;
using STS.Core.Rng;
using STS.Data;

namespace STS.Content.Tests
{
    /// <summary>
    /// 全內容模擬戰(煙霧級):真實 JSON 資料 + 真實引擎,自動出牌把每個遭遇與每張卡都跑過一遍。
    /// 目的不是驗數值,是抓「資料組合在引擎裡會炸」的地雷(壞列舉/壞參照/沒實作的 op)。
    /// </summary>
    public class ContentSimulationTests
    {
        private static ContentDb _db;

        [OneTimeSetUp]
        public void 載入內容()
        {
            _db = ContentDb.From(ContentLoader.Load());
        }

        private static CombatSetup 起始卡組Setup(IEnumerable<string> enemyIds, int playerHp = 80)
        {
            var setup = new CombatSetup { PlayerHp = playerHp, PlayerMaxHp = playerHp };
            int instanceId = 1;
            for (int i = 0; i < 5; i++) setup.Deck.Add(new CardInstance(instanceId++, "strike"));
            for (int i = 0; i < 4; i++) setup.Deck.Add(new CardInstance(instanceId++, "defend"));
            setup.Deck.Add(new CardInstance(instanceId++, "bash"));
            setup.EnemyIds.AddRange(enemyIds);
            return setup;
        }

        /// <summary>自動打:每回合從左到右打出所有還打得動的牌,最多 maxRounds 回合。</summary>
        private static void 自動打完(CombatEngine engine, int maxRounds = 30)
        {
            engine.StartCombat();
            for (int round = 0; round < maxRounds; round++)
            {
                if (engine.State.Phase != CombatPhase.PlayerTurn) break;
                bool playedAny = true;
                while (playedAny && engine.State.Phase == CombatPhase.PlayerTurn)
                {
                    playedAny = false;
                    for (int i = 0; i < engine.State.Hand.Count; i++)
                    {
                        int target = 第一個活敵(engine);
                        if (engine.CanPlayCard(i, target, out _))
                        {
                            engine.PlayCard(i, target);
                            playedAny = true;
                            break;
                        }
                    }
                    if (engine.State.Phase == CombatPhase.AwaitingChoice)
                    {
                        var pick = new int[engine.State.PendingChoiceCount];
                        for (int i = 0; i < pick.Length; i++) pick[i] = i;
                        engine.ResolveChoice(pick);
                        playedAny = true;
                    }
                }
                if (engine.State.Phase != CombatPhase.PlayerTurn) break;
                engine.EndPlayerTurn();
            }
        }

        private static int 第一個活敵(CombatEngine engine)
        {
            for (int i = 0; i < engine.State.Enemies.Count; i++)
            {
                if (engine.State.Enemies[i].IsAlive) return i;
            }
            return 0;
        }

        [Test]
        public void 每個遭遇_固定種子自動打完_不噴例外()
        {
            foreach (var pair in _db.AllEncounters)
            {
                var setup = 起始卡組Setup(pair.Value.EnemyIds);
                var engine = new CombatEngine(_db, RunRng.FromSeed(20260803UL), setup);
                Assert.DoesNotThrow(() => 自動打完(engine), $"遭遇 {pair.Key} 模擬中噴例外");
                Assert.AreNotEqual(CombatPhase.NotStarted, engine.State.Phase, $"遭遇 {pair.Key} 沒開打");
            }
        }

        [Test]
        public void 每張卡_十張同卡自動打_全部效果路徑不炸()
        {
            foreach (var pair in _db.AllCards)
            {
                var card = pair.Value;
                if (card.Id.EndsWith("+")) continue;   // 升級版與基礎版共用效果路徑,基礎跑過即可

                var setup = new CombatSetup { PlayerHp = 200, PlayerMaxHp = 200 };
                for (int i = 0; i < 10; i++) setup.Deck.Add(new CardInstance(i + 1, card.Id));
                setup.EnemyIds.Add("cultist");
                var engine = new CombatEngine(_db, RunRng.FromSeed(7UL), setup);
                engine.StartCombat();

                if (card.Unplayable)
                {
                    Assert.IsFalse(engine.CanPlayCard(0, 0, out string reason), $"{card.Id} 應為不可打出");
                    Assert.AreEqual("此卡不可打出", reason);
                    continue;
                }

                // 宣告了打出條件的卡(契約終結)在條件未滿足時拒絕打出是正確行為,不是當機。
                // 只放行這一種拒絕;其他任何理由的拒絕仍然算失敗。效果路徑由該卡的專屬測試覆蓋。
                if (card.PlayCondition != PlayCondition.None && !engine.CanPlayCard(0, 0, out _)) continue;

                Assert.DoesNotThrow(() =>
                {
                    engine.PlayCard(0, 0);
                    if (engine.State.Phase == CombatPhase.AwaitingChoice)
                    {
                        var pick = new int[engine.State.PendingChoiceCount];
                        for (int i = 0; i < pick.Length; i++) pick[i] = i;
                        engine.ResolveChoice(pick);
                    }
                }, $"卡牌 {card.Id} 打出時噴例外");
            }
        }

        [Test]
        public void 升級卡_也各打一次_不炸()
        {
            foreach (var pair in _db.AllCards)
            {
                var card = pair.Value;
                if (!card.Id.EndsWith("+") || card.Unplayable) continue;
                string baseId = card.Id.Substring(0, card.Id.Length - 1);

                var setup = new CombatSetup { PlayerHp = 200, PlayerMaxHp = 200 };
                for (int i = 0; i < 10; i++) setup.Deck.Add(new CardInstance(i + 1, baseId, upgraded: true));
                setup.EnemyIds.Add("cultist");
                var engine = new CombatEngine(_db, RunRng.FromSeed(7UL), setup);
                engine.StartCombat();

                if (card.PlayCondition != PlayCondition.None && !engine.CanPlayCard(0, 0, out _)) continue;

                Assert.DoesNotThrow(() =>
                {
                    engine.PlayCard(0, 0);
                    if (engine.State.Phase == CombatPhase.AwaitingChoice)
                    {
                        var pick = new int[engine.State.PendingChoiceCount];
                        for (int i = 0; i < pick.Length; i++) pick[i] = i;
                        engine.ResolveChoice(pick);
                    }
                }, $"升級卡 {card.Id} 打出時噴例外");
            }
        }

        [Test]
        public void 守護者戰_固定種子_能完整跑多回合()
        {
            var setup = 起始卡組Setup(new[] { "guardian" }, playerHp: 300);
            var engine = new CombatEngine(_db, RunRng.FromSeed(99UL), setup);
            Assert.DoesNotThrow(() => 自動打完(engine, maxRounds: 20));
            // 起始卡組打不死 240 血 Boss:預期敗北或回合耗盡,重點是全程不炸
            Assert.AreNotEqual(CombatPhase.Victory, engine.State.Phase);
        }
    }
}
