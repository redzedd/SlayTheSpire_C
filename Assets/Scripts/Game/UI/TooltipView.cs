using TMPro;
using UnityEngine;

namespace STS.Game.UI
{
    /// <summary>
    /// 共用提示框:滑鼠指到敵人/自己/遺物/藥水時顯示詳細說明。
    /// 單一實例掛在 Overlay 層最上方;內容由呼叫端組好字串傳入(支援 TMP 粗體標記)。
    /// 尺寸用 TMP 的偏好尺寸算,不依賴 LayoutGroup(佈局群組與 TMP 混用容易在同幀量到舊值)。
    /// </summary>
    public sealed class TooltipView : MonoBehaviour
    {
        private const float 內文寬 = 420f;
        private const float 內距 = 18f;

        private RectTransform _canvasRect;
        private RectTransform _panel;
        private TextMeshProUGUI _text;

        public static TooltipView Build(Transform overlayLayer)
        {
            var root = UiKit.CreateRect("提示框", overlayLayer);
            UiKit.Stretch(root);
            var view = root.gameObject.AddComponent<TooltipView>();
            view._canvasRect = (RectTransform)overlayLayer;

            var panel = UiKit.CreatePanel("底板", root, new Color(0.08f, 0.08f, 0.11f, 0.97f));
            panel.raycastTarget = false;   // 提示框本身不吃射線,否則會擋掉底下的 hover
            view._panel = panel.rectTransform;
            view._panel.pivot = new Vector2(0f, 1f);   // 以左上角對齊游標

            view._text = UiKit.CreateText("內容", panel.transform, "", 22f, Color.white,
                TextAlignmentOptions.TopLeft);
            view._text.rectTransform.anchorMin = new Vector2(0f, 1f);
            view._text.rectTransform.anchorMax = new Vector2(0f, 1f);
            view._text.rectTransform.pivot = new Vector2(0f, 1f);
            view._text.rectTransform.anchoredPosition = new Vector2(內距, -內距);
            view._text.textWrappingMode = TextWrappingModes.Normal;

            root.gameObject.SetActive(false);
            return view;
        }

        public void Show(string content, Vector2 screenPos)
        {
            if (string.IsNullOrEmpty(content))
            {
                Hide();
                return;
            }
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            _text.text = content;

            var preferred = _text.GetPreferredValues(content, 內文寬, 0f);
            float height = Mathf.Min(preferred.y, 640f);
            _text.rectTransform.sizeDelta = new Vector2(內文寬, height);
            _panel.sizeDelta = new Vector2(內文寬 + 內距 * 2f, height + 內距 * 2f);
            Reposition(screenPos);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        /// <summary>貼著游標右下顯示;超出畫面就翻到另一側,不讓提示框跑到看不見的地方。</summary>
        private void Reposition(Vector2 screenPos)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, screenPos, null, out var local);
            var size = _panel.sizeDelta;
            var half = _canvasRect.rect.size * 0.5f;
            float x = local.x + 24f;
            float y = local.y - 16f;
            if (x + size.x > half.x) x = local.x - 24f - size.x;
            if (y - size.y < -half.y) y = local.y + 16f + size.y;
            _panel.anchoredPosition = new Vector2(x, y);
        }
    }
}
