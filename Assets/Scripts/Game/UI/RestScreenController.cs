using UnityEngine;
using STS.Core.Content;

namespace STS.Game.UI
{
    /// <summary>燈火畫面:休息回血或升級一張卡(開牌組檢視選未升級卡)。動作走 GameController。</summary>
    public sealed class RestScreenController : MonoBehaviour
    {
        public static RestScreenController Build(Transform parent, GameController game)
        {
            var root = UiKit.CreateRect("燈火畫面", parent);
            UiKit.Stretch(root);
            var controller = root.gameObject.AddComponent<RestScreenController>();

            var panel = UiKit.CreatePanel("面板", root, UiKit.面板色);
            UiKit.Place(panel.rectTransform, Vector2.zero, new Vector2(640f, 420f));

            UiKit.Place(UiKit.CreateText("標題", panel.transform, "燈火旁", 40f,
                new Color(1f, 0.7f, 0.35f)).rectTransform, new Vector2(0f, 150f), new Vector2(560f, 54f));

            var run = game.Run.State;
            int healAmount = run.MaxHp * game.Db.Balance.RestHealPercent / 100;
            UiKit.Place(UiKit.CreateText("狀態", panel.transform,
                $"生命 {run.Hp}/{run.MaxHp}", 26f).rectTransform,
                new Vector2(0f, 95f), new Vector2(560f, 36f));

            UiKit.Place((RectTransform)UiKit.CreateButton("休息", panel.transform,
                $"休息(回復 {healAmount} 點生命)", 26f, new Color(0.3f, 0.5f, 0.35f),
                () => game.RestHealAction()).transform,
                new Vector2(0f, 10f), new Vector2(420f, 64f));

            UiKit.Place((RectTransform)UiKit.CreateButton("升級", panel.transform,
                "鍛造(升級一張卡)", 26f, new Color(0.55f, 0.4f, 0.2f),
                () => game.RestOpenUpgradePicker()).transform,
                new Vector2(0f, -80f), new Vector2(420f, 64f));
            return controller;
        }
    }
}
