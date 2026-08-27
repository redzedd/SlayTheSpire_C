using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using STS.Core.Combat;

namespace STS.Game.UI
{
    /// <summary>
    /// 戰鬥畫面總控制器——整個 UI 裡唯一碰引擎的類。
    /// 職責:把玩家操作轉成引擎指令,把引擎吐出的事件佇列照序播放;播放期間鎖輸入。
    /// 視圖一律讀快照重繪,不回查引擎(意圖預覽與卡定義查詢是僅有的例外)。
    /// </summary>
    public sealed class CombatScreenController : MonoBehaviour
    {
        public bool InputEnabled { get; private set; }
        /// <summary>戰鬥結束(播放完畢後)通知 Run 層;參數 = 是否勝利。由 GameController 指定。</summary>
        public System.Action<bool> OnCombatEnded;

        private GameController _game;
        private CombatEngine _engine;
        private RectTransform _overlayRoot;
        private HandView _hand;
        private PlayerHudView _hud;
        private readonly List<EnemyView> _enemyViews = new List<EnemyView>();
        private DamageNumberPool _damagePool;
        private TargetArrowView _arrow;
        private ChoicePanelView _choicePanel;
        private PileListOverlay _pileOverlay;
        private Button _endTurnButton;
        private TextMeshProUGUI _drawPileLabel;
        private TextMeshProUGUI _discardPileLabel;
        private TextMeshProUGUI _exhaustPileLabel;
        private RectTransform _potionBar;
        private TextMeshProUGUI _hintText;
        private CanvasGroup _hintGroup;

        private static readonly List<RaycastResult> RaycastBuffer = new List<RaycastResult>();

        public static CombatScreenController Build(Transform screenLayer, Transform overlayLayer,
            GameController game, CombatEngine engine)
        {
            var root = UiKit.CreateRect("戰鬥畫面", screenLayer);
            UiKit.Stretch(root);
            var controller = root.gameObject.AddComponent<CombatScreenController>();
            controller._game = game;
            controller._engine = engine;

            // 敵人橫排(依數量置中)
            int enemyCount = engine.State.Enemies.Count;
            for (int i = 0; i < enemyCount; i++)
            {
                float x = (i - (enemyCount - 1) / 2f) * 280f;
                controller._enemyViews.Add(EnemyView.Build(root, i, new Vector2(x + 180f, 120f)));
            }

            controller._hud = PlayerHudView.Build(root);
            controller._hand = HandView.Build(root, controller);

            controller._endTurnButton = UiKit.CreateButton("結束回合", root, "結束回合", 28f,
                new Color(0.7f, 0.5f, 0.15f), controller.OnEndTurnClicked);
            UiKit.Place((RectTransform)controller._endTurnButton.transform,
                new Vector2(-160f, 220f), new Vector2(220f, 64f), new Vector2(1f, 0f));

            // 牌堆按鈕(左下抽牌、右下棄牌/消耗)
            controller._drawPileLabel = controller.BuildPileButton(root, "抽牌堆", new Vector2(140f, 70f),
                new Vector2(0f, 0f), () => controller.ShowPile("抽牌堆", engine.State.DrawPile, true));
            controller._discardPileLabel = controller.BuildPileButton(root, "棄牌堆", new Vector2(-140f, 70f),
                new Vector2(1f, 0f), () => controller.ShowPile("棄牌堆", engine.State.DiscardPile, false));
            controller._exhaustPileLabel = controller.BuildPileButton(root, "消耗堆", new Vector2(-140f, 140f),
                new Vector2(1f, 0f), () => controller.ShowPile("消耗堆", engine.State.ExhaustPile, false));

            // 藥水列(上方置左)
            controller._potionBar = UiKit.CreateRect("藥水列", root);
            UiKit.Place(controller._potionBar, new Vector2(240f, -50f), new Vector2(460f, 60f), new Vector2(0f, 1f));

            // 提示文字(出牌失敗原因)
            controller._hintText = UiKit.CreateText("提示", root, "", 30f, new Color(1f, 0.55f, 0.45f));
            UiKit.Place(controller._hintText.rectTransform, new Vector2(0f, -140f), new Vector2(800f, 44f), new Vector2(0.5f, 1f));
            controller._hintGroup = controller._hintText.gameObject.AddComponent<CanvasGroup>();
            controller._hintGroup.alpha = 0f;
            controller._hintGroup.blocksRaycasts = false;

            // Overlay 層(疊序高於主畫面):集中在自己的容器,畫面銷毀時一起清,不留孤兒
            controller._overlayRoot = UiKit.CreateRect("戰鬥Overlay", overlayLayer);
            UiKit.Stretch(controller._overlayRoot);
            controller._overlayRoot.gameObject.AddComponent<CanvasGroup>().blocksRaycasts = true;
            controller._damagePool = DamageNumberPool.Build(controller._overlayRoot);
            controller._arrow = TargetArrowView.Build(controller._overlayRoot);
            controller._choicePanel = ChoicePanelView.Build(controller._overlayRoot, controller);
            controller._pileOverlay = PileListOverlay.Build(controller._overlayRoot);

            controller.RefreshAll();
            controller.StartPlayback();   // 消化開戰事件(洗牌/抽牌/意圖)
            return controller;
        }

