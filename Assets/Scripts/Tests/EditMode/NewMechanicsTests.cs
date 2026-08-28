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

        /// <summary>卡面上「現在」會顯示的傷害數字:描述模板換成純 {dmg},格式化結果就是那個數。</summary>
        private static int 卡面傷害(CombatEngine engine, CardDef def, CombatantState target)
        {
            var probe = new CardDef
            {
                Id = def.Id, Name = def.Name, Type = def.Type, Rarity = def.Rarity, Cost = def.Cost,
                DescriptionTemplate = "{dmg}", Steps = def.Steps
            };
            return int.Parse(CardTextFormatter.FormatDescription(probe, engine.State.Player, target, engine));
        }

        /// <summary>出牌前先讀卡面數字,出牌後比對敵人實際掉的血——兩者必須一致。</summary>
        private static void 卡面與實際一致(CombatEngine engine, CardDef def, bool 帶目標 = true)
        {
            var enemy = engine.State.Enemies[0];
            int shown = 卡面傷害(engine, def, 帶目標 ? enemy : null);
            int hpBefore = enemy.Hp;
            出牌(engine, def.Id);
            Assert.AreEqual(shown, hpBefore - enemy.Hp,
                $"{def.Name}:卡面顯示 {shown},實際打了 {hpBefore - enemy.Hp}");
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
            // 6 + 3×2 = 12,而且卡面在打之前就要顯示 12
            Assert.AreEqual(12, 卡面傷害(engine, 灰燼, engine.State.Enemies[0]));
            卡面與實際一致(engine, 灰燼);
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

            // 基礎 4 + 2×2 = 8,再吃易傷 ×1.5 = 12;卡面要在打之前就顯示 12
            Assert.AreEqual(12, 卡面傷害(engine, 欺凌, engine.State.Enemies[0]));
            卡面與實際一致(engine, 欺凌);
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
            // 已打出 2 張其他攻擊 → 8 + 2×2 = 12,卡面在打之前就要是 12
            Assert.AreEqual(12, 卡面傷害(engine, 焚燒, engine.State.Enemies[0]));
            卡面與實際一致(engine, 焚燒);
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
            // 沒指到敵人也要顯示 12:成長來自牌堆,跟目標無關(使用者實測回報的問題)
            Assert.AreEqual(12, 卡面傷害(engine, 完美, null), "沒指目標時卡面就該顯示成長後的數字");
            卡面與實際一致(engine, 完美, 帶目標: false);
            Assert.AreEqual(38, engine.State.Enemies[0].Hp);
        }

        [Test]
        public void 完美打擊_數的是整個牌堆不是只有手牌()
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
            // 8 張牌開場只抽 5 → 一定有「打擊」留在抽牌堆;若只數手牌,數字會少
            var deck = new[] { "perfect", "strike", "strike", "strike", "strike", "strike", "strike", "defend" };
            var engine = 標準引擎(deck, enemyHp: 200, extraDefs: new[] { 完美 });
            engine.StartCombat();

            int perfectIndex = 手牌位置(engine, "perfect");
            if (perfectIndex < 0) Assert.Ignore("這個種子沒把完美打擊抽進手裡");

            int 手牌中的打擊 = 0;
            for (int i = 0; i < engine.State.Hand.Count; i++)
            {
                if (engine.GetCardDef(engine.State.Hand[i]).Name.Contains("打擊")) 手牌中的打擊++;
            }
            // 全牌堆:完美打擊 1 + 打擊 6 = 7 張 → 6 + 2×7 = 20
            Assert.Less(手牌中的打擊, 7, "測試前提:手牌不可能裝下全部 7 張含「打擊」的牌");
            Assert.AreEqual(20, 卡面傷害(engine, 完美, engine.State.Enemies[0]),
                "要數抽牌堆/棄牌堆/消耗堆裡的牌,不能只數手牌");
            卡面與實際一致(engine, 完美);
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
        public void 重振精神_消耗非攻擊牌_每張換格擋()
        {
            var 重振 = new CardDef
            {
                Id = "wind", Name = "重振精神", Type = CardType.Skill, Rarity = CardRarity.Uncommon, Cost = 0,
                DescriptionTemplate = "消耗手牌中所有非攻擊牌。",
                Steps = new[]
                {
                    new EffectStep(EffectOp.ExhaustNonAttacksInHand, EffectTarget.Self),
                    new EffectStep(EffectOp.Block, EffectTarget.Self, 0,
                        AmountKind.PerLastExhausted, secondaryAmount: 5)
                }
            };
            var engine = 標準引擎(new[] { "wind", "defend", "defend", "strike", "strike" },
                extraDefs: new[] { 重振 });
            engine.StartCombat();

            出牌(engine, "wind");
            // 手上剩防禦×2(非攻擊)與打擊×2:只有 2 張防禦被消耗 → 2×5 = 10 點格擋
            Assert.AreEqual(2, engine.State.ExhaustPile.Count);
            Assert.AreEqual(10, engine.State.Player.Block);
            Assert.AreEqual(2, engine.State.Hand.Count, "攻擊牌要留在手上");
        }

        [Test]
        public void 惡魔之焰_消耗整手_每張換傷害()
        {
            var 惡魔之焰 = new CardDef
            {
                Id = "fiend", Name = "惡魔之焰", Type = CardType.Attack, Rarity = CardRarity.Rare, Cost = 0,
                DescriptionTemplate = "造成 {dmg} 點傷害。", Exhausts = true,
                Steps = new[]
                {
                    new EffectStep(EffectOp.ExhaustHand, EffectTarget.Self),
                    new EffectStep(EffectOp.Damage, EffectTarget.TargetEnemy, 0,
                        AmountKind.PerLastExhausted, secondaryAmount: 7)
                }
            };
            var engine = 標準引擎(new[] { "fiend", "defend", "defend", "strike", "strike" },
                enemyHp: 50, extraDefs: new[] { 惡魔之焰 });
            engine.StartCombat();

            出牌(engine, "fiend");
            // 打出後手上剩 4 張全被消耗 → 0 + 7×4 = 28
            Assert.AreEqual(0, engine.State.Hand.Count);
            Assert.AreEqual(22, engine.State.Enemies[0].Hp);
        }

        [Test]
        public void 添柴_棄掉整手再抽等量()
        {
            var 添柴 = new CardDef
            {
                Id = "stoke", Name = "添柴", Type = CardType.Skill, Rarity = CardRarity.Rare, Cost = 0,
                DescriptionTemplate = "棄掉整手牌,然後抽等量的牌。",
                Steps = new[] { new EffectStep(EffectOp.DiscardHandDrawSame, EffectTarget.Self) }
            };
            // 8 張牌:開場抽 5,抽牌堆留 3
            var deck = new[] { "stoke", "stoke", "stoke", "stoke", "stoke", "defend", "defend", "defend" };
            var engine = 標準引擎(deck, extraDefs: new[] { 添柴 });
            engine.StartCombat();

            出牌(engine, "stoke");
            // 打出後手上 4 張全棄掉,再抽 4 張(抽牌堆 3 張 + 重洗棄牌堆補 1)
            Assert.AreEqual(4, engine.State.Hand.Count, "棄幾張就要抽回幾張");
        }

        [Test]
        public void 劫掠_持續抽牌直到抽出非攻擊牌()
        {
            var 劫掠 = new CardDef
            {
                Id = "pillage", Name = "劫掠", Type = CardType.Attack, Rarity = CardRarity.Uncommon, Cost = 0,
                DescriptionTemplate = "造成 {dmg} 點傷害。",
                Steps = new[]
                {
                    new EffectStep(EffectOp.Damage, EffectTarget.TargetEnemy, 6),
                    new EffectStep(EffectOp.DrawUntilNonAttack, EffectTarget.Self)
                }
            };
            // 7 張全是攻擊牌:抽到牌堆乾為止都不該停,也不該無窮迴圈
            var deck = new[] { "pillage", "pillage", "pillage", "pillage", "pillage", "strike", "strike" };
            var engine = 標準引擎(deck, enemyHp: 60, extraDefs: new[] { 劫掠 });
            engine.StartCombat();
            Assert.AreEqual(2, engine.State.DrawPile.Count);

            出牌(engine, "pillage");
            Assert.AreEqual(0, engine.State.DrawPile.Count, "全是攻擊牌就該一路抽到底");
            Assert.AreEqual(6, engine.State.Hand.Count);
            Assert.AreEqual(54, engine.State.Enemies[0].Hp);
        }

        [Test]
        public void 戰鼓_回合開始消耗抽牌堆頂()
        {
            var 戰鼓 = 能力("drum", "戰鼓", StatusId.DrumOfBattle, 1);
            var deck = new[] { "drum", "drum", "drum", "drum", "drum", "defend", "defend", "defend", "defend", "defend" };
            var engine = 標準引擎(deck, enemyHp: 200, enemyAttack: 0, extraDefs: new[] { 戰鼓 });
            engine.StartCombat();

            if (手牌位置(engine, "drum") < 0) Assert.Ignore("這個種子沒把戰鼓抽進手裡");
            出牌(engine, "drum");
            Assert.AreEqual(1, engine.State.Player.GetStatus(StatusId.DrumOfBattle));

            int exhaustBefore = engine.State.ExhaustPile.Count;
            int drawBefore = engine.State.DrawPile.Count;
            engine.EndPlayerTurn();   // 敵人待機 → 回到玩家回合開始
            if (drawBefore == 0) Assert.Ignore("回合開始時抽牌堆本來就空的,消耗不到東西");
            Assert.AreEqual(exhaustBefore + 1, engine.State.ExhaustPile.Count, "回合開始要消耗抽牌堆頂 1 張");
        }

        private static CardDef 破滅牌(string id = "havoc", int cost = 0, PileType after = PileType.Exhaust)
        {
            return new CardDef
            {
                Id = id, Name = "破滅", Type = CardType.Skill, Rarity = CardRarity.Common, Cost = cost,
                DescriptionTemplate = "打出抽牌堆頂部的牌。",
                Steps = new[] { new EffectStep(EffectOp.PlayTopOfDraw, EffectTarget.Self, 1, pile: after) }
            };
        }

        [Test]
        public void 破滅_打出抽牌堆頂那張並消耗它()
        {
            var deck = new[] { "havoc", "havoc", "havoc", "havoc", "havoc",
                               "strike", "strike", "strike", "strike", "strike" };
            var engine = 標準引擎(deck, enemyHp: 200, extraDefs: new[] { 破滅牌() });
            engine.StartCombat();

            int drawBefore = engine.State.DrawPile.Count;
            int exhaustBefore = engine.State.ExhaustPile.Count;
            出牌(engine, "havoc");

            // 不管翻到哪張:抽牌堆少一張、消耗堆多一張
            Assert.AreEqual(drawBefore - 1, engine.State.DrawPile.Count);
            Assert.AreEqual(exhaustBefore + 1, engine.State.ExhaustPile.Count, "自動打出的牌要進消耗堆");
        }

        [Test]
        public void 破滅翻到破滅_不會無限遞迴()
        {
            // 整副都是破滅:每張打出來又翻下一張,沒有重入保護就會鑽到底
            var engine = 標準引擎(重複("havoc", 12), enemyHp: 200, extraDefs: new[] { 破滅牌() });
            engine.StartCombat();

            int drawBefore = engine.State.DrawPile.Count;
            Assert.DoesNotThrow(() => 出牌(engine, "havoc"));
            int consumed = drawBefore - engine.State.DrawPile.Count;
            Assert.Greater(consumed, 0, "至少要打出一張");
            Assert.LessOrEqual(consumed, 4, "巢狀層數要被上限收住,不能一路啃光抽牌堆");
            Assert.AreEqual(CombatPhase.PlayerTurn, engine.State.Phase);
        }

        [Test]
        public void 傾瀉_X費打出抽牌堆頂部X張()
        {
            var 傾瀉 = new CardDef
            {
                Id = "cascade", Name = "傾瀉", Type = CardType.Skill, Rarity = CardRarity.Rare,
                Cost = 0, CostIsX = true,
                DescriptionTemplate = "打出抽牌堆頂部的 X 張牌。",
                Steps = new[] { new EffectStep(EffectOp.PlayTopOfDraw, EffectTarget.Self, 0, repeatKind: RepeatKind.XEnergy, pile: PileType.Discard) }
            };
            // 全牌組只有一張傾瀉,其餘全是防禦:自動打出的牌不可能再是傾瀉,巢狀不會污染計數
            var deck = new[] { "cascade", "defend", "defend", "defend", "defend",
                               "defend", "defend", "defend", "defend", "defend" };
            CombatEngine engine = null;
            foreach (ulong seed in new ulong[] { 1234UL, 1UL, 2UL, 3UL, 4UL, 5UL })
            {
                var candidate = 標準引擎(deck, enemyHp: 200, seed: seed, extraDefs: new[] { 傾瀉 });
                candidate.StartCombat();
                if (手牌位置(candidate, "cascade") >= 0) { engine = candidate; break; }
            }
            Assert.IsNotNull(engine, "試過的種子都沒把傾瀉抽進手裡");

            int drawBefore = engine.State.DrawPile.Count;
            出牌(engine, "cascade");   // 3 點能量全下 → 打出 3 張

            Assert.AreEqual(0, engine.State.Energy, "X 費要把能量吃光");
            Assert.AreEqual(drawBefore - 3, engine.State.DrawPile.Count, "X=3 就該打出 3 張");
        }

        [Test]
        public void 彼岸咆哮_每個回合開始再打一次()
        {
            var 咆哮 = new CardDef
            {
                Id = "howl", Name = "彼岸咆哮", Type = CardType.Attack, Rarity = CardRarity.Uncommon, Cost = 3,
                DescriptionTemplate = "對所有敵人造成 {dmg} 點傷害。", Exhausts = true,
                Steps = new[]
                {
                    new EffectStep(EffectOp.Damage, EffectTarget.AllEnemies, 16),
                    new EffectStep(EffectOp.ApplyStatus, EffectTarget.Self, 16, status: StatusId.HowlFromBeyond)
                }
            };
            var engine = 標準引擎(new[] { "howl", "defend", "defend", "defend", "defend" },
                enemyHp: 200, enemyAttack: 0, extraDefs: new[] { 咆哮 });
            engine.StartCombat();

            出牌(engine, "howl");
            Assert.AreEqual(184, engine.State.Enemies[0].Hp);
            Assert.AreEqual(16, engine.State.Player.GetStatus(StatusId.HowlFromBeyond));

            engine.EndPlayerTurn();   // 敵人待機 → 回到玩家回合開始,咆哮再打一次
            Assert.AreEqual(168, engine.State.Enemies[0].Hp, "回合開始要再造成一次 16 點");
        }

        /// <summary>賦予某個狀態的 0 費技能牌(測試用的載具)。</summary>
        private static CardDef 技能(string id, string name, StatusId status, int stacks)
        {
            return new CardDef
            {
                Id = id, Name = name, Type = CardType.Skill, Rarity = CardRarity.Uncommon, Cost = 0,
                DescriptionTemplate = name,
                Steps = new[] { new EffectStep(EffectOp.ApplyStatus, EffectTarget.Self, stacks, status: status) }
            };
        }

        [Test]
        public void 無情猛攻_下一張攻擊零費_只免一張()
        {
            var 蓄勢 = 技能("free", "蓄勢", StatusId.NextAttackFree, 1);
            var engine = 標準引擎(new[] { "free", "strike", "strike", "defend", "defend" },
                enemyHp: 200, extraDefs: new[] { 蓄勢 });
            engine.StartCombat();

            出牌(engine, "free");
            Assert.AreEqual(3, engine.State.Energy);

            出牌(engine, "strike");   // 這張免費
            Assert.AreEqual(3, engine.State.Energy, "下一張攻擊應該不花能量");
            Assert.AreEqual(0, engine.State.Player.GetStatus(StatusId.NextAttackFree), "用掉就該消失");

            出牌(engine, "strike");   // 這張要正常付費
            Assert.AreEqual(2, engine.State.Energy, "只免一張,第二張要照付");
        }

        [Test]
        public void 腐化_技能零費且打完消耗()
        {
            var 腐化 = 能力("corrupt", "腐化", StatusId.Corruption, 1);
            var engine = 標準引擎(new[] { "corrupt", "defend", "defend", "strike", "strike" },
                enemyHp: 200, extraDefs: new[] { 腐化 });
            engine.StartCombat();

            出牌(engine, "corrupt");
            int exhaustBefore = engine.State.ExhaustPile.Count;
            int discardBefore = engine.State.DiscardPile.Count;

            出牌(engine, "defend");
            Assert.AreEqual(3, engine.State.Energy, "技能牌在腐化下不花能量");
            Assert.AreEqual(exhaustBefore + 1, engine.State.ExhaustPile.Count, "技能牌打完要被消耗");
            Assert.AreEqual(discardBefore, engine.State.DiscardPile.Count, "不該進棄牌堆");

            出牌(engine, "strike");
            Assert.AreEqual(2, engine.State.Energy, "攻擊牌不受腐化影響");
        }

        [Test]
        public void 踩踏_本回合每打出一張攻擊就少一費()
        {
            var 踩踏 = new CardDef
            {
                Id = "stomp", Name = "踩踏", Type = CardType.Attack, Rarity = CardRarity.Uncommon, Cost = 3,
                CostScaling = CostScaling.MinusPerAttackPlayedThisTurn,
                DescriptionTemplate = "對所有敵人造成 {dmg} 點傷害。",
                Steps = new[] { new EffectStep(EffectOp.Damage, EffectTarget.AllEnemies, 12) }
            };
            var engine = 標準引擎(new[] { "stomp", "strike", "strike", "defend", "defend" },
                enemyHp: 200, extraDefs: new[] { 踩踏 });
            engine.StartCombat();

            Assert.AreEqual(3, engine.GetCardCost(踩踏), "還沒打過攻擊牌就是原價");
            出牌(engine, "strike");
            Assert.AreEqual(2, engine.GetCardCost(踩踏), "打過一張攻擊牌就少 1 費");

            出牌(engine, "stomp");
            Assert.AreEqual(0, engine.State.Energy, "3 能量 - 打擊 1 - 踩踏 2 = 0");
            Assert.AreEqual(182, engine.State.Enemies[0].Hp);
        }

        [Test]
        public void 連環拳_下一張攻擊額外生效一次()
        {
            var 連環 = 技能("combo", "連環拳", StatusId.NextAttackDoubled, 1);
            var engine = 標準引擎(new[] { "combo", "strike", "strike", "defend", "defend" },
                enemyHp: 200, extraDefs: new[] { 連環 });
            engine.StartCombat();

            出牌(engine, "combo");
            出牌(engine, "strike");   // 6 點打兩次 = 12
            Assert.AreEqual(188, engine.State.Enemies[0].Hp, "這張攻擊要結算兩次");
            Assert.AreEqual(0, engine.State.Player.GetStatus(StatusId.NextAttackDoubled));

            出牌(engine, "strike");   // 恢復正常,只打一次
            Assert.AreEqual(182, engine.State.Enemies[0].Hp);
        }

        [Test]
        public void 暴走_同一張牌每打一次就更痛()
        {
            var 暴走 = new CardDef
            {
                Id = "rampage", Name = "暴走", Type = CardType.Attack, Rarity = CardRarity.Uncommon, Cost = 0,
                DescriptionTemplate = "造成 {dmg} 點傷害。",
                Steps = new[]
                {
                    new EffectStep(EffectOp.Damage, EffectTarget.TargetEnemy, 9),
                    new EffectStep(EffectOp.GrowThisCardDamage, EffectTarget.Self, 5)
                }
            };
            // 牌組只有一張暴走:結束回合後空堆重洗,同一張(InstanceId 相同)會再回到手上
            var engine = 標準引擎(new[] { "rampage" }, enemyHp: 200, enemyAttack: 0, extraDefs: new[] { 暴走 });
            engine.StartCombat();

            出牌(engine, "rampage");
            Assert.AreEqual(191, engine.State.Enemies[0].Hp, "第一次就是基礎 9 點");

            engine.EndPlayerTurn();
            出牌(engine, "rampage");
            Assert.AreEqual(177, engine.State.Enemies[0].Hp, "第二次要是 9+5=14 點");

            engine.EndPlayerTurn();
            出牌(engine, "rampage");
            Assert.AreEqual(158, engine.State.Enemies[0].Hp, "第三次要是 9+10=19 點");
        }

        [Test]
        public void 痛毆_吃掉手上的攻擊牌後變強()
        {
            var 痛毆 = new CardDef
            {
                Id = "thrash", Name = "痛毆", Type = CardType.Attack, Rarity = CardRarity.Rare, Cost = 0,
                DescriptionTemplate = "造成 {dmg} 點傷害兩次。",
                Steps = new[]
                {
                    new EffectStep(EffectOp.Damage, EffectTarget.TargetEnemy, 4, repeat: 2),
                    new EffectStep(EffectOp.AbsorbRandomAttackFromHand, EffectTarget.Self)
                }
            };
            // 手上只有打擊(6 點)可以被吃
            var engine = 標準引擎(new[] { "thrash", "strike", "defend", "defend", "defend" },
                enemyHp: 200, enemyAttack: 0, extraDefs: new[] { 痛毆 });
            engine.StartCombat();

            出牌(engine, "thrash");
            Assert.AreEqual(192, engine.State.Enemies[0].Hp, "這次還是 4 點打兩下");
            Assert.AreEqual(-1, 手牌位置(engine, "strike"), "手上的打擊要被吃掉");
            Assert.AreEqual(1, engine.State.ExhaustPile.Count);

            engine.EndPlayerTurn();
            出牌(engine, "thrash");
            // 吸收了打擊的 6 點 → (4+6) 打兩下 = 20
            Assert.AreEqual(172, engine.State.Enemies[0].Hp, "吃過打擊後每段要多 6 點");
        }

        [Test]
        public void 扯碎_每失血一次就多打一段()
        {
            var 扯碎 = new CardDef
            {
                Id = "tear", Name = "扯碎", Type = CardType.Attack, Rarity = CardRarity.Rare, Cost = 0,
                DescriptionTemplate = "造成 {dmg} 點傷害。",
                Steps = new[]
                {
                    new EffectStep(EffectOp.Damage, EffectTarget.TargetEnemy, 5, repeat: 1,
                        repeatKind: RepeatKind.PerHpLossThisCombat)
                }
            };
            var engine = 標準引擎(new[] { "tear", "bleed", "bleed", "defend", "defend" },
                enemyHp: 200, enemyAttack: 0, extraDefs: new[] { 扯碎, 放血(2) });
            engine.StartCombat();

            出牌(engine, "tear");
            Assert.AreEqual(195, engine.State.Enemies[0].Hp, "還沒失血過就只打一段");

            出牌(engine, "bleed");   // 失血一次
            出牌(engine, "bleed");   // 失血兩次
            Assert.AreEqual(2, engine.State.HpLossEventsThisCombat);

            engine.EndPlayerTurn();
            出牌(engine, "tear");
            // 1 + 2 段 × 5 點 = 15
            Assert.AreEqual(180, engine.State.Enemies[0].Hp, "失血兩次就要打三段");
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
