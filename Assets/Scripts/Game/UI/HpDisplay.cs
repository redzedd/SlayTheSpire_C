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
        // 要短於傷害事件的節拍(0.22s),多段攻擊的每一段才會在自己的節拍內走完,
        // 而不是第一段還在掉、第二段的數字就跳出來了
        private const float 補間秒數 = 0.18f;

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
            // 四捨五入:數字跟著長條一格一格走。不做「還沒歸零就顯示 1」那種特例——
            // 那會讓致死的那一下卡在 1 停留一段時間才變 0。
            int shown = Mathf.RoundToInt(_shownHp);
            _label.text = $"{shown}/{_maxHp}";
            UiKit.SetBarFill(_fill, _maxHp > 0 ? _shownHp / _maxHp : 0f);
        }
    }
}
