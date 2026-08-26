using NUnit.Framework;
using STS.Core.Cards;
using STS.Core.Combat;
using STS.Core.Combat.Statuses;
using STS.Core.Potions;
using static STS.Core.Tests.TestContent;

namespace STS.Core.Tests
{
    /// <summary>M2 卡牌機制:多段攻擊、X 費、數值換算、中斷選擇、狀態卡、能力卡、藥水。</summary>
    public class CardMechanicsTests
    {
        [Test]
        public void 多段攻擊_逐段扣格擋()
        {
            var db = 基礎DB();
            var 雙擊敵 = 木樁(hp: 50);
            雙擊敵.Moves = new[]
            {
                new Core.Combat.Enemies.MoveDef
                {
                    Id = "double", Name = "連擊", Intent = Core.Combat.Enemies.IntentType.Attack,
                    Steps = new[] { new EffectStep(EffectOp.Damage, EffectTarget.TargetEnemy, 4, repeat: 2) }
                }
            };
            雙擊敵.LoopScript = new[] { "double" };
            db.Enemies["dummy"] = 雙擊敵;
            var setup = 基礎Setup(重複("defend", 10));
            setup.EnemyIds.Add("dummy");
            var engine = 引擎(db, setup);
            engine.StartCombat();
            engine.PlayCard(0, -1);   // 格擋 5
            engine.EndPlayerTurn();
            // 4×2 逐段結算:第一段吃格擋 4(剩1),第二段吃 1、穿 3
            Assert.AreEqual(77, engine.State.Player.Hp);
        }

        [Test]
        public void 旋風斬_X費_段數等於消耗能量_全體()
        {
            var 旋風 = new CardDef
            {
                Id = "whirl", Name = "旋風連斬", Type = CardType.Attack, Rarity = CardRarity.Uncommon, CostIsX = true,
                Steps = new[] { new EffectStep(EffectOp.Damage, EffectTarget.AllEnemies, 5, repeatIsX: true) }
            };
            var db = 基礎DB(旋風);
            db.Enemies["a"] = 木樁(id: "a", hp: 50);
            db.Enemies["b"] = 木樁(id: "b", hp: 50);
            var setup = 基礎Setup(重複("whirl", 8));
            setup.EnemyIds.Add("a");
            setup.EnemyIds.Add("b");
            var engine = 引擎(db, setup);
            engine.StartCombat();
            engine.PlayCard(0, -1);
            // 能量 3 → 三段,兩敵各 -15;能量歸零
            Assert.AreEqual(35, engine.State.Enemies[0].Hp);
            Assert.AreEqual(35, engine.State.Enemies[1].Hp);
            Assert.AreEqual(0, engine.State.Energy);
            // 0 能量再打:合法,零段
            engine.PlayCard(0, -1);
            Assert.AreEqual(35, engine.State.Enemies[0].Hp);
            Assert.AreEqual(35, engine.State.Enemies[1].Hp);
        }

        [Test]
        public void 重刃_力量以三倍計()
        {
            var 重刃 = new CardDef
            {
                Id = "heavy", Name = "千鈞一擊", Type = CardType.Attack, Rarity = CardRarity.Common, Cost = 2,
                Steps = new[]
                {
                    new EffectStep(EffectOp.Damage, EffectTarget.TargetEnemy, 14,
                        amountKind: AmountKind.StrengthTimes, secondaryAmount: 3)
                }
            };
            // 三顆金剛杵 → 開戰力量 3
            var engine = 標準引擎(重複("heavy", 8), enemyHp: 50, enemyAttack: 0, seed: 1234UL, playerHp: 80,
                new[] { "vajra", "vajra", "vajra" }, null, 重刃);
            engine.StartCombat();
            engine.PlayCard(0, 0);
            // 14 + 3×3 = 23
            Assert.AreEqual(27, engine.State.Enemies[0].Hp);
        }

        [Test]
        public void 全身撞擊_傷害等於當前格擋()
        {
            var 撞擊 = new CardDef
            {
                Id = "slam", Name = "全身撞擊", Type = CardType.Attack, Rarity = CardRarity.Uncommon, Cost = 1,
                Steps = new[]
                {
                    new EffectStep(EffectOp.Damage, EffectTarget.TargetEnemy, 0, amountKind: AmountKind.CurrentBlock)
                }
            };
            // 船錨 → 開戰格擋 10
            var engine = 標準引擎(重複("slam", 8), enemyHp: 50, enemyAttack: 0, seed: 1234UL, playerHp: 80,
                new[] { "anchor" }, null, 撞擊);
            engine.StartCombat();
            engine.PlayCard(0, 0);
            Assert.AreEqual(40, engine.State.Enemies[0].Hp);
        }

