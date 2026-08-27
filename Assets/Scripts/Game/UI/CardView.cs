using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using STS.Core.Cards;
using STS.Core.Combat;

namespace STS.Game.UI
{
    /// <summary>
    /// 手牌上的一張卡:色塊+費用+名稱+描述(數值即時代入)。
    /// 互動:hover 抬升、拖曳出牌(放開時交給 CombatScreenController 裁決)。
    /// 佈局目標(槽位)由 HandView 指定;所有位移 tween 先 DOKill 再開新的,不疊加。
    /// </summary>
    public sealed class CardView : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public const float 寬 = 160f;
        public const float 高 = 220f;

        public int HandIndex;
        public CardDef Def { get; private set; }
        public bool RequiresTarget { get; private set; }

        private CombatScreenController _controller;
        private RectTransform _rect;
        private Image _background;
        private Vector2 _slotPos;
        private Vector3 _slotEuler;
        private bool _dragging;
        private bool _hovered;

        public RectTransform Rect => _rect;

        public static CardView Build(Transform parent, CombatScreenController controller)
        {
            var background = UiKit.CreatePanel("卡牌", parent, Color.gray);
            UiKit.Place(background.rectTransform, Vector2.zero, new Vector2(寬, 高));
            var view = background.gameObject.AddComponent<CardView>();
            view._controller = controller;
            view._rect = background.rectTransform;
            view._background = background;

            var costOrb = UiKit.CreatePanel("費用底", background.transform, new Color(0.95f, 0.75f, 0.2f));
            UiKit.Place(costOrb.rectTransform, new Vector2(-寬 / 2f + 22f, 高 / 2f - 22f), new Vector2(40f, 40f));
            costOrb.raycastTarget = false;
            var costText = UiKit.CreateText("費用", costOrb.transform, "1", 26f, Color.black);
            UiKit.Stretch(costText.rectTransform);
            view._costText = costText;

            view._nameText = UiKit.CreateText("卡名", background.transform, "", 24f);
            UiKit.Place(view._nameText.rectTransform, new Vector2(0f, 高 / 2f - 46f), new Vector2(寬 - 16f, 32f));

            view._descText = UiKit.CreateText("描述", background.transform, "", 19f);
            UiKit.Place(view._descText.rectTransform, new Vector2(0f, -30f), new Vector2(寬 - 20f, 130f));
            view._descText.textWrappingMode = TextWrappingModes.Normal;
            return view;
        }

        private TextMeshProUGUI _costText;
        private TextMeshProUGUI _nameText;
        private TextMeshProUGUI _descText;

        public void Bind(int handIndex, CardDef def, string formattedDescription)
        {
            HandIndex = handIndex;
            Def = def;
            RequiresTarget = ComputeRequiresTarget(def);
            _background.color = UiKit.卡牌顏色(def.Type);
            _costText.text = def.CostIsX ? "X" : def.Cost.ToString();
            _nameText.text = def.Name;
            _descText.text = formattedDescription;
            name = $"卡牌_{def.Id}";
        }

        /// <summary>依指定目標重算描述數值(拖曳指向敵人時即時更新)。</summary>
        public void RefreshDescription(CombatantState player, CombatantState target)
        {
            _descText.text = CardTextFormatter.FormatDescription(Def, player, target);
        }

        private static bool ComputeRequiresTarget(CardDef def)
        {
            foreach (var step in def.Steps)
            {
                if (step.Target == EffectTarget.TargetEnemy) return true;
            }
            return false;
        }

        /// <summary>HandView 指定槽位;immediate 用在初始擺放。</summary>
        public void SetSlot(Vector2 anchoredPos, float zRotation, bool immediate)
        {
            _slotPos = anchoredPos;
            _slotEuler = new Vector3(0f, 0f, zRotation);
            if (immediate)
            {
                _rect.anchoredPosition = anchoredPos;
                _rect.localEulerAngles = _slotEuler;
            }
            else if (!_dragging && !_hovered)
            {
                TweenToSlot();
            }
        }

        private void TweenToSlot()
        {
            _rect.DOKill();
            _rect.DOAnchorPos(_slotPos, 0.22f).SetEase(Ease.OutCubic).SetLink(gameObject);
            _rect.DOLocalRotate(_slotEuler, 0.22f).SetEase(Ease.OutCubic).SetLink(gameObject);
            _rect.DOScale(1f, 0.15f).SetLink(gameObject);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_dragging || !_controller.InputEnabled) return;
            _hovered = true;
            _rect.SetAsLastSibling();   // uGUI 疊序 = 階層順序,浮起的卡要壓過鄰卡
            _rect.DOKill();
            _rect.DOAnchorPos(_slotPos + new Vector2(0f, 60f), 0.12f).SetEase(Ease.OutCubic).SetLink(gameObject);
            _rect.DOLocalRotate(Vector3.zero, 0.12f).SetLink(gameObject);
            _rect.DOScale(1.18f, 0.12f).SetEase(Ease.OutCubic).SetLink(gameObject);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (_dragging) return;
            _hovered = false;
            TweenToSlot();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!_controller.InputEnabled) return;
            _dragging = true;
            _background.raycastTarget = false;   // 拖曳中讓射線穿過卡,才能點到敵人
            _rect.DOKill();
            _rect.DOScale(0.9f, 0.1f).SetLink(gameObject);
            _rect.DOLocalRotate(Vector3.zero, 0.1f).SetLink(gameObject);
            if (RequiresTarget)
            {
                _controller.BeginTargeting(this);
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_dragging) return;
            _rect.position = eventData.position;
            _controller.UpdateTargeting(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!_dragging) return;
            _dragging = false;
            _hovered = false;
            _background.raycastTarget = true;
            _controller.EndTargeting();
            _controller.RequestPlay(this, eventData);
        }

        /// <summary>出牌失敗/取消時回槽。</summary>
        public void SnapBack()
        {
            TweenToSlot();
        }
    }
}
