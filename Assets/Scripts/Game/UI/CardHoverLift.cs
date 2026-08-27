using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;

namespace STS.Game.UI
{
    /// <summary>清單/網格中的卡面 hover 回饋:微微放大浮起,表示「這張可以點」。</summary>
    public sealed class CardHoverLift : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private RectTransform _rect;

        public void Setup(RectTransform rect)
        {
            _rect = rect;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_rect == null) return;
            _rect.DOKill();
            _rect.DOScale(1.08f, 0.12f).SetEase(Ease.OutCubic).SetLink(gameObject);
            _rect.SetAsLastSibling();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (_rect == null) return;
            _rect.DOKill();
            _rect.DOScale(1f, 0.12f).SetEase(Ease.OutCubic).SetLink(gameObject);
        }
    }
}
