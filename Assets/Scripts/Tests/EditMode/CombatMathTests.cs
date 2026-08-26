using NUnit.Framework;
using STS.Core.Combat;

namespace STS.Core.Tests
{
    /// <summary>
    /// CombatMath 的單元測試——同時是本專案「純邏輯 + EditMode 測試」的樣板。
    /// </summary>
    public class CombatMathTests
    {
        [Test]
        public void 基礎傷害_無修正時原值輸出()
        {
            Assert.AreEqual(6, CombatMath.CalculateAttackDamage(6, 0, false, false));
        }

        [Test]
        public void 力量_直接加算到基礎傷害()
        {
            Assert.AreEqual(9, CombatMath.CalculateAttackDamage(6, 3, false, false));
        }

        [Test]
        public void 負力量_可把傷害壓到零但不為負()
        {
            Assert.AreEqual(0, CombatMath.CalculateAttackDamage(3, -5, false, false));
        }

        [Test]
        public void 易傷_傷害乘一點五後捨去()
        {
            // 6 × 1.5 = 9
            Assert.AreEqual(9, CombatMath.CalculateAttackDamage(6, 0, false, true));
            // 7 × 1.5 = 10.5 → 10
            Assert.AreEqual(10, CombatMath.CalculateAttackDamage(7, 0, false, true));
        }

        [Test]
        public void 虛弱_傷害乘零點七五後捨去()
        {
            // 6 × 0.75 = 4.5 → 4
            Assert.AreEqual(4, CombatMath.CalculateAttackDamage(6, 0, true, false));
        }

        [Test]
        public void 虛弱與易傷_浮點連乘後最終才捨去()
        {
            // 6 × 0.75 = 4.5,4.5 × 1.5 = 6.75 → 6
            Assert.AreEqual(6, CombatMath.CalculateAttackDamage(6, 0, true, true));
        }

        [Test]
        public void 格擋足夠_完全吸收不扣血()
        {
            var result = CombatMath.ResolveAttack(5, 8, 30);
            Assert.AreEqual(5, result.BlockConsumed);
            Assert.AreEqual(0, result.HpLost);
            Assert.AreEqual(3, result.RemainingBlock);
            Assert.AreEqual(30, result.RemainingHp);
        }

        [Test]
        public void 格擋不足_剩餘傷害扣血()
        {
            var result = CombatMath.ResolveAttack(10, 4, 30);
            Assert.AreEqual(4, result.BlockConsumed);
            Assert.AreEqual(6, result.HpLost);
            Assert.AreEqual(0, result.RemainingBlock);
            Assert.AreEqual(24, result.RemainingHp);
        }

        [Test]
        public void 傷害超過生命_扣血不低於零()
        {
            var result = CombatMath.ResolveAttack(100, 0, 30);
            Assert.AreEqual(30, result.HpLost);
            Assert.AreEqual(0, result.RemainingHp);
        }
    }
}
