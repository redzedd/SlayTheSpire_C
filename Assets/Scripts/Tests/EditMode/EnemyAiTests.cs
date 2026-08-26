using System.Collections.Generic;
using NUnit.Framework;
using STS.Core.Cards;
using STS.Core.Combat;
using STS.Core.Combat.Enemies;
using STS.Core.Combat.Statuses;
using static STS.Core.Tests.TestContent;

namespace STS.Core.Tests
{
    /// <summary>敵人 AI:開場腳本+循環、意圖預覽即時重算、連招上限、守護者模式切換、整場重播決定性。</summary>
    public class EnemyAiTests
    {
        private static EnemyDef 邪教徒(int hp = 48)
        {
            return new EnemyDef
            {
                Id = "cultist", Name = "唸咒者", HpMin = hp, HpMax = hp, Ai = AiKind.Loop,
                Moves = new[]
                {
                    new MoveDef
                    {
                        Id = "incant", Name = "唸咒", Intent = IntentType.Buff,
                        Steps = new[] { new EffectStep(EffectOp.ApplyStatus, EffectTarget.Self, 3, status: StatusId.Ritual) }
                    },
                    new MoveDef
                    {
                        Id = "dark", Name = "暗襲", Intent = IntentType.Attack,
                        Steps = new[] { new EffectStep(EffectOp.Damage, EffectTarget.TargetEnemy, 6) }
                    }
                },
                OpeningScript = new[] { "incant" },
                LoopScript = new[] { "dark" }
            };
        }

        [Test]
        public void 邪教徒_開場唸咒_儀式每回合加力_攻擊遞增()
        {
            var db = 基礎DB();
            db.Enemies["cultist"] = 邪教徒();
            var setup = 基礎Setup(重複("defend", 8));
            setup.EnemyIds.Add("cultist");
            var engine = 引擎(db, setup);
            engine.StartCombat();

            engine.EndPlayerTurn();   // 回合1:唸咒(儀式3),回合末儀式觸發 → 力量3
            Assert.AreEqual(80, engine.State.Player.Hp);
            Assert.AreEqual(3, engine.State.Enemies[0].GetStatus(StatusId.Strength));

            engine.EndPlayerTurn();   // 回合2:暗襲 6+3=9;回合末力量 → 6
            Assert.AreEqual(71, engine.State.Player.Hp);
            Assert.AreEqual(6, engine.State.Enemies[0].GetStatus(StatusId.Strength));

            engine.EndPlayerTurn();   // 回合3:暗襲 6+6=12;回合末力量 → 9
            Assert.AreEqual(59, engine.State.Player.Hp);
            Assert.AreEqual(9, engine.State.Enemies[0].GetStatus(StatusId.Strength));
        }

        [Test]
        public void 意圖預覽_受虛弱即時重算()
        {
            var 虛弱術 = new CardDef
            {
                Id = "hex", Name = "虛弱術", Type = CardType.Skill, Rarity = CardRarity.Common, Cost = 0,
                Steps = new[] { new EffectStep(EffectOp.ApplyStatus, EffectTarget.TargetEnemy, 1, status: StatusId.Weak) }
            };
            var engine = 標準引擎(重複("hex", 8), enemyHp: 50, enemyAttack: 6, seed: 1234UL, playerHp: 80, null, null, 虛弱術);
            engine.StartCombat();
            var before = engine.GetIntentPreview(0);
            Assert.AreEqual(IntentType.Attack, before.Type);
            Assert.AreEqual(6, before.Damage);
            Assert.AreEqual(1, before.Hits);

            engine.PlayCard(0, 0);
            var after = engine.GetIntentPreview(0);
            // 敵人被上虛弱:6 × 0.75 = 4,預覽即時反映
            Assert.AreEqual(4, after.Damage);
        }

