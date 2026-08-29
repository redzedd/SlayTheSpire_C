using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace STS.Game.UI
{
    /// <summary>
    /// 燈火畫面(版面參考附圖):標題問句 + 兩張大選項卡(休息/鍛造),下方是營火與角色。
    /// 動作走 GameController,與煙霧同路徑。
    /// </summary>
    public sealed class RestScreenController : MonoBehaviour
    {
        public static RestScreenController Build(Transform parent, GameController game)
        {
            var root = UiKit.CreateRect("燈火畫面", parent);
            UiKit.Stretch(root);
            var controller = root.gameObject.AddComponent<RestScreenController>();

            var title = UiKit.CreateText("標題", root, "我該做什麼呢?", 46f);
            UiKit.Place(title.rectTransform, new Vector2(0f, 330f), new Vector2(900f, 64f));

            var run = game.Run.State;
            int healAmount = run.MaxHp * game.Db.Balance.RestHealPercent / 100;

            BuildOption(root, new Vector2(-190f, 150f), new Color(0.35f, 0.6f, 0.4f),
                "Zzz...", "休息", $"回復 {healAmount} 點生命", () => game.RestHealAction());
            // 符號一律用 CJK 字元:繁中字型沒有裝飾性符號的字形,會變成豆腐方塊
            BuildOption(root, new Vector2(190f, 150f), new Color(0.62f, 0.32f, 0.3f),
                "鎚", "鍛造", "升級一張卡牌", () => game.RestOpenUpgradePicker());

            // 營火與角色(佔位美術:色塊)
            var log = UiKit.CreatePanel("木頭", root, new Color(0.28f, 0.22f, 0.16f));
            UiKit.Place(log.rectTransform, new Vector2(-220f, -240f), new Vector2(280f, 50f));
            var hero = UiKit.CreatePanel("角色", root, new Color(0.3f, 0.45f, 0.65f));
            UiKit.Place(hero.rectTransform, new Vector2(-240f, -170f), new Vector2(110f, 150f));
            var fire = UiKit.CreatePanel("營火", root, new Color(0.55f, 0.95f, 0.35f));
            UiKit.Place(fire.rectTransform, new Vector2(0f, -240f), new Vector2(70f, 90f));
            // 火焰忽明忽暗
            fire.rectTransform.DOScale(new Vector3(1.15f, 1.3f, 1f), 0.7f)
                .SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine).SetLink(fire.gameObject);
            var log2 = UiKit.CreatePanel("木頭2", root, new Color(0.28f, 0.22f, 0.16f));
            UiKit.Place(log2.rectTransform, new Vector2(230f, -240f), new Vector2(280f, 50f));

            var hint = UiKit.CreateText("狀態", root, $"生命 {run.Hp}/{run.MaxHp}", 28f);
            UiKit.Place(hint.rectTransform, new Vector2(0f, -400f), new Vector2(600f, 40f));
            return controller;
        }

        private static void BuildOption(Transform parent, Vector2 pos, Color color,
            string symbol, string label, string detail, Action onClick)
        {
            var card = UiKit.CreatePanel($"選項_{label}", parent, color);
            UiKit.Place(card.rectTransform, pos, new Vector2(300f, 190f));
            var button = card.gameObject.AddComponent<Button>();
            button.onClick.AddListener(() => onClick());
            card.gameObject.AddComponent<CardHoverLift>().Setup(card.rectTransform);

            var symbolText = UiKit.CreateText("符號", card.transform, symbol, 54f);
            UiKit.Place(symbolText.rectTransform, new Vector2(0f, 30f), new Vector2(280f, 80f));

            var labelText = UiKit.CreateText("標籤", parent, label, 34f, new Color(1f, 0.86f, 0.45f));
            UiKit.Place(labelText.rectTransform, pos + new Vector2(0f, -120f), new Vector2(300f, 44f));

            var detailText = UiKit.CreateText("說明", card.transform, detail, 22f);
            UiKit.Place(detailText.rectTransform, new Vector2(0f, -50f), new Vector2(280f, 40f));
        }
    }
}
