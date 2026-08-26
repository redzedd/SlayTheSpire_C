namespace STS.Core.Rng
{
    /// <summary>
    /// 一輪遊戲的全部隨機流。分流原則:玩家操作會改變呼叫次數的隨機(洗牌、隨機目標)
    /// 與進度決定型隨機(地圖、獎勵)必須分開,互不污染,才能保住同種子的重播性。
    /// </summary>
    public sealed class RunRng
    {
        public readonly RngStream Map;
        public readonly RngStream CardReward;
        public readonly RngStream PotionReward;
        public readonly RngStream RelicReward;
        public readonly RngStream Shuffle;
        public readonly RngStream EnemyAi;
        public readonly RngStream CombatMisc;

        private RunRng(ulong seed)
        {
            Map = new RngStream(RngStream.Mix(seed + 1));
            CardReward = new RngStream(RngStream.Mix(seed + 2));
            PotionReward = new RngStream(RngStream.Mix(seed + 3));
            RelicReward = new RngStream(RngStream.Mix(seed + 4));
            Shuffle = new RngStream(RngStream.Mix(seed + 5));
            EnemyAi = new RngStream(RngStream.Mix(seed + 6));
            CombatMisc = new RngStream(RngStream.Mix(seed + 7));
        }

        public static RunRng FromSeed(ulong seed)
        {
            return new RunRng(seed);
        }
    }
}
