using STS.Core.Cards;
using STS.Core.Combat.Statuses;

namespace STS.Core.Combat.Enemies
{
    public enum AiKind
    {
        /// <summary>照 LoopScript(或 Moves)順序循環。</summary>
        Loop,
        /// <summary>照 Weight 加權隨機,受 MaxConsecutive 限制。</summary>
        Weighted,
        /// <summary>程式註冊的自訂 AI(守護者)。</summary>
        Custom
    }

    public enum IntentType
    {
        Attack,
        Defend,
        Buff,
        Debuff,
        Special
    }

    /// <summary>敵人的一個招式。Steps 與卡牌共用同一套 EffectStep 結算。</summary>
    public sealed class MoveDef
    {
        public string Id;
        public string Name;
        public IntentType Intent;
        public EffectStep[] Steps = System.Array.Empty<EffectStep>();
        /// <summary>Weighted AI 的權重;≤0 視為 1。</summary>
        public int Weight = 1;
        /// <summary>連續使用上限;0 = 無限制。</summary>
        public int MaxConsecutive;
    }

    /// <summary>初始狀態(蝨子的捲曲等)。</summary>
    public readonly struct StatusStack
    {
        public readonly StatusId Id;
        public readonly int Stacks;

        public StatusStack(StatusId id, int stacks)
        {
            Id = id;
            Stacks = stacks;
        }
    }

    public sealed class EnemyDef
    {
        public string Id;
        public string Name;
        public int HpMin;
        public int HpMax;
        /// <summary>招式庫;OpeningScript/LoopScript 以 id 參照這裡。</summary>
        public MoveDef[] Moves = System.Array.Empty<MoveDef>();
        /// <summary>開場固定招(依序),跑完才進入 AI 選招。</summary>
        public string[] OpeningScript = System.Array.Empty<string>();
        /// <summary>Loop AI 的循環序;空 = 循環整個 Moves。</summary>
        public string[] LoopScript = System.Array.Empty<string>();
        public AiKind Ai = AiKind.Loop;
        public StatusStack[] InitialStatuses = System.Array.Empty<StatusStack>();

        public MoveDef GetMove(string moveId)
        {
            for (int i = 0; i < Moves.Length; i++)
            {
                if (Moves[i].Id == moveId) return Moves[i];
            }
            throw new System.InvalidOperationException($"敵人 {Id} 沒有招式 {moveId}");
        }
    }

    public enum EncounterPool
    {
        Weak,
        Normal,
        Elite,
        Boss
    }

    /// <summary>遭遇定義(M5 的 Run 層抽池使用;引擎目前吃 CombatSetup)。</summary>
    public sealed class EncounterDef
    {
        public string Id;
        public string[] EnemyIds = System.Array.Empty<string>();
        public EncounterPool Pool = EncounterPool.Normal;
        public int Weight = 1;
    }

    /// <summary>意圖預覽——UI 顯示用的純查詢結果,傷害含力量/虛弱/易傷即時重算。</summary>
    public readonly struct IntentInfo
    {
        public readonly IntentType Type;
        public readonly string MoveName;
        /// <summary>單段傷害(重算後);非攻擊意圖為 0。</summary>
        public readonly int Damage;
        /// <summary>段數;X 型招式為 0(未定)。</summary>
        public readonly int Hits;

        public IntentInfo(IntentType type, string moveName, int damage, int hits)
        {
            Type = type;
            MoveName = moveName;
            Damage = damage;
            Hits = hits;
        }
    }
}
