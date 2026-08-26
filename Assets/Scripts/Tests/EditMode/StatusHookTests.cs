using NUnit.Framework;
using STS.Core.Cards;
using STS.Core.Combat;
using STS.Core.Combat.Enemies;
using STS.Core.Combat.Statuses;
using static STS.Core.Tests.TestContent;

namespace STS.Core.Tests
{
    /// <summary>狀態衰減時序(JustApplied 語意)與遺物/hook 系統的行為測試。</summary>
    public class StatusHookTests
    {
        // ---- 衰減時序 ----

        [Test]
        public void 易傷_玩家施加給敵_該敵回合末就衰減()
        {
            var engine = 標準引擎(new[] { "bash", "strike", "strike", "strike", "strike" });
            engine.StartCombat();
            engine.PlayCard(手牌位置(engine, "bash"), 0);
            Assert.AreEqual(2, engine.State.Enemies[0].GetStatus(StatusId.Vulnerable));
            engine.EndPlayerTurn();
            // 對手回合中施加的計時狀態不跳過首次衰減:敵回合末 2 → 1
            Assert.AreEqual(1, engine.State.Enemies[0].GetStatus(StatusId.Vulnerable));
        }

        [Test]
        public void 虛弱_敵施加給玩家_玩家回合末衰減()
        {
            var 詛咒者 = new EnemyDef
            {
                Id = "curser", Name = "詛咒者", HpMin = 50, HpMax = 50, Ai = AiKind.Loop,
                Moves = new[]
                {
                    new MoveDef { Id = "curse", Name = "詛咒", Intent = IntentType.Debuff,
                        Steps = new[] { new EffectStep(EffectOp.ApplyStatus, EffectTarget.TargetEnemy, 2, status: StatusId.Weak) } },
                    new MoveDef { Id = "idle", Name = "待機", Intent = IntentType.Special }
                },
                OpeningScript = new[] { "curse" },
                LoopScript = new[] { "idle" }
            };
            var db = 基礎DB();
            db.Enemies["curser"] = 詛咒者;
            var setup = 基礎Setup(重複("strike", 8));
            setup.EnemyIds.Add("curser");
            var engine = 引擎(db, setup);
            engine.StartCombat();

            engine.EndPlayerTurn();   // 回合1末:敵施加虛弱2(玩家衰減已過,不跳過旗標→之後每個玩家回合末 -1)
            Assert.AreEqual(2, engine.State.Player.GetStatus(StatusId.Weak));
            engine.EndPlayerTurn();   // 回合2末:2 → 1
            Assert.AreEqual(1, engine.State.Player.GetStatus(StatusId.Weak));
            engine.EndPlayerTurn();   // 回合3末:1 → 0
            Assert.AreEqual(0, engine.State.Player.GetStatus(StatusId.Weak));
        }

        [Test]
        public void 自己回合施加給自己_首次衰減跳過()
        {
            var 自弱 = new CardDef
            {
                Id = "selfweak", Name = "冒進", Type = CardType.Skill, Rarity = CardRarity.Common, Cost = 0,
                Steps = new[] { new EffectStep(EffectOp.ApplyStatus, EffectTarget.Self, 1, status: StatusId.Weak) }
            };
            var engine = 標準引擎(重複("selfweak", 8), enemyHp: 50, enemyAttack: 0, seed: 1234UL, playerHp: 80, null, null, 自弱);
            engine.StartCombat();
            engine.PlayCard(0, -1);
            Assert.AreEqual(1, engine.State.Player.GetStatus(StatusId.Weak));
            engine.EndPlayerTurn();
            // 自己回合施加:首次衰減跳過,仍為 1
            Assert.AreEqual(1, engine.State.Player.GetStatus(StatusId.Weak));
            engine.EndPlayerTurn();
            Assert.AreEqual(0, engine.State.Player.GetStatus(StatusId.Weak));
        }

