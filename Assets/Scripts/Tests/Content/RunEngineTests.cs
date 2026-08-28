using System.Collections.Generic;
using NUnit.Framework;
using STS.Core.Combat;
using STS.Core.Combat.Enemies;
using STS.Core.Map;
using STS.Core.Run;
using STS.Data;

namespace STS.Content.Tests
{
    /// <summary>
    /// Run 層流程測試(真實 JSON 內容):起始配置、遭遇池、獎勵、商店、燈火、寶箱、勝敗,
    /// 以及一次「真實戰鬥自動爬塔」整合煙霧。
    /// </summary>
    public class RunEngineTests
    {
        private static ContentDb _db;

        [OneTimeSetUp]
        public void 載入內容()
        {
            _db = ContentDb.From(ContentLoader.Load());
        }

        /// <summary>手作地圖:第 0 列放指定型別節點,全部連向 Boss——精準測單一節點型別的流程。</summary>
        private static RunEngine 手作引擎(ulong seed, params MapNodeType[] row0Types)
        {
            var engine = RunEngine.NewRun(_db, seed);
            var map = new MapGraph();
            for (int i = 0; i < row0Types.Length; i++)
            {
                map.Nodes.Add(new MapNode { Id = i, Row = 0, Col = i, Type = row0Types[i] });
            }
            var boss = new MapNode { Id = row0Types.Length, Row = 1, Col = 0, Type = MapNodeType.Boss };
            map.Nodes.Add(boss);
            map.BossNodeId = boss.Id;
            for (int i = 0; i < row0Types.Length; i++)
            {
                map.Edges.Add(new MapEdge(i, boss.Id));
            }
            engine.State.Map = map;
            return engine;
        }

        [Test]
        public void 新輪_起始配置來自平衡表()
        {
            var engine = RunEngine.NewRun(_db, 42UL);
            Assert.AreEqual(80, engine.State.Hp);
            Assert.AreEqual(80, engine.State.MaxHp);
            Assert.AreEqual(99, engine.State.Gold);
            Assert.AreEqual(10, engine.State.Deck.Count);
            Assert.AreEqual(1, engine.State.Relics.Count);
            Assert.AreEqual("burning_blood", engine.State.Relics[0].Id);
            Assert.IsNotNull(engine.State.Map);
            Assert.AreEqual(RunPhase.ChoosingNode, engine.State.Phase);
        }

        [Test]
        public void 決定性_同種子同地圖()
        {
            var a = RunEngine.NewRun(_db, 777UL);
            var b = RunEngine.NewRun(_db, 777UL);
            Assert.AreEqual(a.State.Map.Nodes.Count, b.State.Map.Nodes.Count);
            for (int i = 0; i < a.State.Map.Nodes.Count; i++)
            {
                Assert.AreEqual(a.State.Map.Nodes[i].Type, b.State.Map.Nodes[i].Type);
                Assert.AreEqual(a.State.Map.Nodes[i].Col, b.State.Map.Nodes[i].Col);
            }
        }

        [Test]
        public void 進戰鬥節點_前三場走弱池()
        {
            var engine = 手作引擎(5UL, MapNodeType.Combat);
            var entry = engine.EnterNode(0);
            Assert.AreEqual(RunPhase.InCombat, engine.State.Phase);
            Assert.IsNotNull(entry.EncounterId);
            Assert.AreEqual(EncounterPool.Weak, _db.GetEncounter(entry.EncounterId).Pool);
        }

        [Test]
        public void 打滿弱池場數後_改抽普通池()
        {
            var engine = 手作引擎(5UL, MapNodeType.Combat);
            engine.State.NormalCombatsFought = _db.Balance.WeakPoolFightCount;
            var entry = engine.EnterNode(0);
            Assert.AreEqual(EncounterPool.Normal, _db.GetEncounter(entry.EncounterId).Pool);
        }

