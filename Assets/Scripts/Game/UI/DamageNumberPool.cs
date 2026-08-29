using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;

namespace STS.Game.UI
{
    /// <summary>
    /// 傷害數字物件池。全域規則:生成類熱點一開始就走池,不留之後重構。
    /// tween 一律 SetLink(標配)——池物件停用時 tween 被殺,不殘留。
    /// </summary>
    public sealed class DamageNumberPool : MonoBehaviour
    {
        private readonly Stack<TextMeshProUGUI> _pool = new Stack<TextMeshProUGUI>();
        private Transform _layer;

        public static DamageNumberPool Build(Transform overlayLayer)
        {
            var root = UiKit.CreateRect("傷害數字池", overlayLayer);
            UiKit.Stretch(root);
            root.gameObject.AddComponent<CanvasGroup>().blocksRaycasts = false;
            var pool = root.gameObject.AddComponent<DamageNumberPool>();
            pool._layer = root;
            return pool;
        }

        public void Spawn(Vector3 worldPos, string text, Color color)
        {
            var tmp = _pool.Count > 0 ? _pool.Pop() : Create();
            tmp.gameObject.SetActive(true);
            tmp.text = text;
            tmp.color = color;
            tmp.transform.position = worldPos;
            tmp.transform.localScale = Vector3.one;

            var rect = tmp.rectTransform;
            var start = rect.anchoredPosition;
            DOTween.Sequence()
                .Append(rect.DOAnchorPosY(start.y + 70f, 0.6f).SetEase(Ease.OutCubic))
                .Join(tmp.DOFade(0f, 0.6f).SetEase(Ease.InQuad))
                .OnComplete(() => Recycle(tmp, start))
                .SetLink(tmp.gameObject);
        }

        private TextMeshProUGUI Create()
        {
            var tmp = UiKit.CreateText("傷害數字", _layer, "", 44f);
            tmp.fontStyle = FontStyles.Bold;
            UiKit.Place(tmp.rectTransform, Vector2.zero, new Vector2(220f, 60f));
            return tmp;
        }

        private void Recycle(TextMeshProUGUI tmp, Vector2 originalAnchored)
        {
            tmp.alpha = 1f;
            tmp.rectTransform.anchoredPosition = originalAnchored;
            tmp.gameObject.SetActive(false);
            _pool.Push(tmp);
        }
    }
}
