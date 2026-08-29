namespace STS.Core.Combat.Statuses
{
    /// <summary>
    /// 狀態的資料面定義(名稱/說明,供 tooltip 顯示);行為住在 StatusRegistry。
    /// Description 可含 {n} 佔位符,顯示時代入實際層數。
    /// </summary>
    public sealed class StatusDef
    {
        public StatusId Id;
        public string Name;
        public string Description;

        /// <summary>把 {n} 代成實際層數。</summary>
        public string FormatDescription(int stacks)
        {
            return string.IsNullOrEmpty(Description) ? string.Empty : Description.Replace("{n}", stacks.ToString());
        }
    }
}
