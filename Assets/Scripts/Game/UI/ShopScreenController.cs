using UnityEngine;
using UnityEngine.UI;
using STS.Core.Cards;
using STS.Core.Combat;

namespace STS.Game.UI
{
    /// <summary>
    /// 商店貨架(版面參考附圖):上排職業牌、下左無色牌、中下遺物(上)與藥水(下)、右下圓形移卡服務。
    /// 買不起就壓暗且不可點;每次交易後整面重繪。所有動作走 GameController(煙霧同路徑)。
    /// 職業牌/無色牌共用 Shop.CardIds 的索引,靠 Shop.ClassCardCount 切開,買牌介面才不用分兩套。
    /// </summary>
    public sealed class ShopScreenController : MonoBehaviour
    {
        private const float 卡縮放 = 1.1f;

        private GameController _game;
        private RectTransform _root;

        public static ShopScreenController Build(Transform parent, GameController game)
        {
            var root = UiKit.CreateRect("商店畫面", parent);
            UiKit.Stretch(root);
            var controller = root.gameObject.AddComponent<ShopScreenController>();
            controller._game = game;
            controller._root = root;
            controller.Render();
            return controller;
        }

        public void Render()
        {
            foreach (Transform child in _root)
            {
                Destroy(child.gameObject);
            }
            var shop = _game.Run.Shop;
            var run = _game.Run.State;
            var player = new CombatantState { Hp = run.Hp, MaxHp = run.MaxHp };

            // 綠檯布:商人把貨攤在上面
            var cloth = UiKit.CreatePanel("檯布", _root, new Color(0.29f, 0.45f, 0.42f));
            UiKit.Place(cloth.rectTransform, new Vector2(0f, -10f), new Vector2(1700f, 940f));

            UiKit.Place(UiKit.CreateText("金幣", cloth.transform, $"金幣 {run.Gold}", 34f,
                new Color(1f, 0.87f, 0.35f)).rectTransform, new Vector2(0f, 420f), new Vector2(600f, 46f));

            int classCount = Mathf.Min(shop.ClassCardCount, shop.CardIds.Count);
            // 上排:職業牌
            for (int i = 0; i < classCount; i++)
            {
                BuildCardSlot(cloth.transform, shop, i, player,
                    new Vector2((i - (classCount - 1) / 2f) * 272f, 170f));
            }
            // 下左:無色牌
            for (int i = classCount; i < shop.CardIds.Count; i++)
            {
                int slot = i - classCount;
                BuildCardSlot(cloth.transform, shop, i, player,
                    new Vector2(-470f + slot * 250f, -180f));
            }

            // 中下:上半遺物、下半藥水
            for (int i = 0; i < shop.RelicIds.Count; i++)
            {
                BuildRelicSlot(cloth.transform, shop, i, run.Gold, new Vector2(60f + i * 155f, -120f));
            }
            for (int i = 0; i < shop.PotionIds.Count; i++)
            {
                BuildPotionSlot(cloth.transform, shop, i, run.Gold, new Vector2(60f + i * 155f, -290f));
            }

            BuildRemoveService(cloth.transform, shop, run.Gold, run.Deck.Count, new Vector2(600f, -215f));

            // 左下:回商人房間
            UiKit.Place((RectTransform)UiKit.CreateButton("返回", cloth.transform, "< 返回", 28f,
                new Color(0.55f, 0.22f, 0.2f), () => _game.ShopBackToRoom()).transform,
                new Vector2(-700f, -400f), new Vector2(200f, 66f));
        }

        private void BuildCardSlot(Transform parent, Core.Run.ShopInventory shop, int index,
            CombatantState player, Vector2 pos)
        {
            if (shop.CardIds[index] == null)
            {
                // 售出的位置留白,版面不塌
                var sold = UiKit.CreateText($"售出{index}", parent, "已售出", 22f, new Color(1f, 1f, 1f, 0.35f));
                UiKit.Place(sold.rectTransform, pos, new Vector2(180f, 40f));
                return;
            }
            var def = _game.Db.GetCard(shop.CardIds[index]);
            int cost = shop.CardCosts[index];
            bool affordable = _game.Run.State.Gold >= cost;

            var face = UiKit.MakeCardFace(parent, def, CardTextFormatter.FormatDescription(def, player), 卡縮放);
            UiKit.Place(face, pos, face.sizeDelta);
            var priceText = UiKit.CreateText("價", parent, $"{cost} 金", 24f,
                affordable ? new Color(1f, 0.88f, 0.35f) : new Color(0.65f, 0.42f, 0.42f));
            UiKit.Place(priceText.rectTransform, pos + new Vector2(0f, -140f), new Vector2(160f, 32f));

            if (affordable)
            {
                var button = face.gameObject.AddComponent<Button>();
                int captured = index;
                button.onClick.AddListener(() => _game.ShopBuyCard(captured));
                face.gameObject.AddComponent<CardHoverLift>().Setup(face);
            }
            else
            {
                face.GetComponent<Image>().color = Color.Lerp(UiKit.卡牌顏色(def.Type), Color.black, 0.5f);
            }
        }

