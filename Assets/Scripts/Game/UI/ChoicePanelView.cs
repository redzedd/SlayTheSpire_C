using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using STS.Core.Combat;

namespace STS.Game.UI
{
    /// <summary>
    /// AwaitingChoice 的選牌面板:列出當前手牌,點選湊滿 PendingChoiceCount 張後按確認,
    /// 把索引回填給引擎(ResolveChoice)。面板顯示期間其餘輸入由控制器鎖住。
    /// </summary>
    public sealed class ChoicePanelView : MonoBehaviour
    {
        private CombatScreenController _controller;
        private TextMeshProUGUI _title;
        private RectTransform _listRoot;
        private Button _confirmButton;
        private readonly List<int> _selected = new List<int>();
        private readonly List<Image> _entries = new List<Image>();
        private int _requiredCount;

        public static ChoicePanelView Build(Transform parent, CombatScreenController controller)
        {
            var panel = UiKit.CreatePanel("選擇面板", parent, UiKit.面板色);
            UiKit.Place(panel.rectTransform, Vector2.zero, new Vector2(760f, 520f));
            var view = panel.gameObject.AddComponent<ChoicePanelView>();
            view._controller = controller;

            view._title = UiKit.CreateText("標題", panel.transform, "", 30f);
            UiKit.Place(view._title.rectTransform, new Vector2(0f, 220f), new Vector2(700f, 44f));

            view._listRoot = UiKit.CreateRect("清單", panel.transform);
            UiKit.Place(view._listRoot, new Vector2(0f, -10f), new Vector2(700f, 360f));
            var layout = view._listRoot.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 8f;
            layout.childForceExpandHeight = false;
            layout.childControlHeight = false;

            view._confirmButton = UiKit.CreateButton("確認", panel.transform, "確認", 28f,
                new Color(0.8f, 0.6f, 0.2f), () => view.Confirm());
            UiKit.Place(((RectTransform)view._confirmButton.transform), new Vector2(0f, -230f), new Vector2(220f, 56f));

            view.gameObject.SetActive(false);
            return view;
        }

        public void Show(CombatEngine engine)
        {
            _requiredCount = engine.State.PendingChoiceCount;
            _selected.Clear();
            _entries.Clear();
            foreach (Transform child in _listRoot)
            {
                Destroy(child.gameObject);
            }
            _title.text = $"選擇 {_requiredCount} 張手牌消耗";

            var hand = engine.State.Hand;
            for (int i = 0; i < hand.Count; i++)
            {
                int index = i;
                var def = engine.GetCardDef(hand[i]);
                var row = UiKit.CreatePanel($"項目{i}", _listRoot, new Color(0.25f, 0.25f, 0.3f));
                row.rectTransform.sizeDelta = new Vector2(680f, 44f);
                var button = row.gameObject.AddComponent<Button>();
                button.onClick.AddListener(() => Toggle(index));
                var label = UiKit.CreateText("卡名", row.transform, def.Name, 24f);
                UiKit.Stretch(label.rectTransform);
                _entries.Add(row);
            }
            RefreshVisual();
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
        }

        private void Toggle(int index)
        {
            if (_selected.Contains(index))
            {
                _selected.Remove(index);
            }
            else if (_selected.Count < _requiredCount)
            {
                _selected.Add(index);
            }
            RefreshVisual();
        }

        private void RefreshVisual()
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                _entries[i].color = _selected.Contains(i)
                    ? new Color(0.75f, 0.55f, 0.2f)
                    : new Color(0.25f, 0.25f, 0.3f);
            }
            _confirmButton.interactable = _selected.Count == _requiredCount;
        }

        private void Confirm()
        {
            gameObject.SetActive(false);
            _controller.SubmitChoice(_selected.ToArray());
        }
    }
}
