namespace STS.Core.Cards
{
    /// <summary>
    /// 一張實體卡(卡組/牌堆中的一份)。InstanceId 供 UI 跨事件追蹤同一張卡的動畫。
    /// </summary>
    public sealed class CardInstance
    {
        public readonly int InstanceId;
        public readonly string CardId;
        public bool Upgraded;

        public CardInstance(int instanceId, string cardId, bool upgraded = false)
        {
            InstanceId = instanceId;
            CardId = cardId;
            Upgraded = upgraded;
        }

        /// <summary>解析定義用的 id:升級卡對應「id+」的 CardDef。</summary>
        public string ResolvedCardId => Upgraded ? CardId + "+" : CardId;
    }
}