        private void BuildRelicSlot(Transform parent, Core.Run.ShopInventory shop, int index, int gold, Vector2 pos)
        {
            if (shop.RelicIds[index] == null) return;
            var relic = _game.Db.GetRelicDef(shop.RelicIds[index]);
            bool affordable = gold >= shop.RelicCost;

            var icon = UiKit.CreatePanel($"遺物{index}", parent,
                affordable ? new Color(0.66f, 0.42f, 0.2f) : new Color(0.33f, 0.28f, 0.24f));
            UiKit.Place(icon.rectTransform, pos, new Vector2(96f, 96f));
            UiKit.Place(UiKit.CreateText("名", icon.transform, relic.Name, 17f).rectTransform,
                Vector2.zero, new Vector2(92f, 88f));
            UiKit.Place(UiKit.CreateText("價", parent, $"{shop.RelicCost} 金", 21f,
                affordable ? new Color(1f, 0.88f, 0.35f) : new Color(0.65f, 0.42f, 0.42f)).rectTransform,
                pos + new Vector2(0f, -68f), new Vector2(140f, 28f));

            var button = icon.gameObject.AddComponent<Button>();
            int captured = index;
            button.onClick.AddListener(() => _game.ShopBuyRelic(captured));
            button.interactable = affordable;
            // 買不起也要看得到效果:提示掛在物件上,interactable=false 不影響 hover
            TooltipTrigger.Attach(icon.gameObject, _game.Tooltip, () => TooltipText.遺物(relic, 0));
        }

        private void BuildPotionSlot(Transform parent, Core.Run.ShopInventory shop, int index, int gold, Vector2 pos)
        {
            if (shop.PotionIds[index] == null) return;
            var potion = _game.Db.GetPotion(shop.PotionIds[index]);
            bool affordable = gold >= shop.PotionCost;

            var icon = UiKit.CreatePanel($"藥水{index}", parent,
                affordable ? new Color(0.42f, 0.28f, 0.6f) : new Color(0.28f, 0.24f, 0.32f));
            UiKit.Place(icon.rectTransform, pos, new Vector2(96f, 96f));
            UiKit.Place(UiKit.CreateText("名", icon.transform, potion.Name, 17f).rectTransform,
                Vector2.zero, new Vector2(92f, 88f));
            UiKit.Place(UiKit.CreateText("價", parent, $"{shop.PotionCost} 金", 21f,
                affordable ? new Color(1f, 0.88f, 0.35f) : new Color(0.65f, 0.42f, 0.42f)).rectTransform,
                pos + new Vector2(0f, -68f), new Vector2(140f, 28f));

            var button = icon.gameObject.AddComponent<Button>();
            int captured = index;
            button.onClick.AddListener(() => _game.ShopBuyPotion(captured));
            button.interactable = affordable;
            TooltipTrigger.Attach(icon.gameObject, _game.Tooltip, () => TooltipText.藥水(potion));
        }

        private void BuildRemoveService(Transform parent, Core.Run.ShopInventory shop, int gold, int deckCount, Vector2 pos)
        {
            bool canRemove = gold >= shop.RemoveCost && deckCount > 0;
            var coin = UiKit.CreateCircle("移卡服務", parent,
                canRemove ? new Color(0.83f, 0.68f, 0.28f) : new Color(0.4f, 0.36f, 0.26f));
            UiKit.Place(coin.rectTransform, pos, new Vector2(190f, 190f));
            UiKit.Place(UiKit.CreateText("圖示", coin.transform, "移除\n卡牌", 28f, new Color(0.15f, 0.12f, 0.06f)).rectTransform,
                Vector2.zero, new Vector2(180f, 120f));
            UiKit.Place(UiKit.CreateText("價", parent, $"{shop.RemoveCost} 金", 24f,
                canRemove ? new Color(1f, 0.88f, 0.35f) : new Color(0.65f, 0.42f, 0.42f)).rectTransform,
                pos + new Vector2(0f, -122f), new Vector2(160f, 32f));

            var button = coin.gameObject.AddComponent<Button>();
            button.onClick.AddListener(() => _game.ShopOpenRemovePicker());
            button.interactable = canRemove;
        }
    }
}