        [Test]
        public void 加權AI_連招上限_不超過兩連()
        {
            var 守衛 = new EnemyDef
            {
                Id = "warden", Name = "守衛", HpMin = 500, HpMax = 500, Ai = AiKind.Weighted,
                Moves = new[]
                {
                    new MoveDef
                    {
                        Id = "guard", Name = "架盾", Intent = IntentType.Defend, Weight = 99, MaxConsecutive = 2,
                        Steps = new[] { new EffectStep(EffectOp.Block, EffectTarget.Self, 1) }
                    },
                    new MoveDef
                    {
                        Id = "poke", Name = "戳刺", Intent = IntentType.Attack, Weight = 1,
                        Steps = new[] { new EffectStep(EffectOp.Damage, EffectTarget.TargetEnemy, 1) }
                    }
                }
            };
            var db = 基礎DB();
            db.Enemies["warden"] = 守衛;
            var setup = 基礎Setup(重複("defend", 8), playerHp: 200);
            setup.EnemyIds.Add("warden");
            var engine = 引擎(db, setup);
            engine.StartCombat();
            for (int turn = 0; turn < 12; turn++)
            {
                engine.EndPlayerTurn();
            }

            var executed = new List<string>();
            for (int i = 0; i < engine.Events.Count; i++)
            {
                if (engine.Events[i].Kind == EventKind.EnemyMoveStarted) executed.Add(engine.Events[i].CardId);
            }
            Assert.AreEqual(12, executed.Count);
            int consecutive = 0;
            bool sawPoke = false;
            string last = null;
            foreach (var id in executed)
            {
                consecutive = id == last ? consecutive + 1 : 1;
                last = id;
                if (id == "guard") Assert.LessOrEqual(consecutive, 2, "架盾不得三連");
                if (id == "poke") sawPoke = true;
            }
            Assert.IsTrue(sawPoke, "上限被撞到時必須改出低權重招");
        }

        private static EnemyDef 守護者()
        {
            return new EnemyDef
            {
                Id = "guardian", Name = "守護者", HpMin = 240, HpMax = 240, Ai = AiKind.Custom,
                Moves = new[]
                {
                    new MoveDef { Id = "guardian_charge", Name = "蓄能", Intent = IntentType.Defend,
                        Steps = new[] { new EffectStep(EffectOp.Block, EffectTarget.Self, 9) } },
                    new MoveDef { Id = "guardian_bash", Name = "重砸", Intent = IntentType.Attack,
                        Steps = new[] { new EffectStep(EffectOp.Damage, EffectTarget.TargetEnemy, 32) } },
                    new MoveDef { Id = "guardian_vent", Name = "排氣", Intent = IntentType.Debuff,
                        Steps = new[]
                        {
                            new EffectStep(EffectOp.ApplyStatus, EffectTarget.TargetEnemy, 2, status: StatusId.Weak),
                            new EffectStep(EffectOp.ApplyStatus, EffectTarget.TargetEnemy, 2, status: StatusId.Vulnerable)
                        } },
                    new MoveDef { Id = "guardian_whirl", Name = "迴旋", Intent = IntentType.Attack,
                        Steps = new[] { new EffectStep(EffectOp.Damage, EffectTarget.TargetEnemy, 5, repeat: 4) } },
                    new MoveDef { Id = "guardian_roll", Name = "滾壓", Intent = IntentType.Attack,
                        Steps = new[] { new EffectStep(EffectOp.Damage, EffectTarget.TargetEnemy, 9) } },
                    new MoveDef { Id = "guardian_twin", Name = "雙擊", Intent = IntentType.Attack,
                        Steps = new[] { new EffectStep(EffectOp.Damage, EffectTarget.TargetEnemy, 8, repeat: 2) } }
                }
            };
        }

