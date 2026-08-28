using System.Collections.Generic;
using UnityEngine;
using STS.Core.Cards;
using STS.Core.Combat;

namespace STS.Game.UI
{
    /// <summary>
    /// 手牌扇形佈局。以 InstanceId 做增量更新:新抽的牌從抽牌堆飛入、打掉的牌飛出後自毀,
    /// 留在手上的牌只補間到新槽位——整批重建看不出「哪張是新來的」,手感差很多。
    /// 佈局只在手牌變動時計算,絕不每幀重排。
    /// </summary>
    public sealed class HandView : MonoBehaviour
    {
        /// <summary>抽牌堆在手牌座標系的大致位置(左下),新卡從這裡飛出來。</summary>
        private static readonly Vector2 抽牌堆方向 = new Vector2(-760f, -140f);

        private readonly Dictionary<int, CardView> _cards = new Dictionary<int, CardView>();
        private readonly List<CardView> _ordered = new List<CardView>();
        private readonly HashSet<int> _aliveIds = new HashSet<int>();
        private readonly List<int> _removeBuffer = new List<int>();
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
            var hand = engine.State.Hand;
            var previewTarget = DefaultPreviewTarget(engine);

            _aliveIds.Clear();
            for (int i = 0; i < hand.Count; i++) _aliveIds.Add(hand[i].InstanceId);

            // 已不在手上的:飛出後自毀
            _removeBuffer.Clear();
            foreach (var pair in _cards)
            {
                if (!_aliveIds.Contains(pair.Key)) _removeBuffer.Add(pair.Key);
            }
            foreach (int id in _removeBuffer)
            {
                var leaving = _cards[id];
                _cards.Remove(id);
                if (leaving != null) leaving.AnimateOutAndDestroy();
            }

            // 新卡建立(從抽牌堆方向飛入),舊卡只更新綁定
            _ordered.Clear();
            for (int i = 0; i < hand.Count; i++)
            {
                var instance = hand[i];
                var def = engine.GetCardDef(instance);
                if (!_cards.TryGetValue(instance.InstanceId, out var view) || view == null)
                {
                    view = CardView.Build(transform, _controller);
                    view.SetSlot(抽牌堆方向, 0f, true);
                    view.PlayDrawIn();
                    _cards[instance.InstanceId] = view;
                }
                view.Bind(i, instance.InstanceId, def,
                    CardTextFormatter.FormatDescription(def, engine.State.Player, previewTarget, engine),
                    engine.GetCardCost(def));
                _ordered.Add(view);
            }
            Relayout();
        }

        /// <summary>選卡模式:切換某張卡的選取外觀。</summary>
        public void SetSelected(int handIndex, bool selected)
        {
            if (handIndex < 0 || handIndex >= _ordered.Count) return;
            _ordered[handIndex].SetChoiceSelected(selected);
        }

        public void ClearSelections()
        {
            foreach (var card in _ordered)
            {
                if (card != null) card.SetChoiceSelected(false);
            }
        }

        private void Relayout()
        {
            int count = _ordered.Count;
            if (count == 0) return;
            float spacing = Mathf.Min(140f, 1000f / count);
            float mid = (count - 1) / 2f;
            for (int i = 0; i < count; i++)
            {
                float offset = i - mid;
                var pos = new Vector2(offset * spacing, -Mathf.Abs(offset) * 14f);
                float rot = -offset * 4f;
                _ordered[i].SetSlot(pos, rot, false);
                _ordered[i].transform.SetSiblingIndex(i);
            }
        }
    }
}
