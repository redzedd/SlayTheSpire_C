using System.Collections.Generic;
using STS.Core.Cards;
using STS.Core.Relics;

namespace STS.Core.Combat
{
    /// <summary>
    /// 開戰參數。M5 的 RunEngine 會從 RunState 組出這個物件;在那之前由呼叫端/測試手組。
    /// Relics 傳實體參照:Counter 類狀態跨戰鬥持久,引擎只讀寫不重建。
    /// </summary>
    public sealed class CombatSetup
    {
        public int PlayerHp = 80;
        public int PlayerMaxHp = 80;
        public int MaxEnergy = 3;
        public List<CardInstance> Deck = new List<CardInstance>();
        /// <summary>敵人 id 清單(經 IContentDb.GetEnemy 解析),站位順序即索引。</summary>
        public List<string> EnemyIds = new List<string>();
        public List<RelicInstance> Relics = new List<RelicInstance>();
        public List<string> PotionIds = new List<string>();
        /// <summary>
        /// 「隨機生成一張攻擊牌」時的候選池(地獄之刃)。由 Run 層從內容目錄灌入——
        /// 引擎只看得到 IContentDb,沒有列舉全卡池的能力,不該為了這件事把介面撐大。
        /// 空的話生成類效果就安靜地不做事。
        /// </summary>
        public List<string> RandomAttackPool = new List<string>();
    }
}
