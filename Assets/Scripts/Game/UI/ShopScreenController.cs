using TMPro;
using UnityEngine;
using UnityEngine.UI;
using STS.Core.Cards;
using STS.Core.Combat;

namespace STS.Game.UI
{
    /// <summary>
    /// 商店畫面:卡(依稀有度定價)/遺物/藥水/移卡服務。買不起灰化;每次交易後整面重繪。
    /// 所有動作走 GameController(煙霧同路徑)。
    /// </summary>
    public sealed class ShopScreenController : MonoBehaviour
    {
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

            var panel = UiKit.CreatePanel("面板", _root, UiKit.面板色);
            UiKit.Place(panel.rectTransform, Vector2.zero, new Vector2(1100f, 700f));

            UiKit.Place(UiKit.CreateText("標題", panel.transform, $"商店   金幣 {run.Gold}", 34f,
                new Color(1f, 0.85f, 0.4f)).rectTransform, new Vector2(0f, 310f), new Vector2(900f, 44f));

            // 卡牌列
            var player = new CombatantState { Hp = run.Hp, MaxHp = run.MaxHp };
            int shown = 0;
            for (int i = 0; i < shop.CardIds.Count; i++)
            {
                if (shop.CardIds[i] == null) continue;
                int index = i;
                var def = _game.Db.GetCard(shop.CardIds[i]);
                float x = (shown - 2f) * 200f;
                var face = UiKit.MakeCardFace(panel.transform, def, CardTextFormatter.FormatDescription(def, player), 0.95f);
                UiKit.Place(face, new Vector2(x, 130f), face.sizeDelta);
                bool affordable = run.Gold >= shop.CardCosts[i];
                var priceText = UiKit.CreateText("價", face, $"{shop.CardCosts[i]} 金", 20f,
                    affordable ? new Color(1f, 0.9f, 0.4f) : new Color(0.6f, 0.4f, 0.4f));
                UiKit.Place(priceText.rectTransform, new Vector2(0f, -125f), new Vector2(140f, 28f));
                if (affordable)
                {
                    var button = face.gameObject.AddComponent<Button>();
                    button.onClick.AddListener(() => _game.ShopBuyCard(index));
                    face.gameObject.AddComponent<CardHoverLift>().Setup(face);
                }
                else
                {
                    face.GetComponent<Image>().color = Color.Lerp(UiKit.卡牌顏色(def.Type), Color.black, 0.5f);
                }
                shown++;
            }

            // 遺物與藥水列
            float relicX = -350f;
            for (int i = 0; i < shop.RelicIds.Count; i++)
            {
                if (shop.RelicIds[i] == null) continue;
                int index = i;
                var relic = _game.Db.GetRelicDef(shop.RelicIds[i]);
                bool affordable = run.Gold >= shop.RelicCost;
                var button = UiKit.CreateButton($"遺物{i}", panel.transform,
                    $"{relic.Name}\n{shop.RelicCost} 金", 20f,
                    affordable ? new Color(0.5f, 0.4f, 0.2f) : new Color(0.3f, 0.3f, 0.3f),
                    () => _game.ShopBuyRelic(index));
                button.interactable = affordable;
                UiKit.Place((RectTransform)button.transform, new Vector2(relicX, -90f), new Vector2(240f, 72f));
                // 買不起也要能看效果:提示掛在按鈕上,interactable=false 不影響 hover
                TooltipTrigger.Attach(button.gameObject, _game.Tooltip, () => TooltipText.遺物(relic, 0));
                relicX += 260f;
            }
            float potionX = 200f;
            for (int i = 0; i < shop.PotionIds.Count; i++)
            {
                if (shop.PotionIds[i] == null) continue;
                int index = i;
                var potion = _game.Db.GetPotion(shop.PotionIds[i]);
                bool affordable = run.Gold >= shop.PotionCost;
                var button = UiKit.CreateButton($"藥水{i}", panel.transform,
                    $"{potion.Name}\n{shop.PotionCost} 金", 20f,
                    affordable ? new Color(0.35f, 0.25f, 0.5f) : new Color(0.3f, 0.3f, 0.3f),
                    () => _game.ShopBuyPotion(index));
                button.interactable = affordable;
                UiKit.Place((RectTransform)button.transform, new Vector2(potionX, -90f), new Vector2(220f, 72f));
                TooltipTrigger.Attach(button.gameObject, _game.Tooltip, () => TooltipText.藥水(potion));
                potionX += 240f;
            }

            // 移卡服務與離開
            bool canRemove = run.Gold >= shop.RemoveCost && run.Deck.Count > 0;
            var removeButton = UiKit.CreateButton("移卡", panel.transform,
                $"移除卡牌服務:{shop.RemoveCost} 金", 24f,
                canRemove ? new Color(0.55f, 0.25f, 0.25f) : new Color(0.3f, 0.3f, 0.3f),
                () => _game.ShopOpenRemovePicker());
            removeButton.interactable = canRemove;
            UiKit.Place((RectTransform)removeButton.transform, new Vector2(-200f, -230f), new Vector2(360f, 60f));

            UiKit.Place((RectTransform)UiKit.CreateButton("離開", panel.transform, "離開商店", 26f,
                new Color(0.3f, 0.5f, 0.35f), () => _game.ShopLeave()).transform,
                new Vector2(250f, -230f), new Vector2(220f, 60f));
        }
    }
}
