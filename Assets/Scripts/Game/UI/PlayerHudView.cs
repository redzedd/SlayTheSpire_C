using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using STS.Core.Combat;

namespace STS.Game.UI
{
    /// <summary>
    /// 玩家角色(戰場左側,與敵人左右對峙):色塊軀體+血條+格擋+狀態列。
    /// 版面比照原作:血條在角色正下方,能量球獨立在左下角(EnergyOrbView)。
    /// </summary>
    public sealed class PlayerHudView : MonoBehaviour
    {
        private TextMeshProUGUI _hpText;
        private TextMeshProUGUI _blockText;
        private TextMeshProUGUI _statusText;
        private RectTransform _hpFill;
        private HpDisplay _hp;
        private int _maxHp;
        private Image _body;

        public static PlayerHudView Build(Transform parent)
        {
            var body = UiKit.CreatePanel("玩家", parent, new Color(0.3f, 0.45f, 0.65f));
            UiKit.Place(body.rectTransform, new Vector2(-520f, 60f), new Vector2(220f, 300f));
            var view = body.gameObject.AddComponent<PlayerHudView>();
            view._body = body;

            var nameText = UiKit.CreateText("名稱", body.transform, "無名者", 26f);
            UiKit.Place(nameText.rectTransform, new Vector2(0f, 120f), new Vector2(200f, 34f));

            view._hpFill = UiKit.CreateBar("血條", body.transform, new Vector2(0f, -172f), new Vector2(210f, 20f),
                new Vector2(0.5f, 0.5f), new Color(0.2f, 0.05f, 0.05f), new Color(0.2f, 0.8f, 0.3f));
            view._hpText = UiKit.CreateText("血量", body.transform, "", 24f);
            UiKit.Place(view._hpText.rectTransform, new Vector2(0f, -200f), new Vector2(220f, 30f));

            view._hp = new HpDisplay(view._hpFill, view._hpText, body.gameObject);

            view._blockText = UiKit.CreateText("格擋", body.transform, "", 28f, new Color(0.55f, 0.8f, 1f));
            UiKit.Place(view._blockText.rectTransform, new Vector2(-120f, -172f), new Vector2(80f, 34f));

            view._statusText = UiKit.CreateText("狀態列", body.transform, "", 22f, new Color(0.9f, 0.9f, 0.6f));
            UiKit.Place(view._statusText.rectTransform, new Vector2(0f, -236f), new Vector2(280f, 30f));
            return view;
        }

        /// <summary>玩家受擊:角色震動。</summary>
        public void PlayHitShake()
        {
            transform.DOKill(true);
            transform.DOShakePosition(0.3f, new Vector3(16f, 9f, 0f), 18).SetLink(gameObject);
            if (_body != null)
            {
                _body.DOKill();
                _body.color = Color.white;
                _body.DOColor(new Color(0.3f, 0.45f, 0.65f), 0.25f).SetEase(Ease.OutQuad).SetLink(gameObject);
            }
        }

        /// <summary>
        /// 用事件快照更新血量。理由同 EnemyView:引擎瞬時結算,回查引擎會讓血條
        /// 一口氣衝到最終值,傷害數字卻還在一段一段跳。
        /// </summary>
        public void ApplyHpSnapshot(int hp)
        {
            _hp.Set(hp, _maxHp);
        }

        /// <summary>把血量對齊引擎現況(建立時與播放結束時用)。</summary>
        public void SyncHp(CombatEngine engine)
        {
            var player = engine.State.Player;
            _maxHp = player.MaxHp;
            _hp.Set(player.Hp, player.MaxHp);
        }

        public void RefreshFrom(CombatEngine engine)
        {
            var player = engine.State.Player;
            _maxHp = player.MaxHp;   // 上限會被加最大生命改動,事件快照沒帶它
            _blockText.text = player.Block > 0 ? $"盾{player.Block}" : "";
            _statusText.text = StatusRowText.Build(player);
        }
    }
}
