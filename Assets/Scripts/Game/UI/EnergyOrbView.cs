using DG.Tweening;
using TMPro;
using UnityEngine;
using STS.Core.Combat;

namespace STS.Game.UI
{
    /// <summary>能量球(左下角,參考原作位置):數字變動時彈一下。</summary>
    public sealed class EnergyOrbView : MonoBehaviour
    {
        private TextMeshProUGUI _text;
        private RectTransform _rect;
        private int _lastEnergy = -1;

        public static EnergyOrbView Build(Transform parent)
        {
            var orb = UiKit.CreatePanel("能量球", parent, new Color(0.88f, 0.62f, 0.15f));
            UiKit.Place(orb.rectTransform, new Vector2(160f, 190f), new Vector2(140f, 140f), new Vector2(0f, 0f));
            var view = orb.gameObject.AddComponent<EnergyOrbView>();
            view._rect = orb.rectTransform;
            view._text = UiKit.CreateText("數值", orb.transform, "", 46f, Color.black);
            UiKit.Stretch(view._text.rectTransform);
            return view;
        }

        public void RefreshFrom(CombatEngine engine)
        {
            _text.text = $"{engine.State.Energy}/{engine.State.MaxEnergy}";
            if (_lastEnergy >= 0 && engine.State.Energy != _lastEnergy)
            {
                _rect.DOKill(true);
                _rect.DOPunchScale(Vector3.one * 0.2f, 0.25f, 8).SetLink(gameObject);
            }
            _lastEnergy = engine.State.Energy;
        }
    }
}
