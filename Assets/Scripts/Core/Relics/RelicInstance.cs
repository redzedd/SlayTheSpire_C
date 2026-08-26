namespace STS.Core.Relics
{
    /// <summary>
    /// 一顆遺物的持有實體。Counter 供計數型遺物(雙節棍)使用,跨戰鬥持久——
    /// 生命週期由呼叫端(未來的 RunState)持有,引擎只讀寫不重建。
    /// </summary>
    public sealed class RelicInstance
    {
        public readonly string Id;
        public int Counter;

        public RelicInstance(string id, int counter = 0)
        {
            Id = id;
            Counter = counter;
        }
    }
}
