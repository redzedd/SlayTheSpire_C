using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using STS.Core.Cards;
using STS.Core.Combat;

namespace STS.Game.UI
{
    /// <summary>
    /// 戰後獎勵(版面參考附圖)。兩種模式共用同一個畫面根:
    /// 清單模式 =「搜刮!」布條 + 逐項可領的清單 + 右側「跳過」箭頭;
    /// 選卡模式 =「選擇一張牌」布條 + 三張整卡 + 「跳過」。
    /// 每一項都要玩家自己點才入帳(RunEngine 不再自動入帳),領完清單自動只剩沒領的。
    /// </summary>
    public sealed class RewardScreenController : MonoBehaviour
    {
        private GameController _game;
        private RectTransform _root;
        private bool _cardPickMode;

        public static RewardScreenController Build(Transform parent, GameController game)
        {
            var root = UiKit.CreateRect("獎勵畫面", parent);
            UiKit.Stretch(root);
            var controller = root.gameObject.AddComponent<RewardScreenController>();
            controller._game = game;
            controller._root = root;
            controller.Render();
            return controller;
        }

        /// <summary>切到選卡模式(清單上的「將一張牌加入你的牌組」)。</summary>
        public void OpenCardPick()
        {
            _cardPickMode = true;
            Render();
        }

        public void BackToList()
        {
            _cardPickMode = false;
            Render();
        }

        public void Render()
        {
            foreach (Transform child in _root)
            {
                Destroy(child.gameObject);
            }
            if (_cardPickMode) RenderCardPick();
            else RenderList();
        }

        // ---- 清單模式 ----

        private void RenderList()
        {
            var rewards = _game.Run.PendingRewards;
            BuildBanner("搜刮!", new Vector2(0f, 320f));

            var rows = new List<RewardRow>();
            if (rewards.HasGold)
            {
                rows.Add(new RewardRow($"{rewards.Gold} 金幣", new Color(0.95f, 0.78f, 0.25f),
                    () => _game.RewardClaimGold()));
            }
            if (rewards.HasPotion)
            {
                var potion = _game.Db.GetPotion(rewards.PotionId);
                // 指上去先看清楚效果再決定要不要占掉一個藥水格
                rows.Add(new RewardRow(potion.Name, new Color(0.55f, 0.4f, 0.8f),
                    () => _game.RewardClaimPotion(), () => TooltipText.藥水(potion, inCombat: false)));
            }
            if (rewards.HasRelic)
            {
                var relic = _game.Db.GetRelicDef(rewards.RelicId);
                rows.Add(new RewardRow(relic.Name, new Color(0.7f, 0.5f, 0.25f),
                    () => _game.RewardClaimRelic(), () => TooltipText.遺物(relic, 0)));
            }
            if (rewards.HasCard)
            {
                rows.Add(new RewardRow("將一張牌加入你的牌組。", new Color(0.6f, 0.62f, 0.68f),
                    () => _game.RewardOpenCardPick()));
            }

            // 每列 76 高 + 20 間距,上下各留 24 邊距——面板高度跟著列數走,不留一截空白
            float panelHeight = Mathf.Max(140f, rows.Count * 96f + 28f);
            var panel = UiKit.CreatePanel("清單", _root, new Color(0.13f, 0.18f, 0.23f, 0.96f));
            UiKit.Place(panel.rectTransform, new Vector2(0f, 40f), new Vector2(560f, panelHeight));

            if (rows.Count == 0)
            {
                UiKit.Place(UiKit.CreateText("空", panel.transform, "都搜刮完了。", 26f).rectTransform,
                    Vector2.zero, new Vector2(500f, 40f));
            }
            float top = panelHeight * 0.5f - 62f;
            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                var button = UiKit.CreateButton($"獎勵{i}", panel.transform, row.Label, 26f,
                    new Color(0.24f, 0.55f, 0.6f), row.OnClick);
                UiKit.Place((RectTransform)button.transform, new Vector2(0f, top - i * 96f), new Vector2(480f, 76f));
                // 左側色塊當圖示佔位,讓每一列一眼分得出是什麼
                var icon = UiKit.CreatePanel("圖示", button.transform, row.IconColor);
                UiKit.Place(icon.rectTransform, new Vector2(-200f, 0f), new Vector2(48f, 48f));
                icon.raycastTarget = false;
                if (row.Tooltip != null)
                {
                    TooltipTrigger.Attach(button.gameObject, _game.Tooltip, row.Tooltip);
                }
            }

            ShopRoomScreenController.BuildForwardArrow(_root, new Vector2(640f, -280f), "跳過",
                () => _game.RewardLeave());
        }

        // ---- 選卡模式 ----

        private void RenderCardPick()
        {
            var rewards = _game.Run.PendingRewards;
            BuildBanner("選擇一張牌", new Vector2(0f, 320f));

            var player = new CombatantState { Hp = _game.Run.State.Hp, MaxHp = _game.Run.State.MaxHp };
            for (int i = 0; i < rewards.CardChoices.Count; i++)
            {
                int index = i;
                var def = _game.Db.GetCard(rewards.CardChoices[i]);
                var face = UiKit.MakeCardFace(_root, def, CardTextFormatter.FormatDescription(def, player), 1.45f);
                UiKit.Place(face, new Vector2((i - (rewards.CardChoices.Count - 1) / 2f) * 300f, 10f), face.sizeDelta);
                var button = face.gameObject.AddComponent<Button>();
                button.onClick.AddListener(() => _game.RewardTakeCard(index));
                face.gameObject.AddComponent<CardHoverLift>().Setup(face);
            }

            // 「先不拿」只是退回清單,那一項仍在——真的不要就從清單按「跳過」離開整個獎勵畫面
            UiKit.Place((RectTransform)UiKit.CreateButton("返回", _root, "先不拿", 30f,
                new Color(0.24f, 0.5f, 0.58f), () => _game.RewardBackToCardList()).transform,
                new Vector2(0f, -330f), new Vector2(320f, 76f));
        }

        // ---- 共用 ----

        private void BuildBanner(string title, Vector2 pos)
        {
            UiKit.CreateBanner(_root, title, pos);
        }

        private readonly struct RewardRow
        {
            internal readonly string Label;
            internal readonly Color IconColor;
            internal readonly System.Action OnClick;
            /// <summary>指上去顯示什麼;null = 這一項沒有可預覽的內容(金幣、卡牌那一列)。</summary>
            internal readonly System.Func<string> Tooltip;

            internal RewardRow(string label, Color iconColor, System.Action onClick,
                System.Func<string> tooltip = null)
            {
                Label = label;
                IconColor = iconColor;
                OnClick = onClick;
                Tooltip = tooltip;
            }
        }
    }
}