        private TextMeshProUGUI BuildPileButton(Transform parent, string label, Vector2 pos, Vector2 anchor,
            System.Action onClick)
        {
            var button = UiKit.CreateButton(label, parent, label, 22f, new Color(0.25f, 0.25f, 0.32f), onClick);
            UiKit.Place((RectTransform)button.transform, pos, new Vector2(180f, 56f), anchor);
            return button.GetComponentInChildren<TextMeshProUGUI>();
        }

        private void ShowPile(string pileName, List<Core.Cards.CardInstance> pile, bool hideOrder)
        {
            if (!InputEnabled) return;
            _pileOverlay.Show(_engine, pileName, pile, hideOrder);
        }

        // ---- 玩家操作入口 ----

        public void BeginTargeting(CardView card)
        {
            _arrow.Show(card.Rect.position);
        }

        public void UpdateTargeting(Vector2 pointerScreenPos)
        {
            _arrow.UpdateTo(pointerScreenPos);
        }

        public void EndTargeting()
        {
            _arrow.Hide();
        }

        public void RequestPlay(CardView card, PointerEventData eventData)
        {
            if (!InputEnabled)
            {
                card.SnapBack();
                return;
            }
            int target = -1;
            if (card.RequiresTarget)
            {
                target = RaycastEnemyIndex(eventData);
                if (target < 0)
                {
                    card.SnapBack();   // 沒放到敵人身上 = 取消,不當錯誤
                    return;
                }
            }
            else if (eventData.position.y < Screen.height * 0.38f)
            {
                card.SnapBack();       // 沒拖過出牌線 = 取消
                return;
            }

            if (!_engine.CanPlayCard(card.HandIndex, target, out string reason))
            {
                Flash(reason);
                card.SnapBack();
                return;
            }
            _engine.PlayCard(card.HandIndex, target);
            StartPlayback();
        }

        private static int RaycastEnemyIndex(PointerEventData eventData)
        {
            if (EventSystem.current == null) return -1;
            RaycastBuffer.Clear();
            EventSystem.current.RaycastAll(eventData, RaycastBuffer);
            foreach (var hit in RaycastBuffer)
            {
                var enemyView = hit.gameObject.GetComponentInParent<EnemyView>();
                if (enemyView != null) return enemyView.EnemyIndex;
            }
            return -1;
        }

        public void OnEndTurnClicked()
        {
            if (!InputEnabled) return;
            _engine.EndPlayerTurn();
            StartPlayback();
        }

        public void SubmitChoice(int[] handIndices)
        {
            _engine.ResolveChoice(handIndices);
            StartPlayback();
        }

        private void OnPotionClicked(int slot)
        {
            if (!InputEnabled) return;
            var potionId = _engine.State.PotionSlots[slot];
            if (potionId == null) return;
            int target = -1;
            var def = _game.Db.GetPotion(potionId);
            if (def.NeedsTarget)
            {
                // 佔位 UX:需目標藥水直接丟第一個活敵;M6 之後換成點選目標
                for (int i = 0; i < _engine.State.Enemies.Count; i++)
                {
                    if (_engine.State.Enemies[i].IsAlive) { target = i; break; }
                }
                if (target < 0) return;
            }
            _engine.UsePotion(slot, target);
            StartPlayback();
        }

        /// <summary>verify 煙霧測試入口:走與拖曳出牌相同的指令路徑,出第一張可出的牌。</summary>
        public string 煙霧_出第一張可出的牌()
        {
            if (!InputEnabled) return "輸入鎖定中(播放尚未結束)";
            for (int i = 0; i < _engine.State.Hand.Count; i++)
            {
                if (_engine.CanPlayCard(i, -1, out _))
                {
                    string cardName = _engine.GetCardDef(_engine.State.Hand[i]).Name;
                    _engine.PlayCard(i, -1);
                    StartPlayback();
                    return $"已出牌(無目標):{cardName}";
                }
                for (int t = 0; t < _engine.State.Enemies.Count; t++)
                {
                    if (_engine.CanPlayCard(i, t, out _))
                    {
                        string cardName = _engine.GetCardDef(_engine.State.Hand[i]).Name;
                        _engine.PlayCard(i, t);
                        StartPlayback();
                        return $"已出牌(目標敵{t}):{cardName}";
                    }
                }
            }
            return "無可出之牌";
        }

        // ---- 事件播放 ----

        private void StartPlayback()
        {
            StartCoroutine(PlaybackRoutine());
        }

