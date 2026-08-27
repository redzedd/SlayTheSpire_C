using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using STS.Core.Combat;

namespace STS.Game.UI
{
    /// <summary>
    /// 頂部資訊列(參考原作版面):生命、金幣、藥水欄、樓層、牌組。
    /// 藥水欄固定三格——空格也畫出來,玩家才知道自己有幾個位子。
    /// </summary>
    public sealed class TopBarView : MonoBehaviour
    {
        private const float 高度 = 76f;

        private GameController _game;
        private CombatEngine _engine;
        private TextMeshProUGUI _hpText;
        private TextMeshProUGUI _goldText;
        private TextMeshProUGUI _floorText;
        private TextMeshProUGUI _deckText;
        private RectTransform _potionRow;
        private Action<int> _onPotionClicked;

        public static TopBarView Build(Transform parent, GameController game, CombatEngine engine,
            Action<int> onPotionClicked)
        {
            var bar = UiKit.CreatePanel("頂部資訊列", parent, new Color(0.09f, 0.09f, 0.12f, 0.96f));
            bar.rectTransform.anchorMin = new Vector2(0f, 1f);
            bar.rectTransform.anchorMax = new Vector2(1f, 1f);
            bar.rectTransform.pivot = new Vector2(0.5f, 1f);
            bar.rectTransform.anchoredPosition = Vector2.zero;
            bar.rectTransform.sizeDelta = new Vector2(0f, 高度);

            var view = bar.gameObject.AddComponent<TopBarView>();
            view._game = game;
            view._engine = engine;
            view._onPotionClicked = onPotionClicked;

            // 生命(紅心)
            var heart = UiKit.CreatePanel("心", bar.transform, new Color(0.85f, 0.2f, 0.22f));
            UiKit.Place(heart.rectTransform, new Vector2(40f, 0f), new Vector2(30f, 30f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f));
            view._hpText = UiKit.CreateText("生命", bar.transform, "", 28f, Color.white, TextAlignmentOptions.Left);
            UiKit.Place(view._hpText.rectTransform, new Vector2(82f, 0f), new Vector2(180f, 40f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f));

            // 金幣
            var coin = UiKit.CreatePanel("幣", bar.transform, new Color(0.92f, 0.76f, 0.24f));
            UiKit.Place(coin.rectTransform, new Vector2(270f, 0f), new Vector2(30f, 30f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f));
            view._goldText = UiKit.CreateText("金幣", bar.transform, "", 28f, Color.white, TextAlignmentOptions.Left);
            UiKit.Place(view._goldText.rectTransform, new Vector2(312f, 0f), new Vector2(160f, 40f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f));

            // 藥水欄(固定三格)
            view._potionRow = UiKit.CreateRect("藥水欄", bar.transform);
            UiKit.Place(view._potionRow, new Vector2(500f, 0f), new Vector2(400f, 56f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f));

            // 樓層與牌組(右側)
            view._floorText = UiKit.CreateText("樓層", bar.transform, "", 26f, Color.white, TextAlignmentOptions.Right);
            UiKit.Place(view._floorText.rectTransform, new Vector2(-260f, 0f), new Vector2(220f, 40f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f));

            var deckButton = UiKit.CreateButton("牌組", bar.transform, "", 24f, new Color(0.24f, 0.26f, 0.34f),
                () => game.OpenDeckView());
            UiKit.Place((RectTransform)deckButton.transform, new Vector2(-30f, 0f), new Vector2(170f, 52f),
                new Vector2(1f, 0.5f), new Vector2(1f, 0.5f));
            view._deckText = deckButton.GetComponentInChildren<TextMeshProUGUI>();

            view.Refresh();
            return view;
        }

        public void Refresh()
        {
            var player = _engine.State.Player;
            _hpText.text = $"{player.Hp}/{player.MaxHp}";
            var run = _game.Run != null ? _game.Run.State : null;
            _goldText.text = run != null ? run.Gold.ToString() : "-";
            _floorText.text = run != null ? $"樓層 {run.Floor}" : "";
            _deckText.text = run != null ? $"牌組 {run.Deck.Count}" : "牌組";
            RebuildPotions();
        }

        private void RebuildPotions()
        {
            foreach (Transform child in _potionRow)
            {
                Destroy(child.gameObject);
            }
            var slots = _engine.State.PotionSlots;
            int shown = Mathf.Max(slots.Count, 3);
            for (int i = 0; i < shown; i++)
            {
                int slot = i;
                string potionId = i < slots.Count ? slots[i] : null;
                bool filled = potionId != null;
                var chip = UiKit.CreatePanel($"藥水格{i}", _potionRow,
                    filled ? new Color(0.42f, 0.28f, 0.6f) : new Color(0.2f, 0.2f, 0.24f, 0.8f));
                UiKit.Place(chip.rectTransform, new Vector2(i * 128f, 0f), new Vector2(120f, 50f),
                    new Vector2(0f, 0.5f), new Vector2(0f, 0.5f));
                if (!filled) continue;

                var def = _game.Db.GetPotion(potionId);
                var label = UiKit.CreateText("名", chip.transform, def.Name, 20f);
                UiKit.Stretch(label.rectTransform);
                var button = chip.gameObject.AddComponent<Button>();
                button.onClick.AddListener(() => _onPotionClicked(slot));
                TooltipTrigger.Attach(chip.gameObject, _game.Tooltip, () => TooltipText.藥水(def));
            }
        }
    }
}