        [Test]
        public void 燃燒契約_中斷選擇_消耗一張後抽二()
        {
            var 契約 = new CardDef
            {
                Id = "pact", Name = "燃燒契約", Type = CardType.Skill, Rarity = CardRarity.Uncommon, Cost = 1,
                Steps = new[]
                {
                    new EffectStep(EffectOp.ChooseExhaustFromHand, EffectTarget.Self, 1),
                    new EffectStep(EffectOp.Draw, EffectTarget.Self, 2)
                }
            };
            var engine = 標準引擎(重複("pact", 10), enemyHp: 50, enemyAttack: 0, seed: 1234UL, playerHp: 80, null, null, 契約);
            engine.StartCombat();
            engine.PlayCard(0, -1);
            Assert.AreEqual(CombatPhase.AwaitingChoice, engine.State.Phase);
            Assert.AreEqual(1, engine.State.PendingChoiceCount);
            Assert.AreEqual(1, 事件數(engine, EventKind.ChoiceRequired));
            Assert.AreEqual(4, engine.State.Hand.Count);
            Assert.Throws<System.InvalidOperationException>(() => engine.EndPlayerTurn());   // 中斷期間拒收其他指令

            engine.ResolveChoice(new[] { 0 });
            // 消耗 1(手 3)→ 抽 2(手 5);契約本體進棄牌堆
            Assert.AreEqual(CombatPhase.PlayerTurn, engine.State.Phase);
            Assert.AreEqual(5, engine.State.Hand.Count);
            Assert.AreEqual(1, engine.State.ExhaustPile.Count);
            Assert.AreEqual(1, engine.State.DiscardPile.Count);
        }

        [Test]
        public void 燒傷_回合末在手直接扣血_然後進棄牌堆()
        {
            var 燒傷 = new CardDef
            {
                Id = "burn", Name = "燒傷", Type = CardType.Status, Rarity = CardRarity.Common, Unplayable = true,
                TurnEndInHandSteps = new[] { new EffectStep(EffectOp.LoseHp, EffectTarget.Self, 2) }
            };
            var db = 基礎DB(燒傷);
            var 縱火敵 = 木樁(hp: 50);
            縱火敵.Moves = new[]
            {
                new Core.Combat.Enemies.MoveDef
                {
                    Id = "ignite", Name = "縱火", Intent = Core.Combat.Enemies.IntentType.Debuff,
                    Steps = new[] { new EffectStep(EffectOp.AddCardToPile, EffectTarget.Self, cardId: "burn", pile: PileType.Hand) }
                },
                new Core.Combat.Enemies.MoveDef { Id = "idle", Name = "待機", Intent = Core.Combat.Enemies.IntentType.Special }
            };
            縱火敵.OpeningScript = new[] { "ignite" };
            縱火敵.LoopScript = new[] { "idle" };
            db.Enemies["dummy"] = 縱火敵;
            var setup = 基礎Setup(重複("strike", 8));
            setup.EnemyIds.Add("dummy");
            var engine = 引擎(db, setup);
            engine.StartCombat();

            engine.EndPlayerTurn();   // 回合1末:敵把燒傷塞進手牌
            Assert.AreEqual(6, engine.State.Hand.Count);   // 新回合 5 + 燒傷
            int burnIndex = 手牌位置(engine, "burn");
            Assert.GreaterOrEqual(burnIndex, 0);
            Assert.IsFalse(engine.CanPlayCard(burnIndex, -1, out string reason));
            Assert.AreEqual("此卡不可打出", reason);

            engine.EndPlayerTurn();   // 回合2末:燒傷觸發 -2,之後照常棄掉(會隨洗牌循環回來,不斷言位置)
            Assert.AreEqual(78, engine.State.Player.Hp);
            Assert.AreEqual(1, 事件數(engine, EventKind.HpLost));
        }

