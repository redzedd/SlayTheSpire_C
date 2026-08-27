using System.Collections.Generic;
using UnityEngine;
using STS.Core.Cards;
using STS.Core.Combat;

namespace STS.Game.UI
{
    /// <summary>
    /// 手牌扇形佈局。RefreshAll 時整批重建(佔位期做法,M7 手感期再改增量+物件池);
    /// 佈局只在變更時計算與 tween,絕不每幀重排。
    /// </summary>
    public sealed class HandView : MonoBehaviour
    {
        private readonly List<CardView> _cards = new List<CardView>();
        private CombatScreenController _controller;
        private CanvasGroup _group;

        public static HandView Build(Transform parent, CombatScreenController controller)
        {
            var root = UiKit.CreateRect("手牌區", parent);
            UiKit.Place(root, new Vector2(0f, 150f), new Vector2(1200f, 300f), new Vector2(0.5f, 0f));
            var view = root.gameObject.AddComponent<HandView>();
            view._controller = controller;
            view._group = root.gameObject.AddComponent<CanvasGroup>();
            return view;
        }

        /// <summary>播放期間鎖住整個手牌互動(轉場鐵律:輸入封鎖是播放的一部分)。</summary>
        public void SetInteractable(bool value)
        {
            _group.interactable = value;
            _group.blocksRaycasts = value;
        }

        /// <summary>
        /// 預設預覽目標:場上恰好一個活敵時就是牠(單敵戰鬥的傷害數字直接反映易傷);
        /// 多敵時回 null——由拖曳指向哪隻決定,避免亂猜一個目標騙玩家。
        /// </summary>
        public static CombatantState DefaultPreviewTarget(CombatEngine engine)
        {
            CombatantState found = null;
            foreach (var enemy in engine.State.Enemies)
            {
                if (!enemy.IsAlive) continue;
                if (found != null) return null;
                found = enemy;
            }
            return found;
        }

        public void Rebuild(CombatEngine engine)
        {
            foreach (var card in _cards)
            {
                if (card != null) Destroy(card.gameObject);
            }
            _cards.Clear();

            var hand = engine.State.Hand;
            var previewTarget = DefaultPreviewTarget(engine);
            for (int i = 0; i < hand.Count; i++)
            {
                var def = engine.GetCardDef(hand[i]);
                var view = CardView.Build(transform, _controller);
                view.Bind(i, def, CardTextFormatter.FormatDescription(def, engine.State.Player, previewTarget));
                _cards.Add(view);
            }
            Relayout(true);
        }

        private void Relayout(bool immediate)
        {
            int count = _cards.Count;
            if (count == 0) return;
            float spacing = Mathf.Min(140f, 1000f / count);
            float mid = (count - 1) / 2f;
            for (int i = 0; i < count; i++)
            {
                float offset = i - mid;
                var pos = new Vector2(offset * spacing, -Mathf.Abs(offset) * 14f);
                float rot = -offset * 4f;
                _cards[i].SetSlot(pos, rot, immediate);
            }
        }
    }
}