        private IEnumerator PlaybackRoutine()
        {
            InputEnabled = false;
            _hand.SetInteractable(false);
            _endTurnButton.interactable = false;

            var events = new List<CombatEvent>(_engine.Events);
            _engine.ClearEvents();
            foreach (var combatEvent in events)
            {
                float beat = PresentEvent(combatEvent);
                RefreshUnits();
                if (beat > 0f) yield return new WaitForSeconds(beat);
            }

            if (_engine.State.Phase == CombatPhase.AwaitingChoice)
            {
                _choicePanel.Show(_engine);   // 面板確認後經 SubmitChoice 續播
                yield break;
            }

            RefreshAll();
            if (_engine.State.Phase == CombatPhase.Victory)
            {
                OnCombatEnded?.Invoke(true);
            }
            else if (_engine.State.Phase == CombatPhase.Defeat)
            {
                OnCombatEnded?.Invoke(false);
            }
            else
            {
                InputEnabled = true;
                _hand.SetInteractable(true);
                _endTurnButton.interactable = true;
            }
        }

        private void OnDestroy()
        {
            if (_overlayRoot != null)
            {
                Destroy(_overlayRoot.gameObject);
            }
        }

        /// <summary>單一事件的呈現;回傳節拍秒數。節奏數值屬 M7 手感範圍,先求可讀。</summary>
        private float PresentEvent(in CombatEvent e)
        {
            switch (e.Kind)
            {
                case EventKind.DamageDealt:
                    SpawnNumber(e.TargetIndex, e.HpLost > 0 ? $"-{e.HpLost}" : "格擋",
                        e.HpLost > 0 ? new Color(1f, 0.35f, 0.3f) : new Color(0.6f, 0.8f, 1f));
                    if (e.TargetIndex == CombatEngine.PlayerIndex)
                    {
                        if (e.HpLost > 0) _hud.PlayHitShake();
                    }
                    else
                    {
                        _enemyViews[e.TargetIndex].PlayHitFeedback();
                    }
                    return 0.22f;
                case EventKind.HpLost:
                    SpawnNumber(e.TargetIndex, $"-{e.Amount}", new Color(0.8f, 0.4f, 0.9f));
                    if (e.TargetIndex == CombatEngine.PlayerIndex) _hud.PlayHitShake();
                    return 0.15f;
                case EventKind.HpHealed:
                    SpawnNumber(e.TargetIndex, $"+{e.Amount}", new Color(0.4f, 0.95f, 0.5f));
                    return 0.15f;
                case EventKind.BlockGained:
                    SpawnNumber(e.SourceIndex, $"盾+{e.Amount}", new Color(0.6f, 0.8f, 1f));
                    return 0.06f;
                case EventKind.EnemyMoveStarted:
                    _enemyViews[e.SourceIndex].PlayAttackLunge();
                    return 0.38f;
                case EventKind.EnemyDied:
                    _enemyViews[e.TargetIndex].PlayDeath();
                    return 0.3f;
                case EventKind.CardDrawn: return 0.04f;
                case EventKind.CardPlayed: return 0.08f;
                case EventKind.TurnStarted: return 0.1f;
                case EventKind.PileShuffled: return 0.1f;
                case EventKind.StatusChanged: return 0.05f;
                case EventKind.CombatEnded: return 0.2f;
                default: return 0f;
            }
        }

        private void SpawnNumber(int combatantIndex, string text, Color color)
        {
            Vector3 pos = combatantIndex == CombatEngine.PlayerIndex
                ? _hud.transform.position + new Vector3(60f, 60f, 0f)
                : _enemyViews[combatantIndex].transform.position;
            _damagePool.Spawn(pos, text, color);
        }

        private void RefreshUnits()
        {
            _hud.RefreshFrom(_engine);
            foreach (var enemyView in _enemyViews)
            {
                enemyView.RefreshFrom(_engine);
            }
        }

        private void RefreshAll()
        {
            RefreshUnits();
            _hand.Rebuild(_engine);
            _drawPileLabel.text = $"抽牌 {_engine.State.DrawPile.Count}";
            _discardPileLabel.text = $"棄牌 {_engine.State.DiscardPile.Count}";
            _exhaustPileLabel.text = $"消耗 {_engine.State.ExhaustPile.Count}";
            RebuildPotionBar();
        }

        private void RebuildPotionBar()
        {
            foreach (Transform child in _potionBar)
            {
                Destroy(child.gameObject);
            }
            for (int slot = 0; slot < _engine.State.PotionSlots.Count; slot++)
            {
                var potionId = _engine.State.PotionSlots[slot];
                if (potionId == null) continue;
                int captured = slot;
                var def = _game.Db.GetPotion(potionId);
                var button = UiKit.CreateButton($"藥水{slot}", _potionBar, def.Name, 20f,
                    new Color(0.35f, 0.25f, 0.5f), () => OnPotionClicked(captured));
                UiKit.Place((RectTransform)button.transform, new Vector2(slot * 150f + 75f, 0f), new Vector2(140f, 52f),
                    new Vector2(0f, 0.5f));
            }
        }

        private void Flash(string message)
        {
            _hintText.text = message;
            _hintGroup.DOKill();
            _hintGroup.alpha = 1f;
            _hintGroup.DOFade(0f, 1.2f).SetEase(Ease.InQuad).SetLink(_hintGroup.gameObject);
        }

    }
}