        [Test]
        public void 禁抽_擋掉當回合抽牌_回合末移除()
        {
            var 莽撞 = new CardDef
            {
                Id = "reckless", Name = "莽撞衝鋒", Type = CardType.Skill, Rarity = CardRarity.Common, Cost = 0,
                Steps = new[]
                {
                    new EffectStep(EffectOp.ApplyStatus, EffectTarget.Self, 1, status: StatusId.NoDraw),
                    new EffectStep(EffectOp.Draw, EffectTarget.Self, 2)
                }
            };
            var engine = 標準引擎(重複("reckless", 8), enemyHp: 50, enemyAttack: 0, seed: 1234UL, playerHp: 80, null, null, 莽撞);
            engine.StartCombat();
            engine.PlayCard(0, -1);
            // 禁抽先掛上,後續 Draw 2 被擋:手牌 5-1=4
            Assert.AreEqual(4, engine.State.Hand.Count);
            Assert.AreEqual(1, engine.State.Player.GetStatus(StatusId.NoDraw));
            engine.EndPlayerTurn();
            // 回合末移除,新回合抽牌恢復
            Assert.AreEqual(0, engine.State.Player.GetStatus(StatusId.NoDraw));
            Assert.AreEqual(5, engine.State.Hand.Count);
        }

        [Test]
        public void 失力_回合末扣力量並自我移除()
        {
            var 蠻力 = new CardDef
            {
                Id = "flex", Name = "鼓足蠻力", Type = CardType.Skill, Rarity = CardRarity.Common, Cost = 0,
                Steps = new[]
                {
                    new EffectStep(EffectOp.ApplyStatus, EffectTarget.Self, 2, status: StatusId.Strength),
                    new EffectStep(EffectOp.ApplyStatus, EffectTarget.Self, 2, status: StatusId.LoseStrengthAtTurnEnd)
                }
            };
            var engine = 標準引擎(重複("flex", 8), enemyHp: 50, enemyAttack: 0, seed: 1234UL, playerHp: 80, null, null, 蠻力);
            engine.StartCombat();
            engine.PlayCard(0, -1);
            Assert.AreEqual(2, engine.State.Player.GetStatus(StatusId.Strength));
            engine.EndPlayerTurn();
            Assert.AreEqual(0, engine.State.Player.GetStatus(StatusId.Strength));
            Assert.AreEqual(0, engine.State.Player.GetStatus(StatusId.LoseStrengthAtTurnEnd));
        }

        // ---- 遺物 ----

        [Test]
        public void 船錨_開戰格擋十_不被開戰清除吃掉()
        {
            var engine = 標準引擎(重複("strike", 10), relicIds: new[] { "anchor" });
            engine.StartCombat();
            Assert.AreEqual(10, engine.State.Player.Block);
        }

        [Test]
        public void 金剛杵_開戰力量一_打擊變七()
        {
            var engine = 標準引擎(重複("strike", 10), enemyHp: 50, relicIds: new[] { "vajra" });
            engine.StartCombat();
            engine.PlayCard(0, 0);
            Assert.AreEqual(43, engine.State.Enemies[0].Hp);
        }

        [Test]
        public void 彈珠袋_開戰全敵易傷一()
        {
            var engine = 標準引擎(重複("strike", 10), enemyHp: 50, relicIds: new[] { "bag_of_marbles" });
            engine.StartCombat();
            Assert.AreEqual(1, engine.State.Enemies[0].GetStatus(StatusId.Vulnerable));
            engine.PlayCard(0, 0);
            // 6 × 1.5 = 9
            Assert.AreEqual(41, engine.State.Enemies[0].Hp);
        }

        [Test]
        public void 備戰袋_開局共抽七張()
        {
            var engine = 標準引擎(重複("strike", 10), relicIds: new[] { "bag_of_preparation" });
            engine.StartCombat();
            Assert.AreEqual(7, engine.State.Hand.Count);
            Assert.AreEqual(3, engine.State.DrawPile.Count);
        }

