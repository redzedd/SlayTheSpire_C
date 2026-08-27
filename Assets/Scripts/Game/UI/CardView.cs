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
    /// 兩種互動模式——平時 hover 抬升、拖曳出牌;選卡模式(消耗手牌)改成點擊選取。
    /// 所有位移一律先 DOKill 再開新 tween,不疊加;tween 全掛 SetLink 隨物件銷毀。
    /// </summary>
    public sealed class CardView : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler,
        IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public const float 寬 = 160f;
        public const float 高 = 220f;

        public int HandIndex;
        public int InstanceId { get; private set; }
        public CardDef Def { get; private set; }
        public bool RequiresTarget { get; private set; }

        private CombatScreenController _controller;
        private RectTransform _rect;
        private Image _background;
        private CanvasGroup _group;
        private TextMeshProUGUI _costText;
        private TextMeshProUGUI _nameText;
        private TextMeshProUGUI _descText;
        private Image _selectionFrame;
        /// <summary>需目標卡拖曳時,卡片定位到手牌前方的位置(不跟著游標跑)。</summary>
        private static readonly Vector2 瞄準位置 = new Vector2(0f, 250f);

        private Vector2 _slotPos;
        private Vector3 _slotEuler;
        private bool _dragging;
        private bool _targeting;
        private bool _hovered;
        private bool _leaving;

        public RectTransform Rect => _rect;

        public static CardView Build(Transform parent, CombatScreenController controller)
        {
            var background = UiKit.CreatePanel("卡牌", parent, Color.gray);
            UiKit.Place(background.rectTransform, Vector2.zero, new Vector2(寬, 高));
            var view = background.gameObject.AddComponent<CardView>();
            view._controller = controller;
            view._rect = background.rectTransform;
            view._background = background;
            view._group = background.gameObject.AddComponent<CanvasGroup>();

            // 選取外框(選卡模式用):平時隱藏
            var frame = UiKit.CreatePanel("選取框", background.transform, new Color(1f, 0.85f, 0.3f, 0.35f));
            UiKit.Stretch(frame.rectTransform, -8f);
            frame.raycastTarget = false;
            frame.gameObject.SetActive(false);
            view._selectionFrame = frame;

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

        public void Bind(int handIndex, int instanceId, CardDef def, string formattedDescription)
        {
            HandIndex = handIndex;
            InstanceId = instanceId;
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

        /// <summary>剛抽到:縮小半透明地從抽牌堆方向飛入。</summary>
        public void PlayDrawIn()
        {
            _rect.localScale = Vector3.one * 0.55f;
            _group.alpha = 0f;
            _group.DOFade(1f, 0.22f).SetEase(Ease.OutCubic).SetLink(gameObject);
        }

        /// <summary>離開手牌(打出/棄掉/消耗):放大淡出後自毀。</summary>
        public void AnimateOutAndDestroy()
        {
            if (_leaving) return;
            _leaving = true;
            _group.blocksRaycasts = false;
            _rect.DOKill();
            DOTween.Sequence()
                .Append(_rect.DOScale(1.25f, 0.18f).SetEase(Ease.OutCubic))
                .Join(_rect.DOAnchorPosY(_rect.anchoredPosition.y + 90f, 0.18f))
                .Join(_group.DOFade(0f, 0.18f))
                .OnComplete(() => Destroy(gameObject))
                .SetLink(gameObject);
        }

        public void SetChoiceSelected(bool selected)
        {
            if (_selectionFrame == null) return;
            _selectionFrame.gameObject.SetActive(selected);
            _rect.DOKill();
            _rect.DOAnchorPos(selected ? _slotPos + new Vector2(0f, 50f) : _slotPos, 0.14f)
                .SetEase(Ease.OutCubic).SetLink(gameObject);
            _rect.DOScale(selected ? 1.1f : 1f, 0.14f).SetLink(gameObject);
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
            else if (!_dragging && !_hovered && !_leaving)
            {
                TweenToSlot();
            }
        }

        private void TweenToSlot()
        {
            _rect.DOKill();
            _rect.DOAnchorPos(_slotPos, 0.22f).SetEase(Ease.OutCubic).SetLink(gameObject);
            _rect.DOLocalRotate(_slotEuler, 0.22f).SetEase(Ease.OutCubic).SetLink(gameObject);
            _rect.DOScale(1f, 0.18f).SetEase(Ease.OutBack).SetLink(gameObject);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_dragging || _leaving || !_controller.InputEnabled) return;
            if (_controller.IsChoiceMode && _controller.IsChosen(HandIndex)) return;   // 已選取的卡維持選取姿態
            _hovered = true;
            _rect.SetAsLastSibling();   // uGUI 疊序 = 階層順序,浮起的卡要壓過鄰卡
            _rect.DOKill();
            _rect.DOAnchorPos(_slotPos + new Vector2(0f, 60f), 0.12f).SetEase(Ease.OutCubic).SetLink(gameObject);
            _rect.DOLocalRotate(Vector3.zero, 0.12f).SetLink(gameObject);
            _rect.DOScale(1.18f, 0.12f).SetEase(Ease.OutCubic).SetLink(gameObject);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (_dragging || _leaving) return;
            _hovered = false;
            if (_controller.IsChoiceMode && _controller.IsChosen(HandIndex))
            {
                SetChoiceSelected(true);
                return;
            }
            TweenToSlot();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_leaving || !_controller.IsChoiceMode) return;
            _hovered = false;
            _controller.ToggleChoiceSelection(HandIndex);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (_leaving || !_controller.InputEnabled || _controller.IsChoiceMode) return;   // 選卡模式只接受點擊
            _dragging = true;
            _background.raycastTarget = false;   // 拖曳中讓射線穿過卡,才能點到敵人
            _rect.DOKill();
            _rect.DOLocalRotate(Vector3.zero, 0.12f).SetLink(gameObject);
            if (RequiresTarget)
            {
                // 需指定目標的卡:定位到手牌前方,由箭頭指向敵人(卡片本身不跟著游標)
                _targeting = true;
                _rect.SetAsLastSibling();
                _rect.DOAnchorPos(瞄準位置, 0.14f).SetEase(Ease.OutCubic).SetLink(gameObject);
                _rect.DOScale(1.15f, 0.14f).SetEase(Ease.OutCubic).SetLink(gameObject);
                _controller.BeginTargeting(this);
            }
            else
            {
                // 無目標卡:照舊跟著游標拖,拖過出牌線即施放
                _rect.DOScale(0.9f, 0.1f).SetLink(gameObject);
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_dragging) return;
            if (_targeting)
            {
                _controller.UpdateTargeting(eventData);   // 只有箭頭跟著游標
                return;
            }
            _rect.position = eventData.position;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!_dragging) return;
            _dragging = false;
            _targeting = false;
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

        private static bool ComputeRequiresTarget(CardDef def)
        {
            foreach (var step in def.Steps)
            {
                if (step.Target == EffectTarget.TargetEnemy) return true;
            }
            return false;
        }
    }
}
