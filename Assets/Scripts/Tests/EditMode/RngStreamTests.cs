using System.Collections.Generic;
using NUnit.Framework;
using STS.Core.Rng;

namespace STS.Core.Tests
{
    /// <summary>RngStream/RunRng 的決定性測試——重播性是整個引擎測試策略的地基。</summary>
    public class RngStreamTests
    {
        [Test]
        public void 同種子_產生相同序列()
        {
            var a = new RngStream(42UL);
            var b = new RngStream(42UL);
            for (int i = 0; i < 20; i++)
            {
                Assert.AreEqual(a.NextULong(), b.NextULong());
            }
        }

        [Test]
        public void 不同種子_序列不同()
        {
            var a = new RngStream(1UL);
            var b = new RngStream(2UL);
            bool anyDifferent = false;
            for (int i = 0; i < 10; i++)
            {
                if (a.NextULong() != b.NextULong()) anyDifferent = true;
            }
            Assert.IsTrue(anyDifferent);
        }

        [Test]
        public void NextInt_邊界與範圍()
        {
            var rng = new RngStream(7UL);
            Assert.AreEqual(0, rng.NextInt(0));
            Assert.AreEqual(0, rng.NextInt(1));
            for (int i = 0; i < 100; i++)
            {
                Assert.That(rng.NextInt(5), Is.InRange(0, 4));
            }
        }

        [Test]
        public void Range_含端點_退化區間回下界()
        {
            var rng = new RngStream(7UL);
            Assert.AreEqual(3, rng.Range(3, 3));
            for (int i = 0; i < 100; i++)
            {
                Assert.That(rng.Range(2, 4), Is.InRange(2, 4));
            }
        }

        [Test]
        public void NextFloat_在零到一之間()
        {
            var rng = new RngStream(9UL);
            for (int i = 0; i < 100; i++)
            {
                float value = rng.NextFloat();
                Assert.That(value, Is.GreaterThanOrEqualTo(0f).And.LessThan(1f));
            }
        }

        [Test]
        public void 洗牌_同種子同結果_且為原集合的排列()
        {
            var first = new List<int>();
            var second = new List<int>();
            for (int i = 0; i < 10; i++)
            {
                first.Add(i);
                second.Add(i);
            }
            new RngStream(123UL).Shuffle(first);
            new RngStream(123UL).Shuffle(second);
            CollectionAssert.AreEqual(first, second);
            CollectionAssert.AreEquivalent(new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 }, first);
        }

        [Test]
        public void RunRng_分流互相獨立()
        {
            var consumed = RunRng.FromSeed(99UL);
            var fresh = RunRng.FromSeed(99UL);
            for (int i = 0; i < 50; i++)
            {
                consumed.Map.NextULong();
            }
            // 地圖流被大量消耗後,洗牌流必須完全不受影響
            for (int i = 0; i < 5; i++)
            {
                Assert.AreEqual(fresh.Shuffle.NextULong(), consumed.Shuffle.NextULong());
            }
        }
    }
}
