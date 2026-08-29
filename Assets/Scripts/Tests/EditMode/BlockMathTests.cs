using NUnit.Framework;
using STS.Core.Combat;

namespace STS.Core.Tests
{
    public class BlockMathTests
    {
        [Test]
        public void 基礎格擋_原值輸出()
        {
            Assert.AreEqual(5, BlockMath.CalculateBlockGain(5, 0, false));
        }

        [Test]
        public void 敏捷_直接加算()
        {
            Assert.AreEqual(8, BlockMath.CalculateBlockGain(5, 3, false));
        }

        [Test]
        public void 脆弱_乘零點七五後捨去()
        {
            // 5 × 0.75 = 3.75 → 3
            Assert.AreEqual(3, BlockMath.CalculateBlockGain(5, 0, true));
            // (5+2) × 0.75 = 5.25 → 5
            Assert.AreEqual(5, BlockMath.CalculateBlockGain(5, 2, true));
        }

        [Test]
        public void 負敏捷_不低於零()
        {
            Assert.AreEqual(0, BlockMath.CalculateBlockGain(3, -5, false));
        }
    }
}