        [Test]
        public void 血瓶開戰回血_燃燒之血勝利回血()
        {
            // 受傷開局(50/80)才看得出治療效果
            var db = 基礎DB();
            db.Enemies["dummy"] = 木樁(hp: 10);
            var setup = 基礎Setup(重複("strike", 10), playerHp: 50, playerMaxHp: 80,
                relicIds: new[] { "blood_vial", "burning_blood" });
            setup.EnemyIds.Add("dummy");
            var engine = 引擎(db, setup);
            engine.StartCombat();
            // 血瓶:開戰 +2
            Assert.AreEqual(52, engine.State.Player.Hp);
            engine.PlayCard(0, 0);
            engine.PlayCard(0, 0);
            // 燃燒之血:勝利 +6
            Assert.AreEqual(CombatPhase.Victory, engine.State.Phase);
            Assert.AreEqual(58, engine.State.Player.Hp);
        }

        [Test]
        public void 山銅_回合末無格擋補六_活過敵回合()
        {
            var engine = 標準引擎(重複("strike", 10), enemyHp: 50, enemyAttack: 4, relicIds: new[] { "orichalcum" });
            engine.StartCombat();
            engine.EndPlayerTurn();
            // 回合末山銅 +6,敵攻 4 全吸收:血量不動
            Assert.AreEqual(80, engine.State.Player.Hp);
        }

        [Test]
        public void 雙節棍_第十張攻擊牌回能()
        {
            var engine = 標準引擎(重複("strike", 20), enemyHp: 500, relicIds: new[] { "nunchaku" });
            engine.StartCombat();
            for (int turn = 0; turn < 3; turn++)
            {
                engine.PlayCard(0, 0);
                engine.PlayCard(0, 0);
                engine.PlayCard(0, 0);
                engine.EndPlayerTurn();
            }
            // 已打出 9 張攻擊;第 10 張觸發 +1 能量:付 1 退 1,能量停在 3
            Assert.AreEqual(3, engine.State.Energy);
            engine.PlayCard(0, 0);
            Assert.AreEqual(3, engine.State.Energy);
            Assert.AreEqual(0, engine.Relics[0].Counter);
        }

        [Test]
        public void 青銅鱗片_受攻擊反傷三()
        {
            var engine = 標準引擎(重複("strike", 10), enemyHp: 30, enemyAttack: 5, relicIds: new[] { "bronze_scales" });
            engine.StartCombat();
            engine.EndPlayerTurn();
            Assert.AreEqual(75, engine.State.Player.Hp);
            Assert.AreEqual(27, engine.State.Enemies[0].Hp);
        }

        [Test]
        public void 提燈_首回合能量四()
        {
            var engine = 標準引擎(重複("strike", 10), relicIds: new[] { "lantern" });
            engine.StartCombat();
            Assert.AreEqual(4, engine.State.Energy);
            engine.EndPlayerTurn();
            Assert.AreEqual(3, engine.State.Energy);
        }

        [Test]
        public void hook順序_遺物先於狀態_山銅先看到零格擋()
        {
            var 金屬化 = new CardDef
            {
                Id = "metal", Name = "金屬化身", Type = CardType.Power, Rarity = CardRarity.Uncommon, Cost = 1,
                Steps = new[] { new EffectStep(EffectOp.ApplyStatus, EffectTarget.Self, 3, status: StatusId.Metallicize) }
            };
            var engine = 標準引擎(重複("metal", 8), enemyHp: 50, enemyAttack: 8, seed: 1234UL, playerHp: 80,
                new[] { "orichalcum" }, null, 金屬化);
            engine.StartCombat();
            engine.PlayCard(0, -1);
            engine.EndPlayerTurn();
            // 遺物先觸發:山銅看到格擋 0 → +6;再輪到狀態:金屬化 +3 → 9;敵攻 8 全吸收
            Assert.AreEqual(80, engine.State.Player.Hp);
            // 順序證據:先 6 後 3 的兩筆 BlockGained
            int firstGain = -1, secondGain = -1;
            for (int i = 0; i < engine.Events.Count; i++)
            {
                if (engine.Events[i].Kind != EventKind.BlockGained) continue;
                if (firstGain < 0) firstGain = engine.Events[i].Amount;
                else { secondGain = engine.Events[i].Amount; break; }
            }
            Assert.AreEqual(6, firstGain);
            Assert.AreEqual(3, secondGain);
        }
    }
}
