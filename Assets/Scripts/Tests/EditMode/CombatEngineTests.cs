using NUnit.Framework;
using STS.Core.Cards;
using STS.Core.Combat;
using STS.Core.Combat.Statuses;
using static STS.Core.Tests.TestContent;

namespace STS.Core.Tests
{
    /// <summary>CombatEngine 基礎骨架(M1 範圍)的行為測試。</summary>
    public class CombatEngineTests
    {
        [Test]
        public void 開戰_抽五張_能量滿_回合一()
        {
            var engine = 標準引擎(重複("strike", 10));
            engine.StartCombat();
            Assert.AreEqual(CombatPhase.PlayerTurn, engine.State.Phase);
            Assert.AreEqual(1, engine.State.TurnNumber);
            Assert.AreEqual(5, engine.State.Hand.Count);
            Assert.AreEqual(5, engine.State.DrawPile.Count);
            Assert.AreEqual(3, engine.State.Energy);
            Assert.AreEqual(5, 事件數(engine, EventKind.CardDrawn));
        }

        [Test]
        public void 出打擊_敵扣血_能量扣一_卡進棄牌堆()
        {
            var engine = 標準引擎(重複("strike", 10), enemyHp: 20);
            engine.StartCombat();
            engine.PlayCard(0, 0);
            Assert.AreEqual(14, engine.State.Enemies[0].Hp);
            Assert.AreEqual(2, engine.State.Energy);
            Assert.AreEqual(4, engine.State.Hand.Count);
            Assert.AreEqual(1, engine.State.DiscardPile.Count);
        }

        [Test]
        public void 能量用盡_無法再出牌()
        {
            var engine = 標準引擎(重複("strike", 10));
            engine.StartCombat();
            engine.PlayCard(0, 0);
            engine.PlayCard(0, 0);
            engine.PlayCard(0, 0);
            Assert.AreEqual(0, engine.State.Energy);
            Assert.IsFalse(engine.CanPlayCard(0, 0, out string reason));
            Assert.AreEqual("能量不足", reason);
            Assert.Throws<System.InvalidOperationException>(() => engine.PlayCard(0, 0));
        }

        [Test]
        public void 需目標卡_未指定目標_擋下()
        {
            var engine = 標準引擎(重複("strike", 10));
            engine.StartCombat();
            Assert.IsFalse(engine.CanPlayCard(0, -1, out string reason));
            Assert.AreEqual("此卡需要指定敵人目標", reason);
        }

        [Test]
        public void 痛擊_施加易傷_後續攻擊乘一點五()
        {
            var engine = 標準引擎(new[] { "bash", "strike", "strike", "strike", "strike" }, enemyHp: 50);
            engine.StartCombat();
            engine.PlayCard(手牌位置(engine, "bash"), 0);
            Assert.AreEqual(42, engine.State.Enemies[0].Hp);
            Assert.AreEqual(2, engine.State.Enemies[0].GetStatus(StatusId.Vulnerable));
            engine.PlayCard(手牌位置(engine, "strike"), 0);
            // 6 × 1.5 = 9
            Assert.AreEqual(33, engine.State.Enemies[0].Hp);
            Assert.AreEqual(0, engine.State.Energy);
        }

        [Test]
        public void 格擋_吸收後剩餘傷害穿透()
        {
            var engine = 標準引擎(重複("defend", 10), enemyAttack: 9);
            engine.StartCombat();
            engine.PlayCard(0, -1);
            Assert.AreEqual(5, engine.State.Player.Block);
            engine.EndPlayerTurn();
            // 敵攻 9:格擋吸 5,穿透 4
            Assert.AreEqual(76, engine.State.Player.Hp);
        }

        [Test]
        public void 格擋清除時機_活過敵方回合_自己回合開始才清()
        {
            var engine = 標準引擎(重複("defend", 10), enemyAttack: 3);
            engine.StartCombat();
            engine.PlayCard(0, -1);
            engine.PlayCard(0, -1);
            Assert.AreEqual(10, engine.State.Player.Block);
            engine.EndPlayerTurn();
            // 敵攻 3 被 10 格擋完全吸收:血量不動,證明格擋活過敵方回合
            Assert.AreEqual(80, engine.State.Player.Hp);
            // 現在已是下一個玩家回合開頭:格擋歸零,證明清除發生在自己回合開始
            Assert.AreEqual(CombatPhase.PlayerTurn, engine.State.Phase);
            Assert.AreEqual(2, engine.State.TurnNumber);
            Assert.AreEqual(0, engine.State.Player.Block);
            Assert.GreaterOrEqual(事件數(engine, EventKind.BlockCleared), 1);
        }

