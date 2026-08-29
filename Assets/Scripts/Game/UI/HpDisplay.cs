using DG.Tweening;
using TMPro;
using UnityEngine;

namespace STS.Game.UI
{
    /// <summary>
    /// 血量顯示(玩家與敵人共用):長條與數字綁在同一條補間上,一起從舊值滑到新值。
    /// 兩者分開跑——數字瞬變、長條慢慢掉——讀起來會像血條慢半拍,那正是原本的手感問題。
    /// 第一次設定直接就位,不從 0 補間上來。
    /// </summary>
    public sealed class HpDisplay
    {
        private const float 補間秒數 = 0.35f;

        private readonly RectTransform _fill;
        private readonly TextMeshProUGUI _label;
        private readonly GameObject _owner;
        private float _shownHp = -1f;
        private int _maxHp;
        private Tween _tween;

        public HpDisplay(RectTransform fill, TextMeshProUGUI label, GameObject owner)
        {
            _fill = fill;
            _label = label;
            _owner = owner;
        }

        public void Set(int hp, int maxHp)
        {
            _maxHp = maxHp;
            if (_shownHp < 0f || Mathf.Approximately(_shownHp, hp))
            {
                _tween?.Kill();
                _tween = null;
                _shownHp = hp;
                Apply();
                return;
            }
            _tween?.Kill();
            _tween = DOTween.To(() => _shownHp, value => { _shownHp = value; Apply(); }, hp, 補間秒數)
                .SetEase(Ease.OutCubic)
                .SetLink(_owner);
        }

        private void Apply()
        {
            // 四捨五入:數字要跟著長條一格一格走。無條件進位會讓它一步跳到終值附近再卡住,
            // 看起來就還是「數字先到、長條在追」。只有 0 特別處理:還沒真的歸零就不顯示 0。
            int shown = Mathf.RoundToInt(_shownHp);
            if (shown <= 0 && _shownHp > 0f) shown = 1;
            _label.text = $"{shown}/{_maxHp}";
            UiKit.SetBarFill(_fill, _maxHp > 0 ? _shownHp / _maxHp : 0f);
        }
    }
}
