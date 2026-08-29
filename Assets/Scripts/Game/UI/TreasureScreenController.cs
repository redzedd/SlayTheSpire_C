using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace STS.Game.UI
{
    /// <summary>
    /// 寶箱畫面(版面參考附圖)。兩個階段共用同一個畫面根:
    /// 未開 = 昏暗場景中央一個長滿藤蔓的箱子,點它開箱;
    /// 已開 =「裡面有什麼?」布條 + 浮在箱口的寶物 + 右下「跳過」箭頭,
    /// 指著寶物看得到效果說明,按下去才收進包裡。
    /// 佔位美術全是色塊;所有動作走 GameController(煙霧同路徑)。
    /// </summary>
    public sealed class TreasureScreenController : MonoBehaviour
    {
        private static readonly Color 箱身色 = new Color(0.24f, 0.35f, 0.24f);
        private static readonly Color 箱蓋色 = new Color(0.3f, 0.43f, 0.29f);
        private static readonly Color 藤蔓色 = new Color(0.42f, 0.62f, 0.36f);

        private GameController _game;
        private RectTransform _root;
        private bool _opened;

        public static TreasureScreenController Build(Transform parent, GameController game)
        {
            var root = UiKit.CreateRect("寶箱畫面", parent);
            UiKit.Stretch(root);
            var controller = root.gameObject.AddComponent<TreasureScreenController>();
            controller._game = game;
            controller._root = root;
            controller.Render();
            return controller;
        }

        private void Render()
        {
            foreach (Transform child in _root)
            {
                Destroy(child.gameObject);
            }

            var backdrop = UiKit.CreatePanel("背景", _root, new Color(0.03f, 0.04f, 0.05f));
            UiKit.Stretch(backdrop.rectTransform);

            // 地上那圈光暈:圓形 sprite 壓扁當橢圓
            var glow = UiKit.CreateCircle("光暈", _root, new Color(0.45f, 0.5f, 0.12f, 0.35f));
            UiKit.Place(glow.rectTransform, new Vector2(0f, -360f), new Vector2(900f, 220f));
            glow.raycastTarget = false;

            BuildChest();

            if (_opened) RenderOpened();
            else RenderClosed();
        }

        /// <summary>
        /// 箱身、箱蓋與幾條藤蔓。開箱後蓋子往上掀開,不換一套物件。
        /// 座標互相咬合:箱身頂端 = -140,關著的蓋子底端剛好落在那條線上,才不會看成一整塊。
        /// </summary>
        private void BuildChest()
        {
            var body = UiKit.CreatePanel("箱身", _root, 箱身色);
            UiKit.Place(body.rectTransform, new Vector2(0f, -260f), new Vector2(720f, 240f));
            var band = UiKit.CreatePanel("鐵條", body.transform, new Color(0.14f, 0.2f, 0.15f));
            UiKit.Place(band.rectTransform, new Vector2(0f, 0f), new Vector2(720f, 26f));
            band.raycastTarget = false;
            // 箱口的暗邊:沒有這條線,蓋子掀開後箱身看起來只是一塊平面色塊
            var rim = UiKit.CreatePanel("箱口", body.transform, new Color(0.08f, 0.11f, 0.09f));
            UiKit.Place(rim.rectTransform, new Vector2(0f, 108f), new Vector2(720f, 24f));
            rim.raycastTarget = false;

            var lid = UiKit.CreatePanel("箱蓋", _root, 箱蓋色);
            if (_opened)
            {
                // 掀開:往上挪並向後仰,露出箱口
                UiKit.Place(lid.rectTransform, new Vector2(0f, 170f), new Vector2(720f, 120f));
                lid.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -8f);
            }
            else
            {
                UiKit.Place(lid.rectTransform, new Vector2(0f, -80f), new Vector2(720f, 120f));
            }
            var lidBand = UiKit.CreatePanel("蓋鐵條", lid.transform, new Color(0.14f, 0.2f, 0.15f));
            UiKit.Place(lidBand.rectTransform, new Vector2(0f, 0f), new Vector2(720f, 20f));
            lidBand.raycastTarget = false;

            for (int i = 0; i < 5; i++)
            {
                var vine = UiKit.CreatePanel($"藤蔓{i}", _root, 藤蔓色);
                UiKit.Place(vine.rectTransform, new Vector2(-300f + i * 150f, -290f), new Vector2(22f, 280f));
                vine.rectTransform.localRotation = Quaternion.Euler(0f, 0f, (i % 2 == 0 ? 7f : -7f));
                vine.raycastTarget = false;
            }
        }

        // ---- 未開箱 ----

        private void RenderClosed()
        {
            // 整個箱子區域都可以點,不用玩家去戳箱蓋那一小條
            var hit = UiKit.CreatePanel("開箱熱區", _root, new Color(0f, 0f, 0f, 0f));
            UiKit.Place(hit.rectTransform, new Vector2(0f, -200f), new Vector2(760f, 420f));
            var button = hit.gameObject.AddComponent<Button>();
            button.onClick.AddListener(() => _game.TreasureOpen());

            hit.rectTransform.DOScale(1.03f, 1.1f)
                .SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine).SetLink(hit.gameObject);

            UiKit.Place(UiKit.CreateText("提示", _root, "點擊開啟寶箱", 30f,
                new Color(1f, 0.92f, 0.62f)).rectTransform, new Vector2(0f, 90f), new Vector2(600f, 44f));
        }

        // ---- 已開箱 ----

        private void RenderOpened()
        {
            UiKit.CreateBanner(_root, "裡面有什麼?", new Vector2(0f, 320f));

            string relicId = _game.Run.PendingTreasureRelicId;
            if (relicId == null)
            {
                UiKit.Place(UiKit.CreateText("空箱", _root, "箱子是空的…", 32f,
                    new Color(0.75f, 0.75f, 0.78f)).rectTransform, new Vector2(0f, 20f), new Vector2(600f, 46f));
            }
            else
            {
                var def = _game.Db.GetRelicDef(relicId);
                var item = UiKit.CreatePanel("寶物", _root, new Color(0.92f, 0.78f, 0.28f));
                UiKit.Place(item.rectTransform, new Vector2(0f, 20f), new Vector2(120f, 120f));
                var button = item.gameObject.AddComponent<Button>();
                button.onClick.AddListener(() => _game.TreasureClaim());
                TooltipTrigger.Attach(item.gameObject, _game.Tooltip, () => TooltipText.遺物(def, 0));

                // 浮動:讓「這個可以拿」看得出來
                item.rectTransform.DOAnchorPosY(44f, 1.1f)
                    .SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine).SetLink(item.gameObject);

                // 名字放在寶物與箱口之間;再低就被箱身吃掉了
                UiKit.Place(UiKit.CreateText("寶物名", _root, def.Name, 28f,
                    new Color(1f, 0.92f, 0.7f)).rectTransform, new Vector2(0f, -80f), new Vector2(600f, 40f));
                UiKit.Place(UiKit.CreateText("取物提示", _root, "點擊收下", 24f,
                    new Color(0.8f, 0.8f, 0.85f)).rectTransform, new Vector2(0f, -118f), new Vector2(600f, 34f));
            }

            ShopRoomScreenController.BuildForwardArrow(_root, new Vector2(640f, -400f), "跳過",
                () => _game.TreasureLeave());
        }

        /// <summary>開箱(玩家點箱子或煙霧呼叫);只是切畫面階段,遺物還沒入包。</summary>
        public void Open()
        {
            if (_opened) return;
            _opened = true;
            Render();
        }
    }
}
