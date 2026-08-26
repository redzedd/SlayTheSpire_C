using STS.Core.Rng;

namespace STS.Core.Combat.Enemies
{
    /// <summary>單一敵人的 AI 執行期狀態(選招歷史、守護者模式計數)。</summary>
    internal sealed class EnemyRuntime
    {
        internal EnemyDef Def;
        internal string NextMoveId;
        internal string LastMoveId;
        internal int ConsecutiveCount;
        internal int OpeningIndex;
        internal int LoopIndex;
        internal int DamageAccumulated;
        internal int GuardianMode;
        internal int GuardianModeIndex;
        internal int GuardianThreshold;
    }

    /// <summary>選招邏輯。只消耗 EnemyAi 亂數流,保持與其他流互不污染。</summary>
    internal static class EnemyAi
    {
        internal static string SelectNextMove(CombatEngine engine, int enemyIndex, EnemyRuntime rt, RngStream aiRng)
        {
            var def = rt.Def;
            if (rt.OpeningIndex < def.OpeningScript.Length)
            {
                return def.OpeningScript[rt.OpeningIndex++];
            }
            switch (def.Ai)
            {
                case AiKind.Loop:
                {
                    if (def.LoopScript.Length > 0)
                    {
                        string id = def.LoopScript[rt.LoopIndex % def.LoopScript.Length];
                        rt.LoopIndex++;
                        return id;
                    }
                    var move = def.Moves[rt.LoopIndex % def.Moves.Length];
                    rt.LoopIndex++;
                    return move.Id;
                }
                case AiKind.Weighted:
                    return SelectWeighted(def, rt, aiRng);
                case AiKind.Custom:
                    return GuardianAi.SelectNext(engine, enemyIndex, rt);
                default:
                    return def.Moves[0].Id;
            }
        }

        /// <summary>加權選招;違反 MaxConsecutive 就重擲([近似] StS 實作精神),10 次仍違規走決定性 fallback。</summary>
        private static string SelectWeighted(EnemyDef def, EnemyRuntime rt, RngStream aiRng)
        {
            for (int attempt = 0; attempt < 10; attempt++)
            {
                int total = 0;
                for (int i = 0; i < def.Moves.Length; i++)
                {
                    total += def.Moves[i].Weight <= 0 ? 1 : def.Moves[i].Weight;
                }
                int roll = aiRng.NextInt(total);
                MoveDef chosen = def.Moves[def.Moves.Length - 1];
                for (int i = 0; i < def.Moves.Length; i++)
                {
                    roll -= def.Moves[i].Weight <= 0 ? 1 : def.Moves[i].Weight;
                    if (roll < 0)
                    {
                        chosen = def.Moves[i];
                        break;
                    }
                }
                if (!Violates(chosen, rt)) return chosen.Id;
            }
            for (int i = 0; i < def.Moves.Length; i++)
            {
                if (!Violates(def.Moves[i], rt)) return def.Moves[i].Id;
            }
            return def.Moves[0].Id;
        }

        private static bool Violates(MoveDef move, EnemyRuntime rt)
        {
            return move.MaxConsecutive > 0
                && move.Id == rt.LastMoveId
                && rt.ConsecutiveCount >= move.MaxConsecutive;
        }

        /// <summary>招式執行後記錄連續次數(供 MaxConsecutive 判定)。</summary>
        internal static void RecordExecuted(EnemyRuntime rt, string moveId)
        {
            if (moveId == rt.LastMoveId)
            {
                rt.ConsecutiveCount++;
            }
            else
            {
                rt.LastMoveId = moveId;
                rt.ConsecutiveCount = 1;
            }
        }
    }
}
