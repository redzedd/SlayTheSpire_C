using System.Text;
using STS.Core.Combat;
using STS.Core.Combat.Enemies;
using STS.Core.Content;
using STS.Core.Potions;
using STS.Core.Relics;

namespace STS.Game.UI
{
    /// <summary>
    /// 提示框文字組裝:把引擎的即時狀態(層數/血量/意圖)與資料層的名稱說明合成一段文字。
    /// 純字串處理,不改任何狀態;查無文字定義時退回顯示狀態代號,不讓缺資料變成空白提示。
    /// </summary>
    internal static class TooltipText
    {
        internal static string 敵人(IContentCatalog catalog, CombatEngine engine, int enemyIndex)
        {
            var enemy = engine.State.Enemies[enemyIndex];
            var sb = new StringBuilder(256);
            sb.Append("<b>").Append(enemy.Name).Append("</b>\n");
            sb.Append("生命 ").Append(enemy.Hp).Append('/').Append(enemy.MaxHp);
            if (enemy.Block > 0) sb.Append("   格擋 ").Append(enemy.Block);
            if (enemy.IsAlive)
            {
                var intent = engine.GetIntentPreview(enemyIndex);
                sb.Append("\n意圖:").Append(意圖說明(intent));
            }
            AppendStatuses(sb, catalog, enemy, "\n\n目前狀態:", "\n\n目前沒有任何狀態。");
            return sb.ToString();
        }

        internal static string 玩家(IContentCatalog catalog, CombatEngine engine)
        {
            var player = engine.State.Player;
            var sb = new StringBuilder(256);
            sb.Append("<b>").Append(player.Name).Append("</b>\n");
            sb.Append("生命 ").Append(player.Hp).Append('/').Append(player.MaxHp);
            if (player.Block > 0) sb.Append("   格擋 ").Append(player.Block);
            sb.Append("\n能量 ").Append(engine.State.Energy).Append('/').Append(engine.State.MaxEnergy);
            AppendStatuses(sb, catalog, player, "\n\n目前狀態:", "\n\n目前沒有任何狀態。");
            return sb.ToString();
        }

        internal static string 遺物(RelicDef def, int counter)
        {
            var sb = new StringBuilder(160);
            sb.Append("<b>").Append(def.Name).Append("</b>\n").Append(def.Description);
            if (counter > 0) sb.Append("\n\n目前計數:").Append(counter);
            return sb.ToString();
        }

        internal static string 藥水(PotionDef def)
        {
            var sb = new StringBuilder(160);
            sb.Append("<b>").Append(def.Name).Append("</b>\n");
            sb.Append(string.IsNullOrEmpty(def.Description) ? "(尚無說明)" : def.Description);
            sb.Append(def.NeedsTarget ? "\n\n需要指定敵人目標。" : "\n\n點擊即可使用。");
            return sb.ToString();
        }

        private static void AppendStatuses(StringBuilder sb, IContentCatalog catalog, CombatantState combatant,
            string header, string emptyText)
        {
            if (combatant.Statuses.Count == 0)
            {
                sb.Append(emptyText);
                return;
            }
            sb.Append(header);
            foreach (var status in combatant.Statuses)
            {
                var def = catalog.GetStatusDef(status.Id);
                sb.Append("\n<b>").Append(def != null ? def.Name : status.Id.ToString())
                    .Append(' ').Append(status.Stacks).Append("</b>");
                if (def != null && !string.IsNullOrEmpty(def.Description))
                {
                    sb.Append('\n').Append(def.FormatDescription(status.Stacks));
                }
            }
        }

        private static string 意圖說明(IntentInfo intent)
        {
            switch (intent.Type)
            {
                case IntentType.Attack:
                    return intent.Hits > 1
                        ? $"{intent.MoveName}——造成 {intent.Damage} 點傷害 {intent.Hits} 次"
                        : $"{intent.MoveName}——造成 {intent.Damage} 點傷害";
                case IntentType.Defend: return $"{intent.MoveName}——準備防禦";
                case IntentType.Buff: return $"{intent.MoveName}——強化自身";
                case IntentType.Debuff: return $"{intent.MoveName}——對你施加負面效果";
                default: return intent.MoveName;
            }
        }
    }
}
