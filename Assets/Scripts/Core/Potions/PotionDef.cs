using STS.Core.Cards;

namespace STS.Core.Potions
{
    /// <summary>藥水定義。與卡牌/敵招共用 EffectStep 結算路;使用免費、不佔出牌流程。</summary>
    public sealed class PotionDef
    {
        public string Id;
        public string Name;
        /// <summary>效果說明(tooltip 顯示)。</summary>
        public string Description;
        public EffectStep[] Steps = System.Array.Empty<EffectStep>();
        public bool NeedsTarget;
        /// <summary>
        /// 戰鬥外(地圖/商店/燈火)也能喝。回血、加最大生命這類效果不需要戰場,
        /// 治療藥水擺著不能用是很怪的事;不能在戰鬥外用的藥水,那時只剩「丟棄」。
        /// 由 Run 層執行,所以步驟只能用 Heal / GainMaxHp(匯入時檢查)。
        /// </summary>
        public bool UsableOutOfCombat;
    }
}
