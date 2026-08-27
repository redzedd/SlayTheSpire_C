using UnityEngine;
using UnityEngine.UI;
using STS.Core.Cards;
using STS.Core.Combat;

namespace STS.Game.UI
{
    /// <summary>戰後獎勵畫面:金幣(已入帳)、藥水(如有)、卡三選一或跳過。按鈕走 GameController 動作(煙霧同路徑)。</summary>
    public sealed class RewardScreenController : MonoBehaviour
    {
        public static RewardScreenController Build(Transform parent, GameController game)
        {
            var root = UiKit.CreateRect("獎勵畫面", parent);
            UiKit.Stretch(root);
            var controller = root.gameObject.AddComponent<RewardScreenController>();

            var rewards = game.Run.PendingRewards;
            var panel = UiKit.CreatePanel("面板", root, UiKit.面板色);
            UiKit.Place(panel.rectTransform, Vector2.zero, new Vector2(760f, 560f));

            UiKit.Place(UiKit.CreateText("標題", panel.transform, "戰鬥勝利!", 40f, new Color(1f, 0.85f, 0.3f)).rectTransform,
                new Vector2(0f, 240f), new Vector2(700f, 54f));

            string summary = $"獲得金幣 {rewards.Gold}";
            if (rewards.PotionId != null)
            {
                summary += $"   藥水:{game.Db.GetPotion(rewards.PotionId).Name}";
            }
            if (rewards.RelicId != null)
            {
                summary += $"   遺物:{game.Db.GetRelicDef(rewards.RelicId).Name}";
            }
            UiKit.Place(UiKit.CreateText("摘要", panel.transform, summary, 24f).rectTransform,
                new Vector2(0f, 185f), new Vector2(700f, 36f));

            UiKit.Place(UiKit.CreateText("選卡", panel.transform, "選擇一張卡加入牌組:", 26f).rectTransform,
                new Vector2(0f, 130f), new Vector2(700f, 36f));

            var player = new CombatantState { Hp = game.Run.State.Hp, MaxHp = game.Run.State.MaxHp };
            for (int i = 0; i < rewards.CardChoices.Count; i++)
            {
                int index = i;
                var def = game.Db.GetCard(rewards.CardChoices[i]);
                var face = UiKit.MakeCardFace(panel.transform, def, CardTextFormatter.FormatDescription(def, player), 1.1f);
                UiKit.Place(face, new Vector2((i - (rewards.CardChoices.Count - 1) / 2f) * 210f, -60f), face.sizeDelta);
                var button = face.gameObject.AddComponent<Button>();
                button.onClick.AddListener(() => game.RewardTakeCard(index));
            }

            UiKit.Place((RectTransform)UiKit.CreateButton("跳過", panel.transform, "跳過獎勵", 26f,
                new Color(0.4f, 0.4f, 0.45f), () => game.RewardSkip()).transform,
                new Vector2(0f, -240f), new Vector2(220f, 56f));
            return controller;
        }
    }
}
