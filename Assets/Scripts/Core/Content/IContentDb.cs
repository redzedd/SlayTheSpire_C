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
        EnemyDef GetEnemy(string enemyId);
        PotionDef GetPotion(string potionId);
    }
}
