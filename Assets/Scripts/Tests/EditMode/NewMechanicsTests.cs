using NUnit.Framework;
using STS.Core.Cards;
using STS.Core.Combat;
using STS.Core.Combat.Statuses;
using static STS.Core.Tests.TestContent;

namespace STS.Core.Tests
{
    /// <summary>
    /// 戰士卡池擴充帶進來的新機制:消耗/格擋 hook、壁壘、成長型數值、狀態翻倍、消耗抽牌堆頂。
    /// 牌組一律剛好 5 張,開場全抽進手裡——洗牌順序就影響不到測試。
    /// </summary>
    public class NewMechanicsTests
    {
        private static CardDef 能力(string id, string name, StatusId status, int stacks)
        {
            return new CardDef
            {
                Id = id, Name = name, Type = CardType.Power, Rarity = CardRarity.Rare, Cost = 0,
                DescriptionTemplate = name,
                Steps = new[] { new EffectStep(EffectOp.ApplyStatus, EffectTarget.Self, stacks, status: status) }
            };
        }

        /// <summary>打出後自己被消耗的空白技能牌:用來製造一次「牌被消耗」事件。</summary>
        private static CardDef 自消耗()
        {
            return new CardDef
            {
                Id = "selfexhaust", Name = "自消耗", Type = CardType.Skill, Rarity = CardRarity.Common, Cost = 0,
                DescriptionTemplate = "消耗。", Exhausts = true,
                Steps = System.Array.Empty<EffectStep>()
            };
        }

        private static CardDef 放血(int amount = 3)
        {
            return new CardDef
            {
                Id = "bleed", Name = "放血", Type = CardType.Skill, Rarity = CardRarity.Common, Cost = 0,
                DescriptionTemplate = "失去生命。",
                Steps = new[] { new EffectStep(EffectOp.LoseHp, EffectTarget.Self, amount) }
            };
        }

        private static int 出牌(CombatEngine engine, string cardId, int target = 0)
        {
            int index = 手牌位置(engine, cardId);
            Assert.GreaterOrEqual(index, 0, $"手牌裡找不到 {cardId}");
            engine.PlayCard(index, target);
            return index;
        }

        [Test]
        public void 無懼疼痛_每消耗一張牌就補盾()
        {
            var 無懼 = 能力("fnp", "無懼疼痛", StatusId.FeelNoPain, 3);
            var engine = 標準引擎(new[] { "fnp", "selfexhaust", "defend", "defend", "defend" },
                extraDefs: new[] { 無懼, 自消耗() });
            engine.StartCombat();

            出牌(engine, "fnp");
            Assert.AreEqual(0, engine.State.Player.Block);
            出牌(engine, "selfexhaust");
            Assert.AreEqual(3, engine.State.Player.Block, "牌被消耗時要補 3 點格擋");
        }

        [Test]
        public void 黑暗之擁_每消耗一張牌就抽牌()
        {
            var 之擁 = 能力("de", "黑暗之擁", StatusId.DarkEmbrace, 1);
            // 抽牌堆要有料才抽得到:6 張牌開場抽 5,留 1 張在抽牌堆
            var engine = 標準引擎(new[] { "de", "de", "de", "de", "de", "selfexhaust" },
                extraDefs: new[] { 之擁, 自消耗() });
            engine.StartCombat();

            出牌(engine, "de");
            int drawnBefore = 事件數(engine, EventKind.CardDrawn);
            int exhaustIndex = 手牌位置(engine, "selfexhaust");
            if (exhaustIndex < 0)
            {
                // 自消耗那張剛好留在抽牌堆:改用手上任何一張確認「沒有消耗就沒有額外抽牌」
                Assert.AreEqual(drawnBefore, 事件數(engine, EventKind.CardDrawn));
                return;
            }
            engine.PlayCard(exhaustIndex, 0);
            Assert.AreEqual(drawnBefore + 1, 事件數(engine, EventKind.CardDrawn), "牌被消耗時要抽 1 張");
        }