        [Test]
        public void 虛無卡_回合末在手直接消耗()
        {
            var 暈眩 = new CardDef
            {
                Id = "dazed", Name = "暈眩", Type = CardType.Status, Rarity = CardRarity.Common,
                Unplayable = true, Ethereal = true
            };
            var db = 基礎DB(暈眩);
            var 哨衛 = 木樁(hp: 50);
            哨衛.Moves = new[]
            {
                new Core.Combat.Enemies.MoveDef
                {
                    Id = "beam", Name = "光束", Intent = Core.Combat.Enemies.IntentType.Debuff,
                    Steps = new[] { new EffectStep(EffectOp.AddCardToPile, EffectTarget.Self, cardId: "dazed", pile: PileType.Hand) }
                },
                new Core.Combat.Enemies.MoveDef { Id = "idle", Name = "待機", Intent = Core.Combat.Enemies.IntentType.Special }
            };
            哨衛.OpeningScript = new[] { "beam" };
            哨衛.LoopScript = new[] { "idle" };
            db.Enemies["dummy"] = 哨衛;
            var setup = 基礎Setup(重複("strike", 8));
            setup.EnemyIds.Add("dummy");
            var engine = 引擎(db, setup);
            engine.StartCombat();

            engine.EndPlayerTurn();   // 塞入暈眩
            Assume.That(手牌位置(engine, "dazed"), Is.GreaterThanOrEqualTo(0));
            engine.EndPlayerTurn();   // 回合末:暈眩消耗、不進棄牌堆
            Assert.AreEqual(1, engine.State.ExhaustPile.Count);
            Assert.AreEqual("dazed", engine.State.ExhaustPile[0].CardId);
        }

        [Test]
        public void 能力卡_不進牌堆_每回合開始生效()
        {
            var 惡魔化 = new CardDef
            {
                Id = "demon", Name = "惡魔化身", Type = CardType.Power, Rarity = CardRarity.Rare, Cost = 3,
                Steps = new[] { new EffectStep(EffectOp.ApplyStatus, EffectTarget.Self, 2, status: StatusId.DemonForm) }
            };
            var engine = 標準引擎(重複("demon", 8), enemyHp: 50, enemyAttack: 0, seed: 1234UL, playerHp: 80, null, null, 惡魔化);
            engine.StartCombat();
            engine.PlayCard(0, -1);
            Assert.AreEqual(1, engine.State.PowersPlayed.Count);
            Assert.AreEqual(0, engine.State.DiscardPile.Count);
            Assert.AreEqual(2, engine.State.Player.GetStatus(StatusId.DemonForm));
            Assert.AreEqual(0, engine.State.Player.GetStatus(StatusId.Strength));

            engine.EndPlayerTurn();   // 回合2開始:+2 力量
            Assert.AreEqual(2, engine.State.Player.GetStatus(StatusId.Strength));
            engine.EndPlayerTurn();   // 回合3開始:再 +2
            Assert.AreEqual(4, engine.State.Player.GetStatus(StatusId.Strength));
        }

        [Test]
        public void 藥水_火焰二十點_用完欄位清空()
        {
            var db = 基礎DB();
            db.Enemies["dummy"] = 木樁(hp: 50);
            db.Potions["fire"] = new PotionDef
            {
                Id = "fire", Name = "火焰藥水", NeedsTarget = true,
                Steps = new[] { new EffectStep(EffectOp.Damage, EffectTarget.TargetEnemy, 20) }
            };
            var setup = 基礎Setup(重複("strike", 10), potionIds: new[] { "fire" });
            setup.EnemyIds.Add("dummy");
            var engine = 引擎(db, setup);
            engine.StartCombat();
            engine.UsePotion(0, 0);
            Assert.AreEqual(30, engine.State.Enemies[0].Hp);
            Assert.IsNull(engine.State.PotionSlots[0]);
            Assert.Throws<System.InvalidOperationException>(() => engine.UsePotion(0, 0));
        }

        [Test]
        public void 隨機目標_三段共十二點_全落在活敵身上()
        {
            var 亂射 = new CardDef
            {
                Id = "spray", Name = "亂射", Type = CardType.Attack, Rarity = CardRarity.Common, Cost = 1,
                Steps = new[] { new EffectStep(EffectOp.Damage, EffectTarget.RandomEnemy, 4, repeat: 3) }
            };
            var db = 基礎DB(亂射);
            db.Enemies["a"] = 木樁(id: "a", hp: 50);
            db.Enemies["b"] = 木樁(id: "b", hp: 50);
            var setup = 基礎Setup(重複("spray", 8));
            setup.EnemyIds.Add("a");
            setup.EnemyIds.Add("b");
            var engine = 引擎(db, setup);
            engine.StartCombat();
            engine.PlayCard(0, -1);
            int totalLost = (50 - engine.State.Enemies[0].Hp) + (50 - engine.State.Enemies[1].Hp);
            Assert.AreEqual(12, totalLost);
        }
    }
}
