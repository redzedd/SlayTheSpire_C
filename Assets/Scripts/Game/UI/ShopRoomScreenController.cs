using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace STS.Game.UI
{
    /// <summary>
    /// 商人房間(進商店節點時的第一個畫面,版面參考附圖):左邊英雄、右邊盤坐的商人與攤開的貨,
    /// 右下「前進」離開。點商人才進貨架畫面——貨架與房間是兩個畫面,不是同一個。
    /// 佔位美術全是色塊;所有動作走 GameController。
    /// </summary>
    public sealed class ShopRoomScreenController : MonoBehaviour
    {
        public static ShopRoomScreenController Build(Transform parent, GameController game)
        {
            var root = UiKit.CreateRect("商人房間", parent);
            UiKit.Stretch(root);
            var controller = root.gameObject.AddComponent<ShopRoomScreenController>();

            var tent = UiKit.CreatePanel("帳篷", root, new Color(0.24f, 0.13f, 0.15f));
            UiKit.Stretch(tent.rectTransform);

            // 帳篷後方的布幔(單純讓佔位畫面不是一整塊死色)
            var drape = UiKit.CreatePanel("布幔", root, new Color(0.32f, 0.17f, 0.2f));
            UiKit.Place(drape.rectTransform, new Vector2(0f, 260f), new Vector2(1920f, 420f));

            // 英雄站左邊
            var heroBody = UiKit.CreatePanel("英雄", root, new Color(0.78f, 0.66f, 0.36f));
            UiKit.Place(heroBody.rectTransform, new Vector2(-430f, -80f), new Vector2(150f, 300f));
            var heroHead = UiKit.CreatePanel("英雄頭", root, new Color(0.85f, 0.74f, 0.42f));
            UiKit.Place(heroHead.rectTransform, new Vector2(-430f, 110f), new Vector2(90f, 90f));
            var sword = UiKit.CreatePanel("劍", root, new Color(0.85f, 0.87f, 0.9f));
            UiKit.Place(sword.rectTransform, new Vector2(-540f, -60f), new Vector2(26f, 320f));

            // 商人的地毯與貨
            var rug = UiKit.CreatePanel("地毯", root, new Color(0.28f, 0.45f, 0.36f));
            UiKit.Place(rug.rectTransform, new Vector2(330f, -230f), new Vector2(700f, 250f));
            for (int i = 0; i < 5; i++)
            {
                var goods = UiKit.CreatePanel($"貨{i}", root, new Color(0.9f, 0.88f, 0.8f));
                UiKit.Place(goods.rectTransform, new Vector2(120f + (i % 3) * 70f, -180f - (i / 3) * 70f),
                    new Vector2(50f, 34f));
            }
            var discount = UiKit.CreatePanel("折扣牌", root, new Color(0.95f, 0.55f, 0.2f));
            UiKit.Place(discount.rectTransform, new Vector2(150f, -60f), new Vector2(70f, 70f));
            UiKit.Place(UiKit.CreateText("折扣", discount.transform, "%", 34f, Color.white).rectTransform,
                Vector2.zero, new Vector2(70f, 70f));

            // 商人本體:點他才開貨架
            var merchant = UiKit.CreatePanel("商人", root, new Color(0.26f, 0.42f, 0.78f));
            UiKit.Place(merchant.rectTransform, new Vector2(430f, -140f), new Vector2(200f, 230f));
            var hood = UiKit.CreatePanel("兜帽", root, new Color(0.32f, 0.5f, 0.85f));
            UiKit.Place(hood.rectTransform, new Vector2(430f, 10f), new Vector2(130f, 130f));
            var face = UiKit.CreatePanel("面具", root, new Color(0.95f, 0.93f, 0.78f));
            UiKit.Place(face.rectTransform, new Vector2(430f, 10f), new Vector2(78f, 78f));

            var merchantButton = merchant.gameObject.AddComponent<Button>();
            merchantButton.onClick.AddListener(() => game.ShopOpenCounter());
            var hoodButton = hood.gameObject.AddComponent<Button>();
            hoodButton.onClick.AddListener(() => game.ShopOpenCounter());
            // 商人輕輕晃,讓「他可以點」看得出來
            merchant.rectTransform.DOAnchorPosY(-128f, 1.2f)
                .SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine).SetLink(merchant.gameObject);

            UiKit.Place(UiKit.CreateText("提示", root, "點商人看貨", 26f, new Color(1f, 0.9f, 0.6f)).rectTransform,
                new Vector2(430f, 160f), new Vector2(360f, 40f));

            BuildForwardArrow(root, new Vector2(740f, -400f), "前進", () => game.ShopLeave());
            return controller;
        }

        /// <summary>紅色前進箭頭:一塊長條 + 一個轉 45° 的方塊當箭頭,不依賴任何素材。</summary>
        public static Button BuildForwardArrow(Transform parent, Vector2 pos, string label, System.Action onClick)
        {
            var arrowColor = new Color(0.72f, 0.18f, 0.16f);
            var shaft = UiKit.CreatePanel($"箭頭_{label}", parent, arrowColor);
            UiKit.Place(shaft.rectTransform, pos, new Vector2(260f, 84f));
            var head = UiKit.CreatePanel("箭頭尖", shaft.transform, arrowColor);
            UiKit.Place(head.rectTransform, new Vector2(130f, 0f), new Vector2(60f, 60f));
            head.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            head.raycastTarget = false;

            var text = UiKit.CreateText("標籤", shaft.transform, label, 30f);
            UiKit.Place(text.rectTransform, new Vector2(-10f, 0f), new Vector2(220f, 44f));

            var button = shaft.gameObject.AddComponent<Button>();
            button.onClick.AddListener(() => onClick());
            shaft.rectTransform.DOAnchorPosX(pos.x + 14f, 0.9f)
                .SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine).SetLink(shaft.gameObject);
            return button;
        }
    }
}
