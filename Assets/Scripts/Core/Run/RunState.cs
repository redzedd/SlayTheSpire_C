using System.Collections.Generic;
using STS.Core.Cards;
using STS.Core.Map;
using STS.Core.Relics;
using STS.Core.Rng;

namespace STS.Core.Run
{
    public enum RunPhase
    {
        /// <summary>在地圖上選下一個節點。</summary>
        ChoosingNode,
        /// <summary>戰鬥中(戰鬥本身由 CombatEngine 負責,Run 層等 ApplyCombatResult)。</summary>
        InCombat,
        /// <summary>戰後選卡獎勵。</summary>
        ChoosingReward,
        InShop,
        AtRest,
        /// <summary>寶箱前:箱子已開但東西還沒入包,等玩家收下或跳過。</summary>
        AtTreasure,
        GameOver,
        /// <summary>擊敗 Boss,一輪通關。</summary>
        RunClear
    }

    /// <summary>
    /// 一輪爬塔的全部持久狀態。純資料聚合——禁止任何 UI/引擎物件參照,
    /// 未來存檔 = 把這個物件圖序列化,現在就要守住這條線。
    /// </summary>
    public sealed class RunState
    {
        public ulong Seed;
        public RunRng Rng;
        public RunPhase Phase = RunPhase.ChoosingNode;

        public MapGraph Map;
        /// <summary>-1 = 尚未踏上第一個節點。</summary>
        public int CurrentNodeId = -1;
        /// <summary>已完成的樓層數(= 已進入節點數)。</summary>
        public int Floor;

        public int Hp;
        public int MaxHp;
        public int Gold;
        public readonly List<CardInstance> Deck = new List<CardInstance>();
        public readonly List<RelicInstance> Relics = new List<RelicInstance>();
        /// <summary>藥水欄;null = 空欄。</summary>
        public readonly string[] PotionSlots = new string[3];

        /// <summary>已打過的普通戰數(前 N 場用弱池)。</summary>
        public int NormalCombatsFought;
        /// <summary>商店已購買移除服務次數(定價遞增)。</summary>
        public int ShopRemovesPurchased;
        /// <summary>藥水掉落率的浮動偏移(未掉+、掉了-)。</summary>
        public int PotionChanceOffset;
        /// <summary>新生卡牌實體 id 的下一個值(獎勵/商店加卡用)。</summary>
        public int NextCardInstanceId = 1;
    }
}
