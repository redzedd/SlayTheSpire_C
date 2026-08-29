using System.Collections.Generic;
using STS.Core.Cards;

namespace STS.Core.Combat
{
    public enum CombatPhase
    {
        NotStarted,
        PlayerTurn,
        /// <summary>等待 UI 回填選擇(ResolveChoice);其餘指令一律拒收。</summary>
        AwaitingChoice,
        EnemyTurn,
        Victory,
        Defeat
    }

    /// <summary>選卡中斷時,玩家要從哪一堆牌裡挑。</summary>
    public enum ChoiceSource
    {
        Hand,
        Discard
    }

    /// <summary>選完之後對那幾張牌做什麼。</summary>
    public enum ChoiceAction
    {
        Exhaust,
        /// <summary>本場戰鬥內升級(不影響 run 卡組)。</summary>
        UpgradeForCombat,
        MoveToDrawTop
    }

    /// <summary>戰鬥的全部可變狀態。純資料聚合,禁止藏引擎邏輯或 UI 參照。</summary>
    public sealed class CombatState
    {
        public const int HandLimit = 10;

        public CombatPhase Phase = CombatPhase.NotStarted;
        /// <summary>第一個玩家回合 = 1。</summary>
        public int TurnNumber;
        public int Energy;
        public int MaxEnergy = 3;
        public CombatantState Player;
        public readonly List<CombatantState> Enemies = new List<CombatantState>();
        public readonly List<CardInstance> DrawPile = new List<CardInstance>();
        public readonly List<CardInstance> Hand = new List<CardInstance>();
        public readonly List<CardInstance> DiscardPile = new List<CardInstance>();
        public readonly List<CardInstance> ExhaustPile = new List<CardInstance>();
        /// <summary>已打出的能力卡(不進棄/消耗堆)。</summary>
        public readonly List<CardInstance> PowersPlayed = new List<CardInstance>();
        /// <summary>藥水欄;null = 空欄。</summary>
        public readonly List<string> PotionSlots = new List<string>();
        /// <summary>AwaitingChoice 時要選幾張(UI 顯示用)。</summary>
        public int PendingChoiceCount;
        /// <summary>本回合玩家已打出的攻擊牌數(焚燒型「每打出一張攻擊就加傷」用);回合開始歸零。</summary>
        public int AttacksPlayedThisTurn;
        /// <summary>同一張牌前一個消耗步驟剛消耗掉的張數,供下一個步驟換算數值(重振精神/惡魔之焰)。</summary>
        public int LastExhaustedCount;
        /// <summary>本場戰鬥玩家失去生命的「次數」(不是點數);扯碎的段數靠它成長。</summary>
        public int HpLossEventsThisCombat;
        /// <summary>本回合玩家失去過生命(怨恨);回合開始歸零。</summary>
        public bool LostHpThisTurn;
        /// <summary>
        /// 這張牌前一段攻擊是否剛好打死目標(狂宴)。每次出牌開始時歸零,
        /// 所以它問的一定是「這張牌自己打死的」,不是上一張牌留下的。
        /// </summary>
        public bool LastAttackKilled;
        /// <summary>
        /// 單張卡在本場戰鬥中累積的額外傷害,key = CardInstance.InstanceId(暴走/痛毆)。
        /// 刻意放在戰鬥狀態而不是 CardInstance 上——CardInstance 是跨戰鬥的 run 資料,
        /// 直接改它會把加成帶進下一場戰鬥。
        /// </summary>
        public readonly Dictionary<int, int> CardDamageBonus = new Dictionary<int, int>();
        /// <summary>
        /// 本場戰鬥中被臨時升級的卡(key = CardInstance.InstanceId,武裝)。
        /// 同樣不能寫進 CardInstance.Upgraded——戰鬥用的就是 run 卡組那批物件,
        /// 寫下去會變成永久升級。
        /// </summary>
        public readonly HashSet<int> UpgradedInCombat = new HashSet<int>();

        /// <summary>選卡中斷時,要從哪個牌堆選、選完要做什麼(UI 據此決定怎麼呈現)。</summary>
        public ChoiceSource PendingChoiceSource;
        public ChoiceAction PendingChoiceAction;
    }
}
