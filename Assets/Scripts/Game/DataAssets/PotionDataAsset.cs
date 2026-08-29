using UnityEngine;
using STS.Core.Potions;

namespace STS.Game.DataAssets
{
    /// <summary>藥水資料資產。效果與卡牌共用 EffectStep 結算。</summary>
    [CreateAssetMenu(menuName = "STS/藥水定義", fileName = "Potion")]
    public sealed class PotionDataAsset : ScriptableObject
    {
        [Tooltip("藥水 id")] public string id;
        [Tooltip("顯示名稱")] public string potionName;
        [Tooltip("效果說明(tooltip 顯示)")] [TextArea] public string description;
        [Tooltip("使用時是否需要指定敵人目標")] public bool needsTarget;
        [Tooltip("戰鬥外(地圖/商店/燈火)也能喝;只允許回血與加最大生命的步驟")]
        public bool usableOutOfCombat;
        [Tooltip("效果步驟")] public EffectStepData[] steps = System.Array.Empty<EffectStepData>();

        public PotionDef ToDef()
        {
            return new PotionDef
            {
                Id = id,
                Name = potionName,
                Description = description,
                NeedsTarget = needsTarget,
                UsableOutOfCombat = usableOutOfCombat,
                Steps = EffectStepData.ToSteps(steps)
            };
        }
    }
}
