using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using STS.Core.Cards;
using STS.Core.Combat;

namespace STS.Game.UI
{
    /// <summary>
    /// 牌組檢視:整張卡面排成網格,卡多時可捲動。
    /// 三種用途共用——純檢視、選卡升級、選卡移除;不可選的卡壓暗且不接受點擊。
    /// 每次開啟現建、關閉自毀,不留跨畫面殘留。
    /// </summary>
    public sealed class DeckViewOverlay : MonoBehaviour
    {
        private const float 卡縮放 = 0.92f;
        private const float 每列張數 = 6f;

        /// <param name="upgradedLookup">
        /// 非 null 時進入「升級預覽」模式:點卡不直接執行,先顯示升級前後對照再確認。
        /// </param>
        public static DeckViewOverlay Open(Transform overlayLayer, string title,
            IReadOnlyList<CardInstance> deck, Func<CardInstance, CardDef> defLookup,
            Func<CardInstance, bool> filter, Action<int> onPick, CombatantState previewPlayer = null,
            Func<CardInstance, CardDef> upgradedLookup = null)
        {
            var panel = UiKit.CreatePanel("牌組檢視", overlayLayer, UiKit.面板色);
            UiKit.Place(panel.rectTransform, Vector2.zero, new Vector2(1500f, 860f));
            panel.transform.SetAsLastSibling();
            var view = panel.gameObject.AddComponent<DeckViewOverlay>();

            var group = panel.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.DOFade(1f, 0.18f).SetEase(Ease.OutCubic).SetLink(panel.gameObject);

            var titleText = UiKit.CreateText("標題", panel.transform, $"{title}({deck.Count} 張)", 32f);
            UiKit.Place(titleText.rectTransform, new Vector2(0f, 380f), new Vector2(1400f, 46f));

            // 捲動區:視口要有射線目標,否則指在卡與卡之間的空隙會捲不動
            var viewportImage = UiKit.CreatePanel("視口", panel.transform, new Color(0f, 0f, 0f, 0.004f));
            var viewport = viewportImage.rectTransform;
            UiKit.Place(viewport, new Vector2(0f, -20f), new Vector2(1420f, 660f));
            viewport.gameObject.AddComponent<RectMask2D>();

            var content = UiKit.CreateRect("內容", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;

            var grid = content.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(160f * 卡縮放, 220f * 卡縮放);
            grid.spacing = new Vector2(18f, 18f);
            grid.padding = new RectOffset(20, 20, 16, 16);
            grid.childAlignment = TextAnchor.UpperCenter;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = (int)每列張數;
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = panel.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;   // 到底就停,不做回彈
            scroll.scrollSensitivity = 55f;

            for (int i = 0; i < deck.Count; i++)
            {
                int index = i;
                var card = deck[i];
                var def = defLookup(card);
                bool pickable = onPick != null && (filter == null || filter(card));

                var slot = UiKit.CreateRect($"格_{i}", content);
                var face = UiKit.MakeCardFace(slot, def,
                    CardTextFormatter.FormatDescription(def, previewPlayer ?? 空玩家), 卡縮放);
                face.anchorMin = new Vector2(0.5f, 0.5f);
                face.anchorMax = new Vector2(0.5f, 0.5f);
                face.anchoredPosition = Vector2.zero;

                if (onPick == null) continue;
                if (pickable)
                {
                    var button = face.gameObject.AddComponent<Button>();
                    var previewPlayerRef = previewPlayer;
                    button.onClick.AddListener(() =>
                    {
                        if (upgradedLookup != null)
                        {
                            view.ShowUpgradePreview(def, upgradedLookup(card), previewPlayerRef, () =>
                            {
                                Destroy(view.gameObject);
                                onPick(index);
                            });
                            return;
                        }
                        Destroy(view.gameObject);
                        onPick(index);
                    });
                    // 指到時微微浮起,讓「這張可以選」看得出來
                    var hover = face.gameObject.AddComponent<CardHoverLift>();
                    hover.Setup(face);
                }
                else
                {
                    var image = face.GetComponent<Image>();
                    image.color = Color.Lerp(image.color, Color.black, 0.6f);
                    var mark = UiKit.CreateText("已升級", face, "已升級", 22f, new Color(1f, 0.85f, 0.4f));
                    UiKit.Place(mark.rectTransform, new Vector2(0f, -95f * 卡縮放), new Vector2(150f, 30f));
                }
            }

            UiKit.Place((RectTransform)UiKit.CreateButton("關閉", panel.transform, "關閉", 28f,
                new Color(0.5f, 0.3f, 0.3f), () => Destroy(view.gameObject)).transform,
                new Vector2(0f, -390f), new Vector2(200f, 54f));
            return view;
        }

        /// <summary>升級前後對照:左邊現在的卡、中間箭頭、右邊升級後,確認才真的升級。</summary>
        private void ShowUpgradePreview(CardDef current, CardDef upgraded, CombatantState previewPlayer, Action onConfirm)
        {
            var backdrop = UiKit.CreatePanel("升級預覽", transform, new Color(0.04f, 0.04f, 0.06f, 0.97f));
            // 刻意開超過面板尺寸:遮罩要蓋掉整個螢幕,不然背後的燈火畫面會透出來
            UiKit.Place(backdrop.rectTransform, Vector2.zero, new Vector2(2400f, 1400f));
            backdrop.transform.SetAsLastSibling();

            var group = backdrop.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.DOFade(1f, 0.15f).SetLink(backdrop.gameObject);

            var player = previewPlayer ?? 空玩家;
            var before = UiKit.MakeCardFace(backdrop.transform, current,
                CardTextFormatter.FormatDescription(current, player), 1.35f);
            UiKit.Place(before, new Vector2(-260f, 40f), before.sizeDelta);

            // 箭頭只用 ASCII:繁中字型沒有 ➤ 這類符號的字形,會變成豆腐方塊
            for (int i = 0; i < 3; i++)
            {
                var arrow = UiKit.CreateText($"箭頭{i}", backdrop.transform, ">", 56f, new Color(1f, 0.82f, 0.3f));
                UiKit.Place(arrow.rectTransform, new Vector2(-46f + i * 46f, 40f), new Vector2(60f, 70f));
            }

            var after = UiKit.MakeCardFace(backdrop.transform, upgraded,
                CardTextFormatter.FormatDescription(upgraded, player), 1.35f);
            UiKit.Place(after, new Vector2(260f, 40f), after.sizeDelta);
            var afterName = UiKit.CreateText("升級標記", backdrop.transform, "升級後", 28f, new Color(0.5f, 1f, 0.5f));
            UiKit.Place(afterName.rectTransform, new Vector2(260f, -145f), new Vector2(200f, 36f));
            var beforeName = UiKit.CreateText("目前標記", backdrop.transform, "目前", 28f, new Color(0.8f, 0.8f, 0.8f));
            UiKit.Place(beforeName.rectTransform, new Vector2(-260f, -145f), new Vector2(200f, 36f));

            UiKit.Place((RectTransform)UiKit.CreateButton("確認", backdrop.transform, "確認升級", 28f,
                new Color(0.3f, 0.55f, 0.35f), () => onConfirm()).transform,
                new Vector2(-160f, -280f), new Vector2(240f, 62f));
            UiKit.Place((RectTransform)UiKit.CreateButton("取消", backdrop.transform, "再看看", 28f,
                new Color(0.45f, 0.3f, 0.3f), () => Destroy(backdrop.gameObject)).transform,
                new Vector2(160f, -280f), new Vector2(240f, 62f));
        }

        /// <summary>牌組檢視在戰鬥外開啟時沒有玩家狀態可參考——用空白狀態算基礎值。</summary>
        private static readonly CombatantState 空玩家 = new CombatantState { Hp = 1, MaxHp = 1 };
    }
}
