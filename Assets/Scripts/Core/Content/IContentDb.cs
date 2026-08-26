using STS.Core.Cards;

namespace STS.Core.Content
{
    /// <summary>
    /// 內容查詢介面。引擎只依賴本介面——不知道資料來自 ScriptableObject(執行期)還是字典(測試)。
    /// </summary>
    public interface IContentDb
    {
        /// <summary>依 id 取卡牌定義;查無此 id 應直接拋錯,不回 null(拿到屍體比當場死更難查)。</summary>
        CardDef GetCard(string cardId);
    }
}
