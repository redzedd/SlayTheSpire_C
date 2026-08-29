using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace STS.Game.UI
{
    /// <summary>
    /// 程式建構 uGUI 元件的共用工廠。佔位美術原則:Image 純色塊 + TMP 文字,不引用任何素材。
    /// MainFont 由 GameController 在 Awake 注入(繁中動態字型);為 static 是刻意——
    /// domain reload 後由場景重新注入,不存跨場景狀態。
    /// </summary>
    public static class UiKit
    {
        public static TMP_FontAsset MainFont;

        // 佔位配色(卡牌類型/敵人/面板)
        public static readonly Color 攻擊色 = new Color(0.75f, 0.25f, 0.22f);
        public static readonly Color 技能色 = new Color(0.24f, 0.55f, 0.32f);
        public static readonly Color 能力色 = new Color(0.25f, 0.4f, 0.7f);
        public static readonly Color 狀態色 = new Color(0.45f, 0.45f, 0.45f);
        public static readonly Color 敵人色 = new Color(0.5f, 0.16f, 0.16f);
        public static readonly Color 面板色 = new Color(0.12f, 0.12f, 0.15f, 0.92f);

        public static Color 卡牌顏色(Core.Cards.CardType type)
        {
            switch (type)
            {
                case Core.Cards.CardType.Attack: return 攻擊色;
                case Core.Cards.CardType.Skill: return 技能色;
                case Core.Cards.CardType.Power: return 能力色;
                default: return 狀態色;
            }
        }

        public static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        public static Image CreatePanel(string name, Transform parent, Color color)
        {
            var rect = CreateRect(name, parent);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        /// <summary>圓形色塊。佔位期沒有素材,圓形 sprite 現場畫一張(全域共用一張,不是每次都畫)。</summary>
        public static Image CreateCircle(string name, Transform parent, Color color)
        {
            var image = CreatePanel(name, parent, color);
            image.sprite = 圓形Sprite;
            image.type = Image.Type.Simple;
            return image;
        }

        private static Sprite _circleSprite;

        private static Sprite 圓形Sprite
        {
            get
            {
                if (_circleSprite != null) return _circleSprite;
                const int size = 128;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
                texture.hideFlags = HideFlags.HideAndDontSave;
                float radius = size * 0.5f;
                var pixels = new Color32[size * size];
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float dx = x + 0.5f - radius;
                        float dy = y + 0.5f - radius;
                        // 邊緣 1px 做線性淡出,不然圓周會有鋸齒
                        float alpha = Mathf.Clamp01(radius - Mathf.Sqrt(dx * dx + dy * dy));
                        pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                    }
                }
                texture.SetPixels32(pixels);
                texture.Apply();
                _circleSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f));
                _circleSprite.hideFlags = HideFlags.HideAndDontSave;
                return _circleSprite;
            }
        }

        public static TextMeshProUGUI CreateText(string name, Transform parent, string text, float fontSize,
            Color? color = null, TextAlignmentOptions alignment = TextAlignmentOptions.Center)
        {
            var rect = CreateRect(name, parent);
            var tmp = rect.gameObject.AddComponent<TextMeshProUGUI>();
            if (MainFont != null) tmp.font = MainFont;
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = color ?? Color.white;
            tmp.alignment = alignment;
            tmp.raycastTarget = false;
            return tmp;
        }

        public static Button CreateButton(string name, Transform parent, string label, float fontSize,
            Color background, System.Action onClick)
        {
            var image = CreatePanel(name, parent, background);
            var button = image.gameObject.AddComponent<Button>();
            if (onClick != null) button.onClick.AddListener(() => onClick());
            var text = CreateText("標籤", image.transform, label, fontSize);
            Stretch(text.rectTransform);
            return button;
        }

        /// <summary>錨定四邊撐滿父物件。</summary>
        /// <summary>米色布條標題(獎勵與寶箱共用):一塊長條 + 兩端下垂的小方塊,佔位期夠用。</summary>
        public static Image CreateBanner(Transform parent, string title, Vector2 pos)
        {
            var banner = CreatePanel("布條", parent, new Color(0.85f, 0.76f, 0.56f));
            Place(banner.rectTransform, pos, new Vector2(640f, 92f));
            var left = CreatePanel("左緣", banner.transform, new Color(0.72f, 0.63f, 0.45f));
            Place(left.rectTransform, new Vector2(-330f, -14f), new Vector2(40f, 64f));
            var right = CreatePanel("右緣", banner.transform, new Color(0.72f, 0.63f, 0.45f));
            Place(right.rectTransform, new Vector2(330f, -14f), new Vector2(40f, 64f));
            Place(CreateText("標題", banner.transform, title, 40f, new Color(0.16f, 0.12f, 0.08f)).rectTransform,
                Vector2.zero, new Vector2(600f, 60f));
            return banner;
        }

        public static void Stretch(RectTransform rect, float margin = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(margin, margin);
            rect.offsetMax = new Vector2(-margin, -margin);
        }

        /// <summary>以中心錨點設定位置與大小(anchoredPosition 座標系)。</summary>
        public static void Place(RectTransform rect, Vector2 anchoredPos, Vector2 size,
            Vector2? anchor = null, Vector2? pivot = null)
        {
            var a = anchor ?? new Vector2(0.5f, 0.5f);
            rect.anchorMin = a;
            rect.anchorMax = a;
            rect.pivot = pivot ?? new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = size;
        }

        /// <summary>簡易水平血條:回傳 fill 的 RectTransform,之後用 SetBarFill 調整。</summary>
        public static RectTransform CreateBar(string name, Transform parent, Vector2 anchoredPos, Vector2 size,
            Vector2 anchor, Color backColor, Color fillColor)
        {
            var back = CreatePanel(name, parent, backColor);
            Place(back.rectTransform, anchoredPos, size, anchor);
            var fill = CreatePanel("填充", back.transform, fillColor);
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = new Vector2(1f, 1f);
            fill.rectTransform.offsetMin = Vector2.zero;
            fill.rectTransform.offsetMax = Vector2.zero;
            fill.rectTransform.pivot = new Vector2(0f, 0.5f);
            return fill.rectTransform;
        }

        /// <summary>血條填充比例(0~1):用 anchorMax.x 縮放,不動 width(避免每幀重排版)。</summary>
        public static void SetBarFill(RectTransform fill, float ratio)
        {
            fill.anchorMax = new Vector2(Mathf.Clamp01(ratio), 1f);
        }

        /// <summary>純顯示用卡面(無互動):獎勵/商店/牌組檢視共用。</summary>
        public static RectTransform MakeCardFace(Transform parent, Core.Cards.CardDef def, string description, float scale = 1f)
        {
            var face = CreatePanel($"卡面_{def.Id}", parent, 卡牌顏色(def.Type));
            Place(face.rectTransform, Vector2.zero, new Vector2(160f * scale, 220f * scale));

            var costOrb = CreatePanel("費用底", face.transform, new Color(0.95f, 0.75f, 0.2f));
            Place(costOrb.rectTransform, new Vector2(-70f * scale, 88f * scale), new Vector2(36f * scale, 36f * scale));
            costOrb.raycastTarget = false;
            var costText = CreateText("費用", costOrb.transform, def.CostIsX ? "X" : def.Cost.ToString(), 24f * scale, Color.black);
            Stretch(costText.rectTransform);

            var nameText = CreateText("卡名", face.transform, def.Name, 22f * scale);
            Place(nameText.rectTransform, new Vector2(0f, 64f * scale), new Vector2(150f * scale, 30f * scale));

            var descText = CreateText("描述", face.transform, description, 17f * scale);
            Place(descText.rectTransform, new Vector2(0f, -28f * scale), new Vector2(140f * scale, 120f * scale));
            descText.textWrappingMode = TMPro.TextWrappingModes.Normal;
            return face.rectTransform;
        }
    }
}