        [Test]
        public void 戰勝_金幣要自己領_卡三選一_藥水浮動生效()
        {
            var engine = 手作引擎(9UL, MapNodeType.Combat);
            engine.EnterNode(0);
            int goldBefore = engine.State.Gold;
            engine.ApplyCombatResult(true, 60);

            Assert.AreEqual(RunPhase.ChoosingReward, engine.State.Phase);
            Assert.AreEqual(60, engine.State.Hp);
            var rewards = engine.PendingRewards;
            Assert.IsNotNull(rewards);
            Assert.That(rewards.Gold, Is.InRange(_db.Balance.NormalGoldMin, _db.Balance.NormalGoldMax));
            // 金幣不自動入帳:要玩家在「搜刮!」清單上點下去
            Assert.AreEqual(goldBefore, engine.State.Gold);
            Assert.IsTrue(engine.ClaimRewardGold());
            Assert.AreEqual(goldBefore + rewards.Gold, engine.State.Gold);
            Assert.IsFalse(engine.ClaimRewardGold(), "同一筆金幣不得領兩次");
            Assert.AreEqual(3, rewards.CardChoices.Count);
            foreach (var cardId in rewards.CardChoices)
            {
                Assert.IsFalse(cardId.EndsWith("+"), "獎勵不得直接給升級卡");
                Assert.DoesNotThrow(() => _db.GetCard(cardId));
            }
            CollectionAssert.AllItemsAreUnique(rewards.CardChoices);
            // 藥水掉落浮動:未掉 +delta、掉了 -delta,絕對值必等於 delta
            Assert.AreEqual(_db.Balance.PotionDropDeltaPercent, System.Math.Abs(engine.State.PotionChanceOffset));
        }

        [Test]
        public void 拿卡入組_卡項結案_離開才回選節點()
        {
            var engine = 手作引擎(11UL, MapNodeType.Combat, MapNodeType.Combat);
            engine.EnterNode(0);
            engine.ApplyCombatResult(true, 70);
            int deckBefore = engine.State.Deck.Count;
            string chosen = engine.PendingRewards.CardChoices[1];
            engine.TakeCardReward(1);
            Assert.AreEqual(deckBefore + 1, engine.State.Deck.Count);
            Assert.AreEqual(chosen, engine.State.Deck[engine.State.Deck.Count - 1].CardId);
            // 選完卡只是卡那一項結案,金幣還在清單上等著領
            Assert.IsFalse(engine.PendingRewards.HasCard);
            Assert.AreEqual(RunPhase.ChoosingReward, engine.State.Phase);
            Assert.Throws<System.InvalidOperationException>(() => engine.TakeCardReward(0));

            engine.LeaveRewards();
            Assert.AreEqual(RunPhase.ChoosingNode, engine.State.Phase);
            Assert.IsNull(engine.PendingRewards);
        }

        [Test]
        public void 跳過選卡_其餘獎勵仍可領()
        {
            var engine = 手作引擎(23UL, MapNodeType.Combat, MapNodeType.Combat);
            engine.EnterNode(0);
            engine.ApplyCombatResult(true, 70);
            int deckBefore = engine.State.Deck.Count;
            int goldBefore = engine.State.Gold;
            engine.SkipCardReward();
            Assert.AreEqual(deckBefore, engine.State.Deck.Count);
            Assert.IsTrue(engine.ClaimRewardGold());
            Assert.Greater(engine.State.Gold, goldBefore);
            engine.LeaveRewards();
            Assert.AreEqual(RunPhase.ChoosingNode, engine.State.Phase);
        }

        [Test]
        public void 精英戰勝_掉未持有遺物()
        {
            var engine = 手作引擎(13UL, MapNodeType.Elite);
            var entry = engine.EnterNode(0);
            Assert.AreEqual(EncounterPool.Elite, _db.GetEncounter(entry.EncounterId).Pool);
            engine.ApplyCombatResult(true, 50);
            string relic = engine.PendingRewards.RelicId;
            Assert.IsNotNull(relic);
            Assert.AreNotEqual("burning_blood", relic);
            Assert.IsTrue(engine.ClaimRewardRelic(), "遺物也要自己領");
            int count = 0;
            foreach (var owned in engine.State.Relics)
            {
                if (owned.Id == relic) count++;
            }
            Assert.AreEqual(1, count, "遺物應恰好入包一次");
        }