        [Test]
        public void 守護者_累積受傷切防禦模式_序列跑完切回()
        {
            var db = 基礎DB();
            db.Enemies["guardian"] = 守護者();
            var setup = 基礎Setup(重複("strike", 10), playerHp: 300);
            setup.EnemyIds.Add("guardian");
            var engine = 引擎(db, setup);
            engine.StartCombat();

            // 回合1:三刀 18(累積18);守護者蓄能(格擋9)
            engine.PlayCard(0, 0);
            engine.PlayCard(0, 0);
            engine.PlayCard(0, 0);
            engine.EndPlayerTurn();
            Assert.AreEqual(222, engine.State.Enemies[0].Hp);

            // 回合2:格擋 9 先吃掉 6+3,實際掉血 0+3+6(累積 27);守護者重砸 32
            engine.PlayCard(0, 0);
            engine.PlayCard(0, 0);
            engine.PlayCard(0, 0);
            engine.EndPlayerTurn();
            Assert.AreEqual(213, engine.State.Enemies[0].Hp);
            Assert.AreEqual(268, engine.State.Player.Hp);

            // 回合3第一刀:累積 33 ≥ 30 → 立即切防禦模式:尖刺皮3、意圖改滾壓
            engine.PlayCard(0, 0);
            Assert.AreEqual(3, engine.State.Enemies[0].GetStatus(StatusId.SharpHide));
            Assert.AreEqual("滾壓", engine.GetIntentPreview(0).MoveName);

            // 第二刀:先吃尖刺皮 3 反傷,再命中
            engine.PlayCard(0, 0);
            Assert.AreEqual(265, engine.State.Player.Hp);
            engine.EndPlayerTurn();   // 滾壓 9
            Assert.AreEqual(256, engine.State.Player.Hp);
            Assert.AreEqual("雙擊", engine.GetIntentPreview(0).MoveName);

            engine.EndPlayerTurn();   // 雙擊 8×2;序列跑完 → 回攻擊模式、尖刺皮移除、意圖回蓄能
            Assert.AreEqual(240, engine.State.Player.Hp);
            Assert.AreEqual(0, engine.State.Enemies[0].GetStatus(StatusId.SharpHide));
            Assert.AreEqual("蓄能", engine.GetIntentPreview(0).MoveName);
        }

        [Test]
        public void 整場重播_同種子同腳本_事件流完全一致()
        {
            CombatEngine Run()
            {
                var db = 基礎DB();
                db.Enemies["cultist"] = 邪教徒();
                var deck = new List<string>(重複("strike", 5));
                deck.AddRange(重複("defend", 5));
                var setup = 基礎Setup(deck.ToArray());
                setup.EnemyIds.Add("cultist");
                var engine = 引擎(db, setup, seed: 42UL);
                engine.StartCombat();
                for (int round = 0; round < 3; round++)
                {
                    if (engine.State.Phase != CombatPhase.PlayerTurn) break;
                    bool playedAny = true;
                    while (playedAny)
                    {
                        playedAny = false;
                        for (int i = 0; i < engine.State.Hand.Count; i++)
                        {
                            if (engine.CanPlayCard(i, 0, out _))
                            {
                                engine.PlayCard(i, 0);
                                playedAny = true;
                                break;
                            }
                        }
                        if (engine.State.Phase != CombatPhase.PlayerTurn) return engine;
                    }
                    engine.EndPlayerTurn();
                }
                return engine;
            }

            var first = Run();
            var second = Run();
            Assert.AreEqual(first.Events.Count, second.Events.Count);
            for (int i = 0; i < first.Events.Count; i++)
            {
                var a = first.Events[i];
                var b = second.Events[i];
                Assert.AreEqual(a.Kind, b.Kind, $"事件 {i} 種類不同");
                Assert.AreEqual(a.SourceIndex, b.SourceIndex, $"事件 {i} SourceIndex 不同");
                Assert.AreEqual(a.TargetIndex, b.TargetIndex, $"事件 {i} TargetIndex 不同");
                Assert.AreEqual(a.Amount, b.Amount, $"事件 {i} Amount 不同");
                Assert.AreEqual(a.Amount2, b.Amount2, $"事件 {i} Amount2 不同");
                Assert.AreEqual(a.HpLost, b.HpLost, $"事件 {i} HpLost 不同");
                Assert.AreEqual(a.CardId, b.CardId, $"事件 {i} CardId 不同");
                Assert.AreEqual(a.Status, b.Status, $"事件 {i} Status 不同");
            }
            Assert.AreEqual(first.State.Player.Hp, second.State.Player.Hp);
            Assert.AreEqual(first.State.Enemies[0].Hp, second.State.Enemies[0].Hp);
        }
    }
}
