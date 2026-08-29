using STS.Core.Cards;
using STS.Core.Combat.Enemies;
using STS.Core.Potions;

namespace STS.Core.Content
{
    /// <summary>
    /// 內容查詢介面。引擎只依賴本介面——不知道資料來自 ScriptableObject(執行期)還是字典(測試)。
    /// 查無此 id 應直接拋錯,不回 null(拿到屍體比當場死更難查)。
    /// </summary>
    public interface IContentDb
    {
        CardDef GetCard(string cardId);
        /// <summary>
        /// 查不到就回 false,不拋錯。用途只有一種:問「這張卡有沒有升級版」——
        /// 狀態卡沒有 id+,武裝之類的升級效果要能安靜跳過它們。
        /// </summary>
        bool TryGetCard(string cardId, out CardDef def);
        EnemyDef GetEnemy(string enemyId);
        PotionDef GetPotion(string potionId);
    }
}