        [Test]
        public void 商店_買卡扣金_移卡漲價_離開回選節點()
        {
            var engine = 手作引擎(17UL, MapNodeType.Shop);
            engine.EnterNode(0);
            Assert.AreEqual(RunPhase.InShop, engine.State.Phase);
            var shop = engine.Shop;
            Assert.GreaterOrEqual(shop.CardIds.Count, 1);
            Assert.AreEqual(_db.Balance.ShopRemoveBaseCost, shop.RemoveCost);

            int goldBefore = engine.State.Gold;
            int deckBefore = engine.State.Deck.Count;
            Assert.IsTrue(engine.BuyCard(0));
            Assert.AreEqual(deckBefore + 1, engine.State.Deck.Count);
            Assert.AreEqual(goldBefore - shop.CardCosts[0], engine.State.Gold);
            Assert.IsFalse(engine.BuyCard(0), "售出即下架,不得重買");

            // 移卡:扣款、卡組變少、下次漲價
            engine.State.Gold = 500;
            int removeCostBefore = shop.RemoveCost;
            int deckAfterBuy = engine.State.Deck.Count;
            Assert.IsTrue(engine.BuyRemoveCard(0));
            Assert.AreEqual(deckAfterBuy - 1, engine.State.Deck.Count);
            Assert.AreEqual(removeCostBefore + _db.Balance.ShopRemoveCostIncrement, shop.RemoveCost);

            engine.LeaveShop();
            Assert.AreEqual(RunPhase.ChoosingNode, engine.State.Phase);
        }

        [Test]
        public void 商店貨架_職業牌與無色牌分區_遺物藥水各三格()
        {
            var balance = _db.Balance;
            // 多個種子:單一種子會讓「抽到重複遺物就少一格」這種缺陷矇混過關
            foreach (ulong seed in new ulong[] { 29UL, 37UL, 41UL, 53UL })
            {
                var engine = 手作引擎(seed, MapNodeType.Shop);
                engine.EnterNode(0);
                var shop = engine.Shop;

                Assert.AreEqual(balance.ShopClassCardCount, shop.ClassCardCount, $"種子 {seed}:上排職業牌張數要照 BalanceDef");
                Assert.AreEqual(balance.ShopClassCardCount + balance.ShopColorlessCardCount, shop.CardIds.Count, $"種子 {seed}");
                Assert.AreEqual(balance.ShopRelicCount, shop.RelicIds.Count, $"種子 {seed}:遺物必須湊滿格");
                Assert.AreEqual(balance.ShopPotionCount, shop.PotionIds.Count, $"種子 {seed}");
                CollectionAssert.AllItemsAreUnique(shop.CardIds);
                CollectionAssert.AllItemsAreUnique(shop.RelicIds);
                CollectionAssert.AllItemsAreUnique(shop.PotionIds);

                for (int i = 0; i < shop.ClassCardCount; i++)
                {
                    Assert.IsFalse(_db.GetCard(shop.CardIds[i]).Colorless, $"種子 {seed}:上排第 {i} 張不該是無色牌");
                }
                for (int i = shop.ClassCardCount; i < shop.CardIds.Count; i++)
                {
                    Assert.IsTrue(_db.GetCard(shop.CardIds[i]).Colorless, $"種子 {seed}:下排第 {i} 張必須是無色牌");
                }
            }
        }

        [Test]
        public void 無色牌_不會出現在戰後卡牌獎勵()
        {
            // 跑多場戰鬥累積樣本:無色牌只在商店賣,獎勵池絕不能撈到
            var engine = RunEngine.NewRun(_db, 31UL);
            for (int round = 0; round < 40; round++)
            {
                var reachable = engine.GetReachableNodeIds();
                if (reachable.Count == 0) break;
                bool entered = false;
                foreach (int nodeId in reachable)
                {
                    if (engine.State.Map.NodeById(nodeId).Type != MapNodeType.Combat) continue;
                    engine.EnterNode(nodeId);
                    entered = true;
                    break;
                }
                if (!entered) break;
                engine.ApplyCombatResult(true, engine.State.Hp);
                foreach (var cardId in engine.PendingRewards.CardChoices)
                {
                    Assert.IsFalse(_db.GetCard(cardId).Colorless, $"無色牌 {cardId} 不該出現在戰後獎勵");
                }
                engine.LeaveRewards();
            }
        }

