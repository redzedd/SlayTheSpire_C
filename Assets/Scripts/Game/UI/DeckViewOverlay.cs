using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using STS.Core.Cards;

namespace STS.Game.UI
{
    /// <summary>
    /// 牌組檢視(Run 層):純看,或帶篩選的選卡模式(升級/移除用)。
    /// 每次開啟現建、關閉自毀——不留跨畫面殘留。
    /// </summary>
    public sealed class DeckViewOverlay : MonoBehaviour
    {
        /// <summary>檢視模式:onPick=null 純看;filter 回傳 false 的卡不可選(灰化)。</summary>
        public static DeckViewOverlay Open(Transform overlayLayer, string title,
            IReadOnlyList<CardInstance> deck, Func<CardInstance, CardDef> defLookup,
            Func<CardInstance, bool> filter, Action<int> onPick)
        {
            var panel = UiKit.CreatePanel("牌組檢視", overlayLayer, UiKit.面板色);
            UiKit.Place(panel.rectTransform, Vector2.zero, new Vector2(900f, 640f));
            panel.transform.SetAsLastSibling();
            var view = panel.gameObject.AddComponent<DeckViewOverlay>();

            var titleText = UiKit.CreateText("標題", panel.transform, $"{title}({deck.Count})", 30f);
            UiKit.Place(titleText.rectTransform, new Vector2(0f, 285f), new Vector2(860f, 44f));

            var listRoot = UiKit.CreateRect("清單", panel.transform);
            UiKit.Place(listRoot, new Vector2(0f, 0f), new Vector2(840f, 480f));
            var layout = listRoot.gameObject.AddComponent<GridLayoutGroup>();
            layout.cellSize = new Vector2(200f, 52f);
            layout.spacing = new Vector2(8f, 8f);

            for (int i = 0; i < deck.Count; i++)
            {
                int index = i;
                var card = deck[i];
                var def = defLookup(card);
                bool pickable = onPick != null && (filter == null || filter(card));
                var row = UiKit.CreatePanel($"卡_{i}", listRoot,
                    pickable || onPick == null ? UiKit.卡牌顏色(def.Type) : new Color(0.3f, 0.3f, 0.3f));
                var text = UiKit.CreateText("名", row.transform, def.Name, 22f);
                UiKit.Stretch(text.rectTransform);
                if (pickable)
                {
                    var button = row.gameObject.AddComponent<Button>();
                    button.onClick.AddListener(() =>
                    {
                        Destroy(view.gameObject);
                        onPick(index);
                    });
                }
            }

            UiKit.Place((RectTransform)UiKit.CreateButton("關閉", panel.transform, "關閉", 26f,
                new Color(0.5f, 0.3f, 0.3f), () => Destroy(view.gameObject)).transform,
                new Vector2(0f, -290f), new Vector2(180f, 50f));
            return view;
        }
    }
}