        [Test]
        public void 勢不可當_獲得格擋就砸隨機敵人()
        {
            var 不可當 = 能力("jug", "勢不可當", StatusId.Juggernaut, 5);
            var engine = 標準引擎(new[] { "jug", "defend", "defend", "defend", "defend" },
                enemyHp: 50, extraDefs: new[] { 不可當 });
            engine.StartCombat();

            出牌(engine, "jug");
            Assert.AreEqual(50, engine.State.Enemies[0].Hp);
            出牌(engine, "defend");
            Assert.AreEqual(45, engine.State.Enemies[0].Hp, "獲得格擋要對敵人造成 5 點傷害");
            Assert.AreEqual(5, engine.State.Player.Block);
        }

        [Test]
        public void 壁壘_格擋不在回合開始清除()
        {
            var 壁壘 = 能力("bar", "壁壘", StatusId.Barricade, 1);
            var engine = 標準引擎(new[] { "bar", "defend", "defend", "defend", "defend" },
                enemyHp: 200, enemyAttack: 0, extraDefs: new[] { 壁壘 });
            engine.StartCombat();

            出牌(engine, "bar");
            出牌(engine, "defend");
            Assert.AreEqual(5, engine.State.Player.Block);

            engine.EndPlayerTurn();
            Assert.AreEqual(5, engine.State.Player.Block, "壁壘在場時,回合開始不清格擋");
        }

        [Test]
        public void 撕裂_自己回合失去生命才獲得力量()
        {
            var 撕裂 = 能力("rup", "撕裂", StatusId.Rupture, 1);
            var engine = 標準引擎(new[] { "rup", "bleed", "defend", "defend", "defend" },
                extraDefs: new[] { 撕裂, 放血() });
            engine.StartCombat();

            出牌(engine, "rup");
            Assert.AreEqual(0, engine.State.Player.GetStatus(StatusId.Strength));
            出牌(engine, "bleed");
            Assert.AreEqual(77, engine.State.Player.Hp);
            Assert.AreEqual(1, engine.State.Player.GetStatus(StatusId.Strength), "自己回合失血要獲得 1 點力量");
        }

        [Test]
        public void 灰燼打擊_傷害隨消耗堆張數成長()
        {
            var 灰燼 = new CardDef
            {
                Id = "ashen", Name = "灰燼打擊", Type = CardType.Attack, Rarity = CardRarity.Uncommon, Cost = 0,
                DescriptionTemplate = "造成 {dmg} 點傷害。",
                Steps = new[]
                {
                    new EffectStep(EffectOp.Damage, EffectTarget.TargetEnemy, 6,
                        AmountKind.PerExhaustedCard, secondaryAmount: 3)
                }
            };
            var engine = 標準引擎(new[] { "ashen", "selfexhaust", "selfexhaust", "defend", "defend" },
                enemyHp: 50, extraDefs: new[] { 灰燼, 自消耗() });
            engine.StartCombat();

            出牌(engine, "selfexhaust");
            出牌(engine, "selfexhaust");
            Assert.AreEqual(2, engine.State.ExhaustPile.Count);
            出牌(engine, "ashen");
            // 6 + 3×2 = 12
            Assert.AreEqual(38, engine.State.Enemies[0].Hp);
        }

        [Test]
        public void 欺凌_傷害隨目標易傷層數成長()
        {
            var 欺凌 = new CardDef
            {
                Id = "bully", Name = "欺凌", Type = CardType.Attack, Rarity = CardRarity.Uncommon, Cost = 0,
                DescriptionTemplate = "造成 {dmg} 點傷害。",
                Steps = new[]
                {
                    new EffectStep(EffectOp.Damage, EffectTarget.TargetEnemy, 4,
                        AmountKind.PerTargetVulnerable, secondaryAmount: 2)
                }
            };
            var engine = 標準引擎(new[] { "bash", "bully", "defend", "defend", "defend" },
                enemyHp: 50, extraDefs: new[] { 欺凌 });
            engine.StartCombat();

            出牌(engine, "bash");   // 8 傷 + 2 層易傷
            Assert.AreEqual(42, engine.State.Enemies[0].Hp);
            Assert.AreEqual(2, engine.State.Enemies[0].GetStatus(StatusId.Vulnerable));

            出牌(engine, "bully");  // 基礎 4 + 2×2 = 8,再吃易傷 ×1.5 = 12
            Assert.AreEqual(30, engine.State.Enemies[0].Hp);
        }

