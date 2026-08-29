using UnityEngine;

namespace STS.Game.UI
{
    /// <summary>終局畫面(通關/敗北共用):結果+簡要統計+再來一輪。</summary>
    public sealed class EndScreenController : MonoBehaviour
    {
        public static EndScreenController Build(Transform parent, GameController game, bool victory)
        {
            var root = UiKit.CreateRect("終局畫面", parent);
            UiKit.Stretch(root);
            var controller = root.gameObject.AddComponent<EndScreenController>();

            var panel = UiKit.CreatePanel("面板", root, UiKit.面板色);
            UiKit.Place(panel.rectTransform, Vector2.zero, new Vector2(640f, 420f));

            UiKit.Place(UiKit.CreateText("結果", panel.transform,
                victory ? "通關!" : "敗北…", 64f,
                victory ? new Color(1f, 0.85f, 0.3f) : new Color(0.8f, 0.35f, 0.3f)).rectTransform,
                new Vector2(0f, 110f), new Vector2(560f, 90f));

            var run = game.Run.State;
            UiKit.Place(UiKit.CreateText("統計", panel.transform,
                $"抵達樓層 {run.Floor}   金幣 {run.Gold}   牌組 {run.Deck.Count} 張   遺物 {run.Relics.Count} 件",
                24f).rectTransform, new Vector2(0f, 20f), new Vector2(580f, 40f));

            UiKit.Place((RectTransform)UiKit.CreateButton("再來", panel.transform, "再來一輪", 30f,
                new Color(0.3f, 0.5f, 0.35f), () => game.StartNewRun()).transform,
                new Vector2(0f, -110f), new Vector2(260f, 70f));
            return controller;
        }
    }
}
