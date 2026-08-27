using UnityEngine;
using STS.Core.Combat.Statuses;

namespace STS.Game.DataAssets
{
    /// <summary>狀態的文字資產(tooltip 用);行為在 STS.Core 的 StatusRegistry。</summary>
    [CreateAssetMenu(menuName = "STS/狀態文字", fileName = "Status")]
    public sealed class StatusDataAsset : ScriptableObject
    {
        [Tooltip("狀態種類(對應程式的 StatusId)")] public StatusId id;
        [Tooltip("顯示名稱")] public string statusName;
        [Tooltip("說明;{n} 會代入實際層數")] [TextArea] public string description;

        public StatusDef ToDef()
        {
            return new StatusDef { Id = id, Name = statusName, Description = description };
        }
    }
}
