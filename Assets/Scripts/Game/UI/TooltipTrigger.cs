using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace STS.Game.UI
{
    /// <summary>
    /// 掛在任何可指的 UI 上,滑入時向提示框要內容。
    /// 內容用委派而非字串:狀態層數/血量會變,每次指上去都要現算。
    /// </summary>
    public sealed class TooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private TooltipView _tooltip;
        private Func<string> _contentProvider;

        public static TooltipTrigger Attach(GameObject target, TooltipView tooltip, Func<string> contentProvider)
        {
            var trigger = target.AddComponent<TooltipTrigger>();
            trigger._tooltip = tooltip;
            trigger._contentProvider = contentProvider;
            return trigger;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_tooltip == null || _contentProvider == null) return;
            _tooltip.Show(_contentProvider(), eventData.position);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (_tooltip != null) _tooltip.Hide();
        }

        private void OnDisable()
        {
            if (_tooltip != null) _tooltip.Hide();
        }
    }
}