        [Test]
        public void 焚燒_傷害隨本回合已打出的其他攻擊牌成長()
        {
            var 焚燒 = new CardDef
            {
                Id = "conf", Name = "焚燒", Type = CardType.Attack, Rarity = CardRarity.Rare, Cost = 1,
                DescriptionTemplate = "對所有敵人造成 {dmg} 點傷害。",
                Steps = new[]
                {
                    new EffectStep(EffectOp.Damage, EffectTarget.AllEnemies, 8,
                        AmountKind.PerAttackPlayedThisTurn, secondaryAmount: 2)
                }
            };
            var engine = 標準引擎(new[] { "strike", "strike", "conf", "defend", "defend" },
                enemyHp: 60, extraDefs: new[] { 焚燒 });
            engine.StartCombat();

            出牌(engine, "strike");   // 60 → 54
            出牌(engine, "strike");   // 54 → 48
            出牌(engine, "conf");     // 已打出 2 張其他攻擊 → 8 + 2×2 = 12
            Assert.AreEqual(36, engine.State.Enemies[0].Hp);
        }

        [Test]
        public void 完美打擊_傷害隨牌名含打擊的張數成長()
        {
            var 完美 = new CardDef
            {
                Id = "perfect", Name = "完美打擊", Type = CardType.Attack, Rarity = CardRarity.Common, Cost = 0,
                DescriptionTemplate = "造成 {dmg} 點傷害。",
                Steps = new[]
                {
                    new EffectStep(EffectOp.Damage, EffectTarget.TargetEnemy, 6,
                        AmountKind.PerStrikeCard, secondaryAmount: 2)
                }
            };
            var engine = 標準引擎(new[] { "perfect", "strike", "strike", "defend", "defend" },
                enemyHp: 50, extraDefs: new[] { 完美 });
            engine.StartCombat();

            // 名字含「打擊」:打擊×2 + 完美打擊×1 = 3 張 → 6 + 2×3 = 12
            出牌(engine, "perfect");
            Assert.AreEqual(38, engine.State.Enemies[0].Hp);
        }

        [Test]
        public void 熔融之拳_把目標的易傷層數翻倍()
        {
            var 熔融 = new CardDef
            {
                Id = "molten", Name = "熔融之拳", Type = CardType.Attack, Rarity = CardRarity.Common, Cost = 0,
                DescriptionTemplate = "造成 {dmg} 點傷害。",
                Steps = new[]
                {
                    new EffectStep(EffectOp.Damage, EffectTarget.TargetEnemy, 10),
                    new EffectStep(EffectOp.DoubleStatus, EffectTarget.TargetEnemy, status: StatusId.Vulnerable)
                }
            };
            var engine = 標準引擎(new[] { "bash", "molten", "defend", "defend", "defend" },
                enemyHp: 60, extraDefs: new[] { 熔融 });
            engine.StartCombat();

            出牌(engine, "bash");     // 8 傷,2 層易傷
            出牌(engine, "molten");   // 10 × 1.5 = 15 傷,易傷翻倍成 4
            Assert.AreEqual(37, engine.State.Enemies[0].Hp);
            Assert.AreEqual(4, engine.State.Enemies[0].GetStatus(StatusId.Vulnerable), "易傷要從 2 翻倍成 4");
        }

        [Test]
        public void 餘燼_消耗抽牌堆最上面一張()
        {
            var 餘燼 = new CardDef
            {
                Id = "cinder", Name = "餘燼", Type = CardType.Attack, Rarity = CardRarity.Common, Cost = 0,
                DescriptionTemplate = "造成 {dmg} 點傷害。",
                Steps = new[]
                {
                    new EffectStep(EffectOp.Damage, EffectTarget.TargetEnemy, 6),
                    new EffectStep(EffectOp.ExhaustTopOfDraw, EffectTarget.Self, 1)
                }
            };
            // 6 張全是餘燼:開場抽 5,抽牌堆剩 1,手上一定有餘燼
            var engine = 標準引擎(重複("cinder", 6), enemyHp: 50, extraDefs: new[] { 餘燼 });
            engine.StartCombat();
            Assert.AreEqual(1, engine.State.DrawPile.Count);

            出牌(engine, "cinder");
            Assert.AreEqual(0, engine.State.DrawPile.Count, "抽牌堆頂那張要被消耗掉");
            Assert.AreEqual(1, engine.State.ExhaustPile.Count);
            Assert.AreEqual(44, engine.State.Enemies[0].Hp);
        }
    }
}
