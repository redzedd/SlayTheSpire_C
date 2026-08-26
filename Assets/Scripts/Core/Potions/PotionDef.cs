using STS.Core.Cards;

namespace STS.Core.Potions
{
    /// <summary>藥水定義。與卡牌/敵招共用 EffectStep 結算路;使用免費、不佔出牌流程。</summary>
    public sealed class PotionDef
    {
        public string Id;
        public string Name;
        public EffectStep[] Steps = System.Array.Empty<EffectStep>();
        public bool NeedsTarget;
    }
}
