using System.Collections.Generic;

namespace STS.Core.Rng
{
    /// <summary>
    /// 決定性偽隨機流(SplitMix64)。
    /// 不用 System.Random:其行為跨 .NET 版本不保證一致,會毀掉同種子重播性。
    /// 刻意做成 class 而非 struct:可變 struct 的複製語意會無聲分岔亂數流,是經典地雷。
    /// </summary>
    public sealed class RngStream
    {
        private ulong _state;

        public RngStream(ulong seed)
        {
            _state = seed;
        }

        /// <summary>目前內部狀態(未來存檔/重播用)。</summary>
        public ulong State => _state;

        /// <summary>SplitMix64 的單次混合函數,派生子種子用。</summary>
        public static ulong Mix(ulong z)
        {
            z += 0x9E3779B97F4A7C15UL;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        public ulong NextULong()
        {
            _state += 0x9E3779B97F4A7C15UL;
            ulong z = _state;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        /// <summary>回傳 [0, maxExclusive) 的整數;maxExclusive ≤ 0 一律回 0。</summary>
        public int NextInt(int maxExclusive)
        {
            if (maxExclusive <= 0) return 0;
            return (int)(NextULong() % (ulong)maxExclusive);
        }

        /// <summary>回傳 [minInclusive, maxInclusive] 的整數。</summary>
        public int Range(int minInclusive, int maxInclusive)
        {
            if (maxInclusive <= minInclusive) return minInclusive;
            return minInclusive + NextInt(maxInclusive - minInclusive + 1);
        }

        /// <summary>回傳 [0, 1) 的浮點數。取高 24 bit,避開低位品質問題。</summary>
        public float NextFloat()
        {
            return (NextULong() >> 40) * (1f / 16777216f);
        }

        /// <summary>Fisher-Yates 洗牌,就地打亂並消耗本流。</summary>
        public void Shuffle<T>(IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = NextInt(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
