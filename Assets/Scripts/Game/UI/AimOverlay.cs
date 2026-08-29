using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace STS.Game.UI
{
    /// <summary>
    /// 瞄準用的全螢幕透明遮罩(藥水指定目標時開著)。
    /// 它是最上層的命中對象,但攔不住底下的敵人——判定走 RaycastAll,遮罩只是結果裡的一筆。
    /// 游標位置每幀直接讀 Input System:瞄準沒有按住不放,拿不到 IDragHandler 的事件流,
    /// 而 IPointerMoveHandler 要不要送由輸入模組決定,不能拿箭頭跟不跟得上去賭。
    /// 物件只在瞄準期間啟用,所以平時完全不跑 Update。
    /// </summary>
    public sealed class AimOverlay : MonoBehaviour, IPointerClickHandler
    {
        private Action<Vector2> _onMove;
        private Action<PointerEventData> _onClick;
        private Vector2 _lastCursor;

        public static AimOverlay Build(Transform overlayLayer, Action<Vector2> onMove,
            Action<PointerEventData> onClick)
        {
            // 全透明但仍然接收 raycast:uGUI 的命中判定不看 alpha
            var image = UiKit.CreatePanel("瞄準遮罩", overlayLayer, new Color(0f, 0f, 0f, 0f));
            UiKit.Stretch(image.rectTransform);
            image.raycastTarget = true;

            var view = image.gameObject.AddComponent<AimOverlay>();
            view._onMove = onMove;
            view._onClick = onClick;
            image.gameObject.SetActive(false);
            return view;
        }

        public void Show()
        {
            _lastCursor = Vector2.zero;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        /// <summary>目前游標位置;沒有滑鼠(手把/觸控)時回 false,呼叫端就別動箭頭終點。</summary>
        public static bool TryGetCursor(out Vector2 position)
        {
            var mouse = Mouse.current;
            if (mouse == null)
            {
                position = Vector2.zero;
                return false;
            }
            position = mouse.position.ReadValue();
            return true;
        }

        private void Update()
        {
            if (!TryGetCursor(out var cursor)) return;
            if (cursor == _lastCursor) return;   // 沒動就不重算曲線
            _lastCursor = cursor;
            _onMove(cursor);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            _onClick(eventData);
        }
    }
}
