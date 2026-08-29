using UnityEngine;
using STS.Core.Combat.Enemies;

namespace STS.Game.DataAssets
{
    /// <summary>遭遇資料資產(哪些敵人、哪個池、抽中權重)。</summary>
    [CreateAssetMenu(menuName = "STS/遭遇定義", fileName = "Encounter")]
    public sealed class EncounterAsset : ScriptableObject
    {
        [Tooltip("遭遇 id")] public string id;
        [Tooltip("所屬池(弱普/普通/精英/Boss)")] public EncounterPool pool = EncounterPool.Normal;
        [Tooltip("池內抽中權重")] public int weight = 1;
        [Tooltip("敵人 id 清單(站位順序)")] public string[] enemyIds = System.Array.Empty<string>();

        public EncounterDef ToDef()
        {
            return new EncounterDef { Id = id, EnemyIds = enemyIds, Pool = pool, Weight = weight };
        }
    }
}
