using NUnit.Framework;
using STS.Core.Cards;
using STS.Core.Combat;
using STS.Core.Combat.Statuses;
using static STS.Core.Tests.TestContent;

namespace STS.Core.Tests
{
    /// <summary>卡牌描述數值代入——UI 顯示的唯一計算來源,錯了玩家看到的數字就是謊話。</summary>
    public class CardTextFormatterTests
    {
        private static CombatantState 玩家(int strength = 0, int dexterity = 0, bool weak = false, bool frail = false, int block = 0)
        {
            var player = new CombatantState { Hp = 80, MaxHp = 80, Block = block };
            if (strength != 0) player.ModifyStatus(StatusId.Strength, strength);
            if (dexterity != 0) player.ModifyStatus(StatusId.Dexterity, dexterity);
            if (weak) player.ModifyStatus(StatusId.Weak, 1);
            if (frail) player.ModifyStatus(StatusId.Frail, 1);
            return player;
        }

        [Test]
        public void 傷害佔位_基礎值()
        {
            Assert.AreEqual("造成 6 點傷害。", CardTextFormatter.FormatDescription(打擊(), 玩家()));
        }

        [Test]
        public void 傷害佔位_含力量與虛弱()
        {
            var def = 打擊();
            def.DescriptionTemplate = "造成 {dmg} 點傷害。";
            Assert.AreEqual("造成 9 點傷害。", CardTextFormatter.FormatDescription(def, 玩家(strength: 3)));
            // (6+3) × 0.75 = 6.75 → 6
            Assert.AreEqual("造成 6 點傷害。", CardTextFormatter.FormatDescription(def, 玩家(strength: 3, weak: true)));
        }

        [Test]
        public void 格擋佔位_含敏捷與脆弱()
        {
            var def = 防禦();
            def.DescriptionTemplate = "獲得 {blk} 點格擋。";
            Assert.AreEqual("獲得 7 點格擋。", CardTextFormatter.FormatDescription(def, 玩家(dexterity: 2)));
            // (5+2) × 0.75 = 5.25 → 5
            Assert.AreEqual("獲得 5 點格擋。", CardTextFormatter.FormatDescription(def, 玩家(dexterity: 2, frail: true)));
        }

        [Test]
        public void 力量倍計卡_預覽含倍率()
        {
            var def = new CardDef
            {
                Id = "heavy", Name = "千鈞斬", Type = CardType.Attack, Cost = 2,
                DescriptionTemplate = "造成 {dmg} 點傷害。",
                Steps = new[]
                {
                    new EffectStep(EffectOp.Damage, EffectTarget.TargetEnemy, 14,
                        amountKind: AmountKind.StrengthTimes, secondaryAmount: 3)
                }
            };
            // 14 + 2×3 = 20
            Assert.AreEqual("造成 20 點傷害。", CardTextFormatter.FormatDescription(def, 玩家(strength: 2)));
        }

        private static CombatantState 敵人(bool vulnerable = false)
        {
            var enemy = new CombatantState { Hp = 50, MaxHp = 50 };
            if (vulnerable) enemy.ModifyStatus(StatusId.Vulnerable, 2);
            return enemy;
        }

        [Test]
        public void 目標易傷_描述顯示放大後傷害()
        {
            // 6 × 1.5 = 9;卡面數字必須與實際打出去的一致,否則玩家會以為易傷沒作用
            Assert.AreEqual("造成 9 點傷害。",
                CardTextFormatter.FormatDescription(打擊(), 玩家(), 敵人(vulnerable: true)));
        }

        [Test]
        public void 目標無易傷或未指定目標_維持基礎值()
        {
            Assert.AreEqual("造成 6 點傷害。",
                CardTextFormatter.FormatDescription(打擊(), 玩家(), 敵人()));
            Assert.AreEqual("造成 6 點傷害。",
                CardTextFormatter.FormatDescription(打擊(), 玩家(), null));
        }

        [Test]
        public void 力量與目標易傷_先加後乘且只捨去一次()
        {
            // (6+3) × 1.5 = 13.5 → 13
            Assert.AreEqual("造成 13 點傷害。",
                CardTextFormatter.FormatDescription(打擊(), 玩家(strength: 3), 敵人(vulnerable: true)));
        }

        [Test]
        public void 虛弱與目標易傷_連乘後才捨去()
        {
            // 6 × 0.75 = 4.5,× 1.5 = 6.75 → 6(與 CombatMath 同一條路徑)
            Assert.AreEqual("造成 6 點傷害。",
                CardTextFormatter.FormatDescription(打擊(), 玩家(weak: true), 敵人(vulnerable: true)));
        }

        [Test]
        public void 格擋描述_不受目標易傷影響()
        {
            var def = 防禦();
            def.DescriptionTemplate = "獲得 {blk} 點格擋。";
            Assert.AreEqual("獲得 5 點格擋。",
                CardTextFormatter.FormatDescription(def, 玩家(), 敵人(vulnerable: true)));
        }

        [Test]
        public void 無佔位_原文照出()
        {
            var def = new CardDef { Id = "x", DescriptionTemplate = "消耗。", Steps = System.Array.Empty<EffectStep>() };
            Assert.AreEqual("消耗。", CardTextFormatter.FormatDescription(def, 玩家()));
        }
    }
}
