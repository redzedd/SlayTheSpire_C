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

        private static CardDef 打擊升級版()
        {
            return new CardDef
            {
                Id = "strike+", Name = "打擊+", Type = CardType.Attack, Rarity = CardRarity.Starter, Cost = 1,
                DescriptionTemplate = "造成 {dmg} 點傷害。",
                Steps = new[] { new EffectStep(EffectOp.Damage, EffectTarget.TargetEnemy, 9) }
            };
        }

        private static CardDef 武裝(bool 全部 = false)
        {
            return new CardDef
            {
                Id = "arm", Name = "武裝", Type = CardType.Skill, Rarity = CardRarity.Common, Cost = 0,
                DescriptionTemplate = "升級手牌。",
                Steps = 全部
                    ? new[] { new EffectStep(EffectOp.UpgradeAllInHand, EffectTarget.Self) }
                    : new[] { new EffectStep(EffectOp.ChooseUpgradeInHand, EffectTarget.Self, 1) }
            };
        }

        [Test]
        public void 武裝_選一張手牌升級_該牌本場變強()
        {
            var engine = 標準引擎(new[] { "arm", "strike", "defend", "defend", "defend" },
                enemyHp: 200, extraDefs: new[] { 武裝(), 打擊升級版() });
            engine.StartCombat();

            出牌(engine, "arm");
            Assert.AreEqual(CombatPhase.AwaitingChoice, engine.State.Phase);
            Assert.AreEqual(ChoiceSource.Hand, engine.State.PendingChoiceSource);
            Assert.AreEqual(ChoiceAction.UpgradeForCombat, engine.State.PendingChoiceAction);

            int strikeIndex = 手牌位置(engine, "strike");
            var strikeCard = engine.State.Hand[strikeIndex];
            engine.ResolveChoice(new[] { strikeIndex });

            Assert.AreEqual("打擊+", engine.GetCardDef(strikeCard).Name, "升級後要查到 strike+ 的定義");
            出牌(engine, "strike");
            Assert.AreEqual(191, engine.State.Enemies[0].Hp, "升級後的打擊是 9 點,不是 6 點");
        }

        [Test]
        public void 武裝_戰鬥內升級不會外洩到卡組()
        {
            var engine = 標準引擎(new[] { "arm", "strike", "defend", "defend", "defend" },
                enemyHp: 200, extraDefs: new[] { 武裝(), 打擊升級版() });
            engine.StartCombat();

            出牌(engine, "arm");
            int strikeIndex = 手牌位置(engine, "strike");
            var strikeCard = engine.State.Hand[strikeIndex];
            engine.ResolveChoice(new[] { strikeIndex });

            // 戰鬥用的就是 run 卡組那批 CardInstance:升級只能記在戰鬥狀態,
            // 寫進 CardInstance.Upgraded 會讓這張牌永久升級,下一場戰鬥還在
            Assert.IsFalse(strikeCard.Upgraded, "CardInstance 不可以被改動");
            Assert.IsTrue(engine.State.UpgradedInCombat.Contains(strikeCard.InstanceId));
        }

        [Test]
        public void 武裝升級版_手上每張能升的都升()
        {
            var engine = 標準引擎(new[] { "arm", "strike", "strike", "defend", "defend" },
                enemyHp: 200, extraDefs: new[] { 武裝(全部: true), 打擊升級版() });
            engine.StartCombat();

            出牌(engine, "arm");
            Assert.AreEqual(CombatPhase.PlayerTurn, engine.State.Phase, "全體升級不需要玩家選,不該中斷");

            出牌(engine, "strike");
            Assert.AreEqual(191, engine.State.Enemies[0].Hp);
            出牌(engine, "strike");
            Assert.AreEqual(182, engine.State.Enemies[0].Hp, "第二張打擊也要是升級版");
            // 防禦沒有升級版,不該讓流程炸掉
            Assert.DoesNotThrow(() => 出牌(engine, "defend"));
        }

        [Test]
        public void 頭槌_從棄牌堆挑一張放到抽牌堆頂()
        {
            var 頭槌 = new CardDef
            {
                Id = "headbutt", Name = "頭槌", Type = CardType.Attack, Rarity = CardRarity.Common, Cost = 0,
                DescriptionTemplate = "造成 {dmg} 點傷害。",
                Steps = new[]
                {
                    new EffectStep(EffectOp.Damage, EffectTarget.TargetEnemy, 9),
                    new EffectStep(EffectOp.ChooseFromDiscardToDrawTop, EffectTarget.Self, 1)
                }
            };
            var engine = 標準引擎(new[] { "headbutt", "strike", "defend", "defend", "defend" },
                enemyHp: 200, extraDefs: new[] { 頭槌 });
            engine.StartCombat();

            出牌(engine, "strike");   // 先讓棄牌堆有東西可挑
            Assert.AreEqual(1, engine.State.DiscardPile.Count);
            var buried = engine.State.DiscardPile[0];

            出牌(engine, "headbutt");
            Assert.AreEqual(CombatPhase.AwaitingChoice, engine.State.Phase);
            Assert.AreEqual(ChoiceSource.Discard, engine.State.PendingChoiceSource);

            engine.ResolveChoice(new[] { 0 });
            // 頭槌自己打完也會落進棄牌堆,所以不能斷言數量是 0——要看的是那張被挑走的牌走了沒
            CollectionAssert.DoesNotContain(engine.State.DiscardPile, buried, "挑走的牌要離開棄牌堆");
            Assert.AreSame(buried, engine.State.DrawPile[engine.State.DrawPile.Count - 1],
                "要放在抽牌堆頂(尾端就是堆頂)");
        }

        [Test]
        public void 拆卸_目標有易傷才打第二下()
        {
            var 拆卸 = new CardDef
            {
                Id = "dismantle", Name = "拆卸", Type = CardType.Attack, Rarity = CardRarity.Uncommon, Cost = 0,
                DescriptionTemplate = "造成 {dmg} 點傷害。",
                Steps = new[]
                {
                    new EffectStep(EffectOp.Damage, EffectTarget.TargetEnemy, 8),
                    new EffectStep(EffectOp.Damage, EffectTarget.TargetEnemy, 8,
                        condition: StepCondition.TargetIsVulnerable)
                }
            };
            var engine = 標準引擎(new[] { "dismantle", "dismantle", "bash", "defend", "defend" },
                enemyHp: 200, extraDefs: new[] { 拆卸 });
            engine.StartCombat();

            出牌(engine, "dismantle");   // 沒有易傷 → 只打一下 8
            Assert.AreEqual(192, engine.State.Enemies[0].Hp);

            出牌(engine, "bash");        // 8 傷 + 2 層易傷 → 184
            Assert.AreEqual(184, engine.State.Enemies[0].Hp);

            出牌(engine, "dismantle");   // 有易傷 → 兩下,各 8×1.5=12 → 共 24
            Assert.AreEqual(160, engine.State.Enemies[0].Hp, "有易傷時要打兩下");
        }

        [Test]
        public void 怨恨_本回合失過血才觸發條件步驟()
        {
            // 條件效果用格擋而不是抽牌:抽牌要靠抽牌堆有料,5 張牌全在手上時抽不到,
            // 那樣測到的會是牌堆狀態而不是條件本身
            var 怨恨 = new CardDef
            {
                Id = "spite", Name = "怨恨", Type = CardType.Attack, Rarity = CardRarity.Uncommon, Cost = 0,
                DescriptionTemplate = "造成 {dmg} 點傷害。",
                Steps = new[]
                {
                    new EffectStep(EffectOp.Damage, EffectTarget.TargetEnemy, 6),
                    new EffectStep(EffectOp.Block, EffectTarget.Self, 5, condition: StepCondition.LostHpThisTurn)
                }
            };
            var engine = 標準引擎(new[] { "spite", "spite", "bleed", "defend", "defend" },
                enemyHp: 200, enemyAttack: 0, extraDefs: new[] { 怨恨, 放血(3) });
            engine.StartCombat();

            出牌(engine, "spite");
            Assert.AreEqual(194, engine.State.Enemies[0].Hp);
            Assert.AreEqual(0, engine.State.Player.Block, "沒失過血,條件步驟不該執行");

            出牌(engine, "bleed");
            Assert.IsTrue(engine.State.LostHpThisTurn);

            出牌(engine, "spite");
            Assert.AreEqual(188, engine.State.Enemies[0].Hp);
            Assert.AreEqual(5, engine.State.Player.Block, "失過血就要執行條件步驟");
        }

        [Test]
        public void 狂宴_斬殺才加最大生命()
        {
            var 狂宴 = new CardDef
            {
                Id = "feed", Name = "狂宴", Type = CardType.Attack, Rarity = CardRarity.Rare, Cost = 0,
                DescriptionTemplate = "造成 {dmg} 點傷害。", Exhausts = true,
                Steps = new[]
                {
                    new EffectStep(EffectOp.Damage, EffectTarget.TargetEnemy, 10),
                    new EffectStep(EffectOp.GainMaxHp, EffectTarget.Self, 3,
                        condition: StepCondition.LastAttackKilled)
                }
            };
            // 敵人 30 血:第一刀打不死,第二刀也打不死,第三刀才斬殺
            var engine = 標準引擎(重複("feed", 5), enemyHp: 30, playerHp: 80, extraDefs: new[] { 狂宴 });
            engine.StartCombat();

            出牌(engine, "feed");
            Assert.AreEqual(80, engine.State.Player.MaxHp, "沒斬殺就不加最大生命");
            出牌(engine, "feed");
            Assert.AreEqual(80, engine.State.Player.MaxHp);
            出牌(engine, "feed");   // 30 → 0,斬殺
            Assert.IsFalse(engine.State.Enemies[0].IsAlive);
            Assert.AreEqual(83, engine.State.Player.MaxHp, "斬殺要永久 +3 最大生命");
        }

        [Test]
        public void 契約終結_消耗堆不夠就不能打出()
        {
            var 契約終結 = new CardDef
            {
                Id = "pacts", Name = "契約終結", Type = CardType.Attack, Rarity = CardRarity.Rare, Cost = 0,
                PlayCondition = PlayCondition.ExhaustPileAtLeast, PlayConditionAmount = 3,
                DescriptionTemplate = "對所有敵人造成 {dmg} 點傷害。",
                Steps = new[] { new EffectStep(EffectOp.Damage, EffectTarget.AllEnemies, 17) }
            };
            var engine = 標準引擎(new[] { "pacts", "selfexhaust", "selfexhaust", "selfexhaust", "defend" },
                enemyHp: 200, extraDefs: new[] { 契約終結, 自消耗() });
            engine.StartCombat();

            int pactsIndex = 手牌位置(engine, "pacts");
            Assert.IsFalse(engine.CanPlayCard(pactsIndex, 0, out string reason), "消耗堆是空的,不該能打");
            StringAssert.Contains("消耗堆", reason);

            出牌(engine, "selfexhaust");
            出牌(engine, "selfexhaust");
            出牌(engine, "selfexhaust");
            Assert.AreEqual(3, engine.State.ExhaustPile.Count);

            pactsIndex = 手牌位置(engine, "pacts");
            Assert.IsTrue(engine.CanPlayCard(pactsIndex, 0, out _), "湊滿 3 張就該能打");
            engine.PlayCard(pactsIndex, 0);
            Assert.AreEqual(183, engine.State.Enemies[0].Hp);
        }

        [Test]
        public void 殘酷_只對易傷目標加成_且卡面同步()
        {
            var 殘酷 = 能力("cruel", "殘酷", StatusId.Cruelty, 25);
            // 用 0 費的上易傷牌而不是痛擊:一回合只有 3 能量,痛擊要 2 費會爆
            var engine = 標準引擎(new[] { "cruel", "strike", "hex", "strike", "defend" },
                enemyHp: 200, extraDefs: new[] { 殘酷, 上易傷() });
            engine.StartCombat();

            出牌(engine, "cruel");
            出牌(engine, "strike");   // 目標沒易傷 → 殘酷不生效,照樣 6 點
            Assert.AreEqual(194, engine.State.Enemies[0].Hp, "沒易傷時殘酷不該加成");

            出牌(engine, "hex");      // 上 3 層易傷

            // 6 × (1.5 + 0.25) = 10.5 → 無條件捨去 = 10
            var enemy = engine.State.Enemies[0];
            int shown = int.Parse(CardTextFormatter.FormatDescription(
                new CardDef { Id = "s", Name = "打擊", Type = CardType.Attack, Cost = 1,
                    DescriptionTemplate = "{dmg}", Steps = 打擊().Steps },
                engine.State.Player, enemy, engine));
            Assert.AreEqual(10, shown, "卡面要把殘酷算進去");

            出牌(engine, "strike");
            Assert.AreEqual(184, engine.State.Enemies[0].Hp, "易傷 + 殘酷 = 10 點");
        }

        /// <summary>0 費的上易傷牌:測試裡要製造易傷又不想被能量卡住時用。</summary>
        private static CardDef 上易傷()
        {
            return new CardDef
            {
                Id = "hex", Name = "虛弱術", Type = CardType.Skill, Rarity = CardRarity.Common, Cost = 0,
                DescriptionTemplate = "施加 3 層易傷。",
                Steps = new[] { new EffectStep(EffectOp.ApplyStatus, EffectTarget.TargetEnemy, 3, status: StatusId.Vulnerable) }
            };
        }

        [Test]
        public void 巨像_易傷的攻擊者對你只打一半()
        {
            var 巨像 = 技能("colossus", "巨像", StatusId.Colossus, 1);
            // 敵人每回合打 10 點
            var engine = 標準引擎(new[] { "colossus", "hex", "defend", "defend", "defend" },
                enemyHp: 200, enemyAttack: 10, playerHp: 80, extraDefs: new[] { 巨像, 上易傷() });
            engine.StartCombat();

            出牌(engine, "hex");        // 敵人上易傷
            Assert.AreEqual(10, engine.GetIntentPreview(0).Damage, "還沒放巨像,意圖是原本的 10");

            出牌(engine, "colossus");   // 本回合減半
            Assert.AreEqual(5, engine.GetIntentPreview(0).Damage, "意圖要立刻反映減半後的 5");

            engine.EndPlayerTurn();
            // 敵人易傷 + 玩家有巨像 → 10 × 0.5 = 5,再被 0 格擋吸收 → 掉 5 血
            Assert.AreEqual(75, engine.State.Player.Hp, "易傷的攻擊者只能打一半");
            Assert.AreEqual(0, engine.State.Player.GetStatus(StatusId.Colossus), "巨像回合結束就消失");
        }

        [Test]
        public void 覆甲_回合結束給等量護甲然後自己減一()
        {
            var 岩石鎧甲 = 能力("armor", "岩石鎧甲", StatusId.Plating, 4);
            var engine = 標準引擎(new[] { "armor", "defend", "defend", "defend", "defend" },
                enemyHp: 200, enemyAttack: 10, playerHp: 80, extraDefs: new[] { 岩石鎧甲 });
            engine.StartCombat();

            出牌(engine, "armor");
            Assert.AreEqual(4, engine.State.Player.GetStatus(StatusId.Plating));
            Assert.AreEqual(0, engine.State.Player.Block, "護甲要等回合結束才給");

            engine.EndPlayerTurn();
            // 回合結束給 4 點格擋 → 敵人打 10 → 擋掉 4,掉 6 血;覆甲降為 3
            Assert.AreEqual(74, engine.State.Player.Hp, "打出的當回合就要生效");
            Assert.AreEqual(3, engine.State.Player.GetStatus(StatusId.Plating), "給完護甲後覆甲要減 1");

            engine.EndPlayerTurn();
            // 這次只給 3 點 → 掉 7 血;覆甲降為 2
            Assert.AreEqual(67, engine.State.Player.Hp);
            Assert.AreEqual(2, engine.State.Player.GetStatus(StatusId.Plating));
        }

        [Test]
        public void 邪眼_本回合消耗過牌才給第二份格擋()
        {
            var 邪眼 = new CardDef
            {
                Id = "eye", Name = "邪眼", Type = CardType.Skill, Rarity = CardRarity.Uncommon, Cost = 0,
                DescriptionTemplate = "獲得 {blk} 點格擋。",
                Steps = new[]
                {
                    new EffectStep(EffectOp.Block, EffectTarget.Self, 8),
                    new EffectStep(EffectOp.Block, EffectTarget.Self, 8, condition: StepCondition.ExhaustedThisTurn)
                }
            };
            var engine = 標準引擎(new[] { "eye", "eye", "selfexhaust", "defend", "defend" },
                enemyHp: 200, enemyAttack: 0, extraDefs: new[] { 邪眼, 自消耗() });
            engine.StartCombat();

            出牌(engine, "eye");
            Assert.AreEqual(8, engine.State.Player.Block, "沒消耗過牌就只有一份");

            出牌(engine, "selfexhaust");
            Assert.IsTrue(engine.State.ExhaustedThisTurn);
            出牌(engine, "eye");
            Assert.AreEqual(24, engine.State.Player.Block, "消耗過牌就給兩份(8 + 16)");
        }

        [Test]
        public void 岿然不動_只翻倍本回合第一次格擋()
        {
            var 岿然 = 能力("unmov", "岿然不動", StatusId.Unmovable, 1);
            var engine = 標準引擎(new[] { "unmov", "defend", "defend", "defend", "defend" },
                enemyHp: 200, enemyAttack: 0, extraDefs: new[] { 岿然 });
            engine.StartCombat();

            出牌(engine, "unmov");
            出牌(engine, "defend");
            Assert.AreEqual(10, engine.State.Player.Block, "第一次格擋要翻倍");
            出牌(engine, "defend");
            Assert.AreEqual(15, engine.State.Player.Block, "第二次就不翻倍了");
        }

        [Test]
        public void 擒拿_本回合獲得格擋就追打()
        {
            var 擒拿 = new CardDef
            {
                Id = "grapple", Name = "擒拿", Type = CardType.Attack, Rarity = CardRarity.Uncommon, Cost = 0,
                DescriptionTemplate = "造成 {dmg} 點傷害。",
                Steps = new[]
                {
                    new EffectStep(EffectOp.Damage, EffectTarget.TargetEnemy, 7),
                    new EffectStep(EffectOp.ApplyStatus, EffectTarget.Self, 5, status: StatusId.Grapple)
                }
            };
            var engine = 標準引擎(new[] { "grapple", "defend", "defend", "defend", "defend" },
                enemyHp: 200, enemyAttack: 0, extraDefs: new[] { 擒拿 });
            engine.StartCombat();

            出牌(engine, "grapple");
            Assert.AreEqual(193, engine.State.Enemies[0].Hp);
            出牌(engine, "defend");
            Assert.AreEqual(188, engine.State.Enemies[0].Hp, "獲得格擋要追打 5 點");
        }

        [Test]
        public void 兇惡_施加易傷就抽牌()
        {
            var 兇惡 = 能力("vic", "兇惡", StatusId.Vicious, 1);
            var engine = 標準引擎(new[] { "vic", "hex", "defend", "defend", "defend" },
                enemyHp: 200, enemyAttack: 0, extraDefs: new[] { 兇惡, 上易傷() });
            engine.StartCombat();

            出牌(engine, "vic");
            出牌(engine, "defend");   // 讓棄牌堆有料,抽牌才抽得動(抽牌堆此時是空的)
            int drawnBefore = 事件數(engine, EventKind.CardDrawn);

            出牌(engine, "hex");      // 施加易傷 → 兇惡抽 1 張
            Assert.AreEqual(drawnBefore + 1, 事件數(engine, EventKind.CardDrawn), "施加易傷要抽 1 張");
        }

        [Test]
        public void 躍躍欲試_依手上攻擊牌給能量_之後不再獲得能量()
        {
            var 躍躍欲試 = new CardDef
            {
                Id = "expect", Name = "躍躍欲試", Type = CardType.Skill, Rarity = CardRarity.Uncommon, Cost = 2,
                DescriptionTemplate = "獲得能量。",
                Steps = new[]
                {
                    new EffectStep(EffectOp.GainEnergy, EffectTarget.Self, 0,
                        AmountKind.PerAttackInHand, secondaryAmount: 1),
                    new EffectStep(EffectOp.ApplyStatus, EffectTarget.Self, 1, status: StatusId.NoEnergyGain)
                }
            };
            var 補能 = new CardDef
            {
                Id = "refill", Name = "補能", Type = CardType.Skill, Rarity = CardRarity.Common, Cost = 0,
                DescriptionTemplate = "獲得 2 點能量。",
                Steps = new[] { new EffectStep(EffectOp.GainEnergy, EffectTarget.Self, 2) }
            };
            var engine = 標準引擎(new[] { "expect", "strike", "strike", "refill", "defend" },
                enemyHp: 200, enemyAttack: 0, extraDefs: new[] { 躍躍欲試, 補能 });
            engine.StartCombat();

            出牌(engine, "expect");   // 花 2 能量,手上剩 2 張打擊 → 補回 2 → 3
            Assert.AreEqual(3, engine.State.Energy, "手上兩張攻擊牌要補 2 點能量");

            出牌(engine, "refill");   // 本回合不再獲得能量,這 2 點要被擋掉
            Assert.AreEqual(3, engine.State.Energy, "力竭期間不該再拿到能量");
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
