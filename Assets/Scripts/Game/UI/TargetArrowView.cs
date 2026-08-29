using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace STS.Game.UI
{
    /// <summary>
    /// 指向目標的曲線箭頭(參考原作):由一串小方塊沿二次貝茲曲線排出弧線,末端一個較大的箭頭。
    /// 卡片定位在手牌前方不動,箭頭才是跟著游標跑的東西。
    /// 節點數固定、只更新既有物件的位置,不在拖曳中配置新物件。
    /// </summary>
    public sealed class TargetArrowView : MonoBehaviour
    {
        private const int 節數 = 16;
        private static readonly Color 箭頭色 = new Color(0.85f, 0.22f, 0.2f, 0.95f);

        private readonly List<RectTransform> _segments = new List<RectTransform>(節數);
        private RectTransform _head;
        private RectTransform _canvasRect;

        public static TargetArrowView Build(Transform overlayLayer)
        {
            var root = UiKit.CreateRect("目標箭頭", overlayLayer);
            UiKit.Stretch(root);
            root.gameObject.AddComponent<CanvasGroup>().blocksRaycasts = false;
            var view = root.gameObject.AddComponent<TargetArrowView>();
            view._canvasRect = (RectTransform)overlayLayer;

            for (int i = 0; i < 節數; i++)
            {
                var segment = UiKit.CreatePanel($"節{i}", root, 箭頭色);
                segment.raycastTarget = false;
                // 越靠近箭頭越粗,做出漸縮的尾巴
                float size = Mathf.Lerp(14f, 30f, i / (float)(節數 - 1));
                UiKit.Place(segment.rectTransform, Vector2.zero, new Vector2(size, size));
                view._segments.Add(segment.rectTransform);
            }

            var head = UiKit.CreatePanel("箭頭", root, 箭頭色);
            head.raycastTarget = false;
            UiKit.Place(head.rectTransform, Vector2.zero, new Vector2(52f, 52f));
            view._head = head.rectTransform;

            root.gameObject.SetActive(false);
            return view;
        }

        public void Show()
        {
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
        }

        /// <summary>每次移動都重算整條弧線;起點是卡片位置,終點是游標。</summary>
        public void UpdateCurve(Vector2 originScreen, Vector2 targetScreen)
        {
            var origin = ToLocal(originScreen);
            var target = ToLocal(targetScreen);
            // 控制點抬高,做出原作那種先上揚再俯衝的弧線
            var control = (origin + target) * 0.5f + new Vector2(0f, Mathf.Max(120f, Vector2.Distance(origin, target) * 0.35f));

            for (int i = 0; i < _segments.Count; i++)
            {
                float t = (i + 1) / (float)(_segments.Count + 1);
                _segments[i].anchoredPosition = Bezier(origin, control, target, t);
            }
            _head.anchoredPosition = target;
            var tangent = Bezier(origin, control, target, 0.99f) - Bezier(origin, control, target, 0.9f);
            _head.localEulerAngles = new Vector3(0f, 0f, Mathf.Atan2(tangent.y, tangent.x) * Mathf.Rad2Deg + 45f);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        private Vector2 ToLocal(Vector2 screenPos)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, screenPos, null, out var local);
            return local;
        }

        private static Vector2 Bezier(Vector2 a, Vector2 b, Vector2 c, float t)
        {
            float u = 1f - t;
            return u * u * a + 2f * u * t * b + t * t * c;
        }
    }
}
