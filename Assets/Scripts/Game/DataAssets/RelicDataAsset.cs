using UnityEngine;
using STS.Core.Relics;

namespace STS.Game.DataAssets
{
    /// <summary>遺物資料資產(名稱/稀有度/描述);行為在 STS.Core 的 RelicRegistry,id 必須對得上 RelicIds。</summary>
    [CreateAssetMenu(menuName = "STS/遺物定義", fileName = "Relic")]
    public sealed class RelicDataAsset : ScriptableObject
    {
        [Tooltip("遺物 id(必須存在於 RelicIds,否則沒有行為)")] public string id;
        [Tooltip("顯示名稱")] public string relicName;
        [Tooltip("稀有度")] public RelicRarity rarity = RelicRarity.Common;
        [Tooltip("描述")] [TextArea] public string description;

        public RelicDef ToDef()
        {
            return new RelicDef { Id = id, Name = relicName, Rarity = rarity, Description = description };
        }
    }
}