        [Test]
        public void 空抽牌堆_洗入棄牌堆繼續抽()
        {
            var engine = 標準引擎(重複("strike", 6), enemyHp: 500);
            engine.StartCombat();
            engine.PlayCard(0, 0);
            engine.PlayCard(0, 0);
            engine.PlayCard(0, 0);
            engine.EndPlayerTurn();
            // 回合末棄 2 → 棄 5;新回合抽 5:抽 1 張後堆空 → 洗入 5 張 → 再抽 4
            Assert.AreEqual(5, engine.State.Hand.Count);
            Assert.AreEqual(1, engine.State.DrawPile.Count);
            Assert.AreEqual(0, engine.State.DiscardPile.Count);
            Assert.AreEqual(2, 事件數(engine, EventKind.PileShuffled));
        }

        [Test]
        public void 手牌上限十張_多抽取消()
        {
            var 大抽 = new CardDef
            {
                Id = "bigdraw", Name = "戰術大抽", Type = CardType.Skill, Rarity = CardRarity.Common, Cost = 0,
                Steps = new[] { new EffectStep(EffectOp.Draw, EffectTarget.Self, 10) }
            };
            var ids = new System.Collections.Generic.List<string>(重複("bigdraw", 5));
            ids.AddRange(重複("strike", 15));
            var engine = 標準引擎(ids.ToArray(), enemyHp: 50, enemyAttack: 0, seed: 1UL, playerHp: 80, null, null, 大抽);
            engine.StartCombat();
            int bigdrawIndex = 手牌位置(engine, "bigdraw");
            Assume.That(bigdrawIndex, Is.GreaterThanOrEqualTo(0), "此種子開局手牌應含大抽;若換種子請調整");
            engine.PlayCard(bigdrawIndex, -1);
            // 出牌後手 4,抽到上限 10 就取消剩餘抽牌
            Assert.AreEqual(10, engine.State.Hand.Count);
            Assert.AreEqual(9, engine.State.DrawPile.Count);
        }

        [Test]
        public void 勝利_敵人歸零_進入Victory()
        {
            var engine = 標準引擎(重複("strike", 10), enemyHp: 10);
            engine.StartCombat();
            engine.PlayCard(0, 0);
            engine.PlayCard(0, 0);
            Assert.AreEqual(CombatPhase.Victory, engine.State.Phase);
            Assert.AreEqual(0, engine.State.Enemies[0].Hp);
            Assert.AreEqual(1, 事件數(engine, EventKind.EnemyDied));
            Assert.AreEqual(1, 事件數(engine, EventKind.CombatEnded));
            Assert.IsFalse(engine.CanPlayCard(0, 0, out _));
            Assert.Throws<System.InvalidOperationException>(() => engine.EndPlayerTurn());
        }

        [Test]
        public void 敗北_玩家歸零_進入Defeat()
        {
            var engine = 標準引擎(重複("strike", 10), enemyHp: 50, enemyAttack: 6, playerHp: 5);
            engine.StartCombat();
            engine.EndPlayerTurn();
            Assert.AreEqual(CombatPhase.Defeat, engine.State.Phase);
            Assert.AreEqual(0, engine.State.Player.Hp);
            Assert.AreEqual(1, 事件數(engine, EventKind.CombatEnded));
        }

        [Test]
        public void 消耗卡_進消耗堆不進棄牌堆()
        {
            var 斬除 = new CardDef
            {
                Id = "purge", Name = "斬除", Type = CardType.Attack, Rarity = CardRarity.Common, Cost = 1, Exhausts = true,
                Steps = new[] { new EffectStep(EffectOp.Damage, EffectTarget.TargetEnemy, 3) }
            };
            var engine = 標準引擎(重複("purge", 6), enemyHp: 50, enemyAttack: 0, seed: 1234UL, playerHp: 80, null, null, 斬除);
            engine.StartCombat();
            engine.PlayCard(0, 0);
            Assert.AreEqual(1, engine.State.ExhaustPile.Count);
            Assert.AreEqual(0, engine.State.DiscardPile.Count);
            Assert.AreEqual(1, 事件數(engine, EventKind.CardExhausted));
        }

        [Test]
        public void 決定性_同種子同開局手牌()
        {
            var first = 標準引擎(重複("strike", 10), seed: 777UL);
            var second = 標準引擎(重複("strike", 10), seed: 777UL);
            first.StartCombat();
            second.StartCombat();
            for (int i = 0; i < 5; i++)
            {
                Assert.AreEqual(first.State.Hand[i].InstanceId, second.State.Hand[i].InstanceId);
            }
        }

        [Test]
        public void 重複開戰_拋錯()
        {
            var engine = 標準引擎(重複("strike", 10));
            engine.StartCombat();
            Assert.Throws<System.InvalidOperationException>(() => engine.StartCombat());
        }
    }
}
