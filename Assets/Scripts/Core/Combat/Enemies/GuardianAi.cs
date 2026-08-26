using STS.Core.Combat.Statuses;

namespace STS.Core.Combat.Enemies
{
    /// <summary>
    /// 守護者 Boss 的自訂 AI:攻擊模式循環出招;累積受傷達閾值立即切防禦模式
    /// (獲得尖刺皮、意圖換成防禦序列),防禦序列跑完回攻擊模式並提高閾值。
    /// 數值與序列為 [近似] 重建,由測試鎖定;校正期對照資料修 JSON/常數。
    /// </summary>
    internal static class GuardianAi
    {
        internal const int InitialThreshold = 30;
        internal const int ThresholdGrowth = 10;
        internal const int SharpHideStacks = 3;

        private static readonly string[] OffensiveLoop = { "guardian_charge", "guardian_bash", "guardian_vent", "guardian_whirl" };
        private static readonly string[] DefensiveSequence = { "guardian_roll", "guardian_twin" };

        internal static string SelectNext(CombatEngine engine, int enemyIndex, EnemyRuntime rt)
        {
            if (rt.GuardianMode == 1)
            {
                if (rt.GuardianModeIndex < DefensiveSequence.Length)
                {
                    return DefensiveSequence[rt.GuardianModeIndex++];
                }
                ExitDefensive(engine, enemyIndex, rt);
            }
            string id = OffensiveLoop[rt.LoopIndex % OffensiveLoop.Length];
            rt.LoopIndex++;
            return id;
        }

        /// <summary>每次掉血呼叫;達閾值立即切模式並改寫當前意圖(StS 行為:切換是即時的)。</summary>
        internal static void OnDamaged(CombatEngine engine, int enemyIndex, EnemyRuntime rt, int hpLost)
        {
            rt.DamageAccumulated += hpLost;
            if (rt.GuardianMode != 0 || rt.DamageAccumulated < rt.GuardianThreshold) return;

            rt.GuardianMode = 1;
            rt.GuardianModeIndex = 0;
            rt.DamageAccumulated = 0;
            rt.GuardianThreshold += ThresholdGrowth;
            rt.LoopIndex = 0;
            engine.ApplyStatusTo(enemyIndex, StatusId.SharpHide, SharpHideStacks);
            rt.NextMoveId = DefensiveSequence[rt.GuardianModeIndex++];
            engine.EmitIntentShown(enemyIndex);
        }

        private static void ExitDefensive(CombatEngine engine, int enemyIndex, EnemyRuntime rt)
        {
            rt.GuardianMode = 0;
            int stacks = engine.GetCombatant(enemyIndex).GetStatus(StatusId.SharpHide);
            if (stacks > 0)
            {
                engine.ApplyStatusTo(enemyIndex, StatusId.SharpHide, -stacks);
            }
        }
    }
}
