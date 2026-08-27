using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using STS.Core.Cards;
using STS.Core.Combat;

namespace STS.Game.UI
{
    /// <summary>
    /// 牌堆內容清單(抽牌/棄牌/消耗堆共用)。抽牌堆按卡名排序顯示——
    /// 不能洩漏真實堆序,那是玩家不該知道的資訊。
    /// </summary>
    public sealed class PileListOverlay : MonoBehaviour
    {
        private TextMeshProUGUI _title;
        private TextMeshProUGUI _content;

        public static PileListOverlay Build(Transform parent)
        {
            var panel = UiKit.CreatePanel("牌堆清單", parent, UiKit.面板色);
            UiKit.Place(panel.rectTransform, Vector2.zero, new Vector2(640f, 560f));
            var view = panel.gameObject.AddComponent<PileListOverlay>();

            view._title = UiKit.CreateText("標題", panel.transform, "", 30f);
            UiKit.Place(view._title.rectTransform, new Vector2(0f, 240f), new Vector2(600f, 44f));

            view._content = UiKit.CreateText("內容", panel.transform, "", 24f, null, TextAlignmentOptions.Top);
            UiKit.Place(view._content.rectTransform, new Vector2(0f, -30f), new Vector2(560f, 420f));
            view._content.textWrappingMode = TextWrappingModes.Normal;

            UiKit.CreateButton("關閉", panel.transform, "關閉", 26f, new Color(0.5f, 0.3f, 0.3f),
                () => view.gameObject.SetActive(false));
            UiKit.Place((RectTransform)panel.transform.Find("關閉"), new Vector2(0f, -250f), new Vector2(180f, 50f));

            view.gameObject.SetActive(false);
            return view;
        }

        public void Show(CombatEngine engine, string pileName, List<CardInstance> pile, bool hideOrder)
        {
            _title.text = $"{pileName}({pile.Count})";
            var names = new List<string>(pile.Count);
            foreach (var card in pile)
            {
                names.Add(engine.GetCardDef(card).Name);
            }
            if (hideOrder) names.Sort(string.CompareOrdinal);
            _content.text = names.Count == 0 ? "(空)" : string.Join("、", names);
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
        }
    }
}