        [Test]
        public void 燈火_回血三成封頂_或升級卡()
        {
            // 回血(同列節點進過一個後其餘不可達——規則使然,升級用另一個引擎測)
            var healer = 手作引擎(19UL, MapNodeType.Rest);
            healer.State.Hp = 40;
            healer.EnterNode(0);
            healer.RestHeal();
            Assert.AreEqual(40 + 80 * _db.Balance.RestHealPercent / 100, healer.State.Hp);
            Assert.AreEqual(RunPhase.ChoosingNode, healer.State.Phase);

            // 升級
            var upgrader = 手作引擎(21UL, MapNodeType.Rest);
            upgrader.EnterNode(0);
            Assert.IsFalse(upgrader.State.Deck[0].Upgraded);
            upgrader.RestUpgrade(0);
            Assert.IsTrue(upgrader.State.Deck[0].Upgraded);
            Assert.AreEqual(RunPhase.ChoosingNode, upgrader.State.Phase);
        }

        [Test]
        public void 寶箱_遺物自動入包_直接回選節點()
        {
            var engine = 手作引擎(23UL, MapNodeType.Treasure);
            int relicsBefore = engine.State.Relics.Count;
            var entry = engine.EnterNode(0);
            Assert.IsNotNull(entry.TreasureRelicId);
            Assert.AreEqual(relicsBefore + 1, engine.State.Relics.Count);
            Assert.AreEqual(RunPhase.ChoosingNode, engine.State.Phase);
        }

        [Test]
        public void Boss勝利_通關_敗北_GameOver()
        {
            var engine = 手作引擎(29UL, MapNodeType.Combat);
            engine.EnterNode(0);
            engine.ApplyCombatResult(true, 60);
            engine.SkipCardReward();
            engine.LeaveRewards();
            var bossEntry = engine.EnterNode(engine.State.Map.BossNodeId);
            Assert.AreEqual(EncounterPool.Boss, _db.GetEncounter(bossEntry.EncounterId).Pool);
            engine.ApplyCombatResult(true, 30);
            Assert.AreEqual(RunPhase.RunClear, engine.State.Phase);

            var loser = 手作引擎(31UL, MapNodeType.Combat);
            loser.EnterNode(0);
            loser.ApplyCombatResult(false, 0);
            Assert.AreEqual(RunPhase.GameOver, loser.State.Phase);
        }

        [Test]
        public void 整合_真實戰鬥自動爬塔_直到終局不噴例外()
        {
            var engine = RunEngine.NewRun(_db, 20260827UL);
            string pendingEncounterId = null;
            int guard = 0;
            while (engine.State.Phase != RunPhase.GameOver
                && engine.State.Phase != RunPhase.RunClear
                && guard++ < 120)
            {
                switch (engine.State.Phase)
                {
                    case RunPhase.ChoosingNode:
                    {
                        var reachable = engine.GetReachableNodeIds();
                        Assert.Greater(reachable.Count, 0, "選節點階段必有可達節點");
                        var entry = engine.EnterNode(reachable[0]);
                        pendingEncounterId = entry.EncounterId;
                        break;
                    }
                    case RunPhase.InCombat:
                    {
                        Assert.IsNotNull(pendingEncounterId, "進戰鬥階段必有遭遇 id");
                        var combat = new CombatEngine(_db, engine.State.Rng, engine.BuildCombatSetup(pendingEncounterId));
                        自動打完(combat);
                        bool victory = combat.State.Phase == CombatPhase.Victory;
                        engine.ApplyCombatResult(victory, combat.State.Player.Hp);
                        pendingEncounterId = null;
                        break;
                    }
                    case RunPhase.ChoosingReward:
                        // 逐項領取:金幣 → 遺物 → 藥水(欄滿就算了)→ 選卡 → 離開
                        engine.ClaimRewardGold();
                        engine.ClaimRewardRelic();
                        engine.ClaimRewardPotion();
                        if (engine.PendingRewards.HasCard) engine.TakeCardReward(0);
                        engine.LeaveRewards();
                        break;
                    case RunPhase.InShop:
                        engine.LeaveShop();
                        break;
                    case RunPhase.AtRest:
                        engine.RestHeal();
                        break;
                }
            }
            Assert.Less(guard, 120, "爬塔迴圈未在限制內收束");
            Assert.That(engine.State.Phase, Is.EqualTo(RunPhase.GameOver).Or.EqualTo(RunPhase.RunClear));
        }

        private static void 自動打完(CombatEngine engine, int maxRounds = 40)
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
    }
}
