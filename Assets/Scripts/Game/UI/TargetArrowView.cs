using UnityEngine;
using UnityEngine.UI;

namespace STS.Game.UI
{
    /// <summary>需目標卡拖曳時的指示線:一條由起點拉伸到游標的 Image(佔位版,M7 換點串曲線)。</summary>
    public sealed class TargetArrowView : MonoBehaviour
    {
        private RectTransform _line;
        private Vector2 _originScreen;

        public static TargetArrowView Build(Transform overlayLayer)
        {
            var root = UiKit.CreateRect("目標指示線", overlayLayer);
            UiKit.Stretch(root);
            root.gameObject.AddComponent<CanvasGroup>().blocksRaycasts = false;
            var view = root.gameObject.AddComponent<TargetArrowView>();

            var line = UiKit.CreatePanel("線", root, new Color(1f, 0.4f, 0.3f, 0.85f));
            line.raycastTarget = false;
            view._line = line.rectTransform;
            view._line.pivot = new Vector2(0f, 0.5f);
            view.gameObject.SetActive(false);
            return view;
        }

        public void Show(Vector2 originScreenPos)
        {
            _originScreen = originScreenPos;
            gameObject.SetActive(true);
            UpdateTo(originScreenPos);
        }

        public void UpdateTo(Vector2 pointerScreenPos)
        {
            _line.position = _originScreen;
            var delta = pointerScreenPos - _originScreen;
            _line.sizeDelta = new Vector2(delta.magnitude, 10f);
            _line.localEulerAngles = new Vector3(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }
    }
}
