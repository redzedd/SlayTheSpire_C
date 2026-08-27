using DG.Tweening;
using TMPro;
using UnityEngine;
using STS.Core.Combat;

namespace STS.Game.UI
{
    /// <summary>玩家狀態面板:血條/格擋/能量球/狀態列。只讀快照。</summary>
    public sealed class PlayerHudView : MonoBehaviour
    {
        private TextMeshProUGUI _hpText;
        private TextMeshProUGUI _blockText;
        private TextMeshProUGUI _energyText;
        private TextMeshProUGUI _statusText;
        private RectTransform _hpFill;

        public static PlayerHudView Build(Transform parent)
        {
            // 面板底板同時是 hover 感應區(透明 Image 仍會吃射線),讓玩家能指自己看狀態
            var background = UiKit.CreatePanel("玩家面板", parent, new Color(0.1f, 0.1f, 0.14f, 0.55f));
            var root = background.rectTransform;
            UiKit.Place(root, new Vector2(230f, 260f), new Vector2(420f, 200f), new Vector2(0f, 0f));
            var view = root.gameObject.AddComponent<PlayerHudView>();

            var energyOrb = UiKit.CreatePanel("能量球", root, new Color(0.9f, 0.65f, 0.15f));
            UiKit.Place(energyOrb.rectTransform, new Vector2(-140f, 0f), new Vector2(110f, 110f));
            view._energyText = UiKit.CreateText("能量", energyOrb.transform, "3/3", 40f, Color.black);
            UiKit.Stretch(view._energyText.rectTransform);

            view._hpFill = UiKit.CreateBar("血條", root, new Vector2(60f, 30f), new Vector2(260f, 24f),
                new Vector2(0.5f, 0.5f), new Color(0.2f, 0.05f, 0.05f), new Color(0.2f, 0.8f, 0.3f));
            view._hpText = UiKit.CreateText("血量", root, "", 24f);
            UiKit.Place(view._hpText.rectTransform, new Vector2(60f, 0f), new Vector2(260f, 30f));

            view._blockText = UiKit.CreateText("格擋", root, "", 28f, new Color(0.55f, 0.8f, 1f));
            UiKit.Place(view._blockText.rectTransform, new Vector2(-60f, 30f), new Vector2(100f, 34f));

            view._statusText = UiKit.CreateText("狀態列", root, "", 22f, new Color(0.9f, 0.9f, 0.6f));
            UiKit.Place(view._statusText.rectTransform, new Vector2(60f, -34f), new Vector2(380f, 30f));
            return view;
        }

        /// <summary>玩家受擊:面板震動。</summary>
        public void PlayHitShake()
        {
            transform.DOKill(true);
            transform.DOShakePosition(0.3f, new Vector3(14f, 8f, 0f), 18).SetLink(gameObject);
        }

        public void RefreshFrom(CombatEngine engine)
        {
            var player = engine.State.Player;
            _hpText.text = $"{player.Hp}/{player.MaxHp}";
            UiKit.SetBarFill(_hpFill, player.MaxHp > 0 ? (float)player.Hp / player.MaxHp : 0f);
            _blockText.text = player.Block > 0 ? $"盾{player.Block}" : "";
            _energyText.text = $"{engine.State.Energy}/{engine.State.MaxEnergy}";
            _statusText.text = StatusRowText.Build(player);
        }
    }
}
