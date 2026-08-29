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
        private PileListOverlay _pileOverlay;
        private TooltipView _tooltip;
        /// <summary>選卡模式(消耗手牌):直接點手上的牌,湊滿張數自動送出。</summary>
        public bool IsChoiceMode { get; private set; }
        private readonly List<int> _choiceSelected = new List<int>();
        private int _choiceRequired;
        /// <summary>從棄牌堆挑牌時開著的卡片網格;送出後要收掉,不然會留在畫面上。</summary>
        private DeckViewOverlay _discardPicker;
        /// <summary>選卡與藥水瞄準共用的模式提示(兩者互斥,不會同時開)。</summary>
        private TextMeshProUGUI _modeHint;
        private AimOverlay _aimOverlay;
        /// <summary>正在瞄準的藥水格;-1 = 沒在瞄準。</summary>
        private int _aimingPotionSlot = -1;
        /// <summary>藥水瞄準模式:點敵人施放,點別處取消。期間手牌不能拖曳。</summary>
        public bool IsPotionAiming => _aimingPotionSlot >= 0;
        private Button _endTurnButton;
        private TextMeshProUGUI _drawPileLabel;
        private TextMeshProUGUI _discardPileLabel;
        private TextMeshProUGUI _exhaustPileLabel;
        private EnergyOrbView _energyOrb;
        private TopBarView _topBar;
        private TextMeshProUGUI _hintText;
        private CanvasGroup _hintGroup;

        private static readonly List<RaycastResult> RaycastBuffer = new List<RaycastResult>();
        /// <summary>「尚未判定過」的哨兵值;-1 是合法的「沒指向任何敵人」。</summary>
        private const int NoPreviewIndex = -2;
        private CardView _draggingCard;
        private int _lastPreviewEnemyIndex = NoPreviewIndex;

        public static CombatScreenController Build(Transform screenLayer, Transform overlayLayer,
            GameController game, CombatEngine engine)
        {
            var root = UiKit.CreateRect("戰鬥畫面", screenLayer);
            UiKit.Stretch(root);
            var controller = root.gameObject.AddComponent<CombatScreenController>();
            controller._game = game;
            controller._engine = engine;

            // 版面參考原作:玩家在左、敵人在右、資訊列在上、能量球與牌堆在下方兩角
            controller._hud = PlayerHudView.Build(root);

            int enemyCount = engine.State.Enemies.Count;
            for (int i = 0; i < enemyCount; i++)
            {
                controller._enemyViews.Add(EnemyView.Build(root, i, new Vector2(260f + i * 300f, 60f)));
            }

            controller._hand = HandView.Build(root, controller);
            controller._energyOrb = EnergyOrbView.Build(root);
            controller._topBar = TopBarView.Build(root, game, engine, controller.OnPotionClicked);

            controller._endTurnButton = UiKit.CreateButton("結束回合", root, "結束回合", 28f,
                new Color(0.7f, 0.5f, 0.15f), controller.OnEndTurnClicked);
            UiKit.Place((RectTransform)controller._endTurnButton.transform,
                new Vector2(-200f, 250f), new Vector2(260f, 70f), new Vector2(1f, 0f));

            // 牌堆:抽牌左下角、棄牌與消耗右下角
            controller._drawPileLabel = controller.BuildPileButton(root, "抽牌堆", new Vector2(120f, 70f),
                new Vector2(0f, 0f), () => controller.ShowPile("抽牌堆", engine.State.DrawPile, true));
            controller._discardPileLabel = controller.BuildPileButton(root, "棄牌堆", new Vector2(-120f, 70f),
                new Vector2(1f, 0f), () => controller.ShowPile("棄牌堆", engine.State.DiscardPile, false));
            controller._exhaustPileLabel = controller.BuildPileButton(root, "消耗堆", new Vector2(-120f, 145f),
                new Vector2(1f, 0f), () => controller.ShowPile("消耗堆", engine.State.ExhaustPile, false));

            // 模式提示(選卡消耗手牌 / 藥水瞄準)
            controller._modeHint = UiKit.CreateText("模式提示", root, "", 34f, new Color(1f, 0.85f, 0.4f));
            UiKit.Place(controller._modeHint.rectTransform, new Vector2(0f, 430f), new Vector2(900f, 48f));
            controller._modeHint.gameObject.SetActive(false);

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
            // 遮罩要比箭頭早建:兩者都會 SetAsLastSibling,箭頭後顯示才會蓋在遮罩上面
            controller._aimOverlay = AimOverlay.Build(controller._overlayRoot,
                controller.OnAimMove, controller.OnAimClick);
            controller._arrow = TargetArrowView.Build(controller._overlayRoot);
            controller._pileOverlay = PileListOverlay.Build(controller._overlayRoot);
            controller._tooltip = game.Tooltip;   // 全域提示框(所有畫面共用)

            // 指到敵人/自己看狀態說明;遺物列與藥水鈕的提示各自在建構處掛上
            for (int i = 0; i < controller._enemyViews.Count; i++)
            {
                int index = i;
                TooltipTrigger.Attach(controller._enemyViews[i].gameObject, controller._tooltip,
                    () => TooltipText.敵人(game.Db, engine, index));
            }
            TooltipTrigger.Attach(controller._hud.gameObject, controller._tooltip,
                () => TooltipText.玩家(game.Db, engine));
            RelicBarView.Build(root, game.Db, engine.Relics, controller._tooltip);

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
            _draggingCard = card;
            _lastPreviewEnemyIndex = NoPreviewIndex;
            _arrow.Show();
        }

        public void UpdateTargeting(PointerEventData eventData)
        {
            if (_draggingCard == null) return;
            // 起點取卡片頂端:用 TransformPoint 換算,不能把畫布單位直接加到螢幕座標上(單位不同)
            var origin = _draggingCard.Rect.TransformPoint(new Vector3(0f, _draggingCard.Rect.rect.height * 0.5f, 0f));
            _arrow.UpdateCurve(origin, eventData.position);

            // 只在指向的敵人「換人」時重算描述——每幀重設 TMP 文字會白白觸發重排版
            int enemyIndex = RaycastEnemyIndex(eventData);
            if (enemyIndex == _lastPreviewEnemyIndex) return;
            _lastPreviewEnemyIndex = enemyIndex;
            var target = enemyIndex >= 0
                ? _engine.State.Enemies[enemyIndex]
                : HandView.DefaultPreviewTarget(_engine);
            _draggingCard.RefreshDescription(_engine.State.Player, target, _engine);
        }

        public void EndTargeting()
        {
            _arrow.Hide();
            if (_draggingCard != null)
            {
                // 出牌失敗/取消時卡會留在手上,描述要回到預設目標的值
                _draggingCard.RefreshDescription(_engine.State.Player, HandView.DefaultPreviewTarget(_engine), _engine);
                _draggingCard = null;
            }
            _lastPreviewEnemyIndex = NoPreviewIndex;
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

        // ---- 選卡模式:直接點手上的牌 ----

        public bool IsChosen(int handIndex)
        {
            return _choiceSelected.Contains(handIndex);
        }

        private void EnterChoiceMode()
        {
            IsChoiceMode = true;
            _choiceRequired = _engine.State.PendingChoiceCount;
            _choiceSelected.Clear();

            if (_engine.State.PendingChoiceSource == ChoiceSource.Discard)
            {
                // 棄牌堆的牌不在畫面上,點不到——改開卡片網格讓玩家挑
                OpenDiscardPicker();
                return;
            }
            _modeHint.gameObject.SetActive(true);
            RefreshChoiceHint();
            _hand.ClearSelections();
            _hand.SetInteractable(true);   // 手牌要能點,但拖曳出牌由 CardView 擋掉
        }

        /// <summary>選卡動作的動詞,提示文字與煙霧回報都用它。</summary>
        private string 選卡動詞
        {
            get
            {
                switch (_engine.State.PendingChoiceAction)
                {
                    case ChoiceAction.UpgradeForCombat: return "升級";
                    case ChoiceAction.MoveToDrawTop: return "放到抽牌堆頂";
                    default: return "消耗";
                }
            }
        }

        /// <summary>從棄牌堆挑牌(頭槌型)。挑滿張數就送出;每挑一張重開一次,已挑的會被濾掉。</summary>
        private void OpenDiscardPicker()
        {
            var discard = _engine.State.DiscardPile;
            var picked = new List<int>(_choiceRequired);

            void Open()
            {
                _discardPicker = DeckViewOverlay.Open(_overlayRoot, $"選擇 {_choiceRequired} 張要{選卡動詞}的牌",
                    discard, card => _engine.GetCardDef(card),
                    card => !picked.Contains(discard.IndexOf(card)),
                    index =>
                    {
                        picked.Add(index);
                        if (picked.Count < _choiceRequired)
                        {
                            Open();
                            return;
                        }
                        ExitChoiceMode();
                        SubmitChoice(picked.ToArray());
                    },
                    _engine.State.Player);
            }

            Open();
        }

        public void ToggleChoiceSelection(int handIndex)
        {
            if (!IsChoiceMode) return;
            if (_choiceSelected.Remove(handIndex))
            {
                _hand.SetSelected(handIndex, false);
                RefreshChoiceHint();
                return;
            }
            if (_choiceSelected.Count >= _choiceRequired) return;
            _choiceSelected.Add(handIndex);
            _hand.SetSelected(handIndex, true);
            RefreshChoiceHint();
            if (_choiceSelected.Count == _choiceRequired)
            {
                SubmitChoice(_choiceSelected.ToArray());
            }
        }

        private void RefreshChoiceHint()
        {
            _modeHint.text = $"點選 {_choiceRequired} 張要{選卡動詞}的手牌({_choiceSelected.Count}/{_choiceRequired})";
        }

        private void ExitChoiceMode()
        {
            IsChoiceMode = false;
            _choiceSelected.Clear();
            _modeHint.gameObject.SetActive(false);
            if (_discardPicker != null)
            {
                Destroy(_discardPicker.gameObject);
                _discardPicker = null;
            }
        }

        /// <summary>
        /// verify 煙霧入口:選卡模式下從左邊開始選滿要求的張數。
        /// 自動化沒有偏好,只需要能把流程推下去——沒有這條,任何觸發選卡的牌都會讓整輪煙霧永遠卡住。
        /// </summary>
        public string 煙霧_選滿要消耗的牌()
        {
            if (!IsChoiceMode) return "不在選卡模式";
            if (_engine.State.PendingChoiceSource == ChoiceSource.Discard)
            {
                // 棄牌堆的挑選走的是卡片網格,不是點手牌:直接挑前幾張送出
                if (_discardPicker != null) Destroy(_discardPicker.gameObject);
                _discardPicker = null;
                int need = Mathf.Min(_choiceRequired, _engine.State.DiscardPile.Count);
                var picks = new int[need];
                for (int i = 0; i < need; i++) picks[i] = i;
                ExitChoiceMode();
                SubmitChoice(picks);
                return "已從棄牌堆挑滿並送出";
            }
            int guard = 0;
            while (IsChoiceMode && guard++ <= CombatState.HandLimit)
            {
                int pick = -1;
                for (int i = 0; i < _engine.State.Hand.Count; i++)
                {
                    if (!IsChosen(i)) { pick = i; break; }
                }
                if (pick < 0) break;
                ToggleChoiceSelection(pick);   // 湊滿張數時它自己會送出
            }
            return IsChoiceMode ? "選卡未完成" : "已選滿並送出";
        }

        public void SubmitChoice(int[] handIndices)
        {
            ExitChoiceMode();
            _engine.ResolveChoice(handIndices);
            StartPlayback();
        }

        // ---- 藥水:需要目標的先進瞄準模式,點敵人才丟出去 ----

        private void OnPotionClicked(int slot)
        {
            // 瞄準中整個畫面被遮罩蓋住,藥水鈕收不到點擊——這裡只是防禦
            if (!InputEnabled || IsChoiceMode || IsPotionAiming) return;
            var potionId = _engine.State.PotionSlots[slot];
            if (potionId == null) return;

            var chip = _topBar.GetPotionChip(slot);
            var anchor = chip != null
                ? (Vector2)chip.TransformPoint(new Vector3(0f, -chip.rect.height * 0.5f, 0f))
                : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            PotionMenuView.Open(_overlayRoot, anchor, _game.Db.GetPotion(potionId).Name,
                () => UsePotionFromMenu(slot), () => DiscardPotionFromMenu(slot));
        }

        private void UsePotionFromMenu(int slot)
        {
            if (!InputEnabled) return;
            var potionId = _engine.State.PotionSlots[slot];
            if (potionId == null) return;
            if (_game.Db.GetPotion(potionId).NeedsTarget)
            {
                BeginPotionAim(slot);
                return;
            }
            _engine.UsePotion(slot, -1);
            StartPlayback();
        }

        private void DiscardPotionFromMenu(int slot)
        {
            if (!InputEnabled) return;
            if (_engine.State.PotionSlots[slot] == null) return;
            _engine.DiscardPotion(slot);   // 效果完全不觸發,只是把格子空出來
            RefreshAll();
        }

        /// <summary>開始瞄準:箭頭從那格藥水拉出來,點到敵人才真的使用。</summary>
        public void BeginPotionAim(int slot)
        {
            if (!InputEnabled || IsChoiceMode) return;
            var potionId = _engine.State.PotionSlots[slot];
            if (potionId == null) return;
            if (!AnyLivingEnemy())
            {
                Flash("沒有可以指定的敵人");
                return;
            }
            _aimingPotionSlot = slot;
            _topBar.SetPotionAiming(slot, true);
            _modeHint.gameObject.SetActive(true);
            _modeHint.text = $"選擇「{_game.Db.GetPotion(potionId).Name}」的目標(右鍵或點別處取消)";
            _aimOverlay.Show();
            _arrow.Show();
            // 一開就把箭頭拉到游標,不要先閃一團堆在藥水格上的節點
            bool hasCursor = AimOverlay.TryGetCursor(out var cursor);
            UpdateAimArrow(cursor, hasCursor);
        }

        /// <summary>
        /// 確認瞄準:對指定的敵人使用。
        /// 與點擊路徑分開,是為了讓自動化(煙霧/驗證)能走同一條指令路徑而不必偽造滑鼠事件。
        /// </summary>
        public string ConfirmPotionAim(int enemyIndex)
        {
            if (!IsPotionAiming) return "不在藥水瞄準模式";
            if (enemyIndex < 0 || enemyIndex >= _engine.State.Enemies.Count
                || !_engine.State.Enemies[enemyIndex].IsAlive)
            {
                return "目標無效";
            }
            int slot = _aimingPotionSlot;
            string potionName = _game.Db.GetPotion(_engine.State.PotionSlots[slot]).Name;
            CancelPotionAim();
            _engine.UsePotion(slot, enemyIndex);
            StartPlayback();
            return $"已使用 {potionName}(目標敵{enemyIndex})";
        }

        public void CancelPotionAim()
        {
            if (!IsPotionAiming) return;
            _topBar.SetPotionAiming(_aimingPotionSlot, false);
            _aimingPotionSlot = -1;
            _modeHint.gameObject.SetActive(false);
            _aimOverlay.Hide();
            _arrow.Hide();
        }

        private void OnAimMove(Vector2 cursorScreen)
        {
            if (!IsPotionAiming) return;
            UpdateAimArrow(cursorScreen, true);
        }

        private void OnAimClick(PointerEventData eventData)
        {
            if (!IsPotionAiming) return;
            if (eventData.button != PointerEventData.InputButton.Left)
            {
                CancelPotionAim();   // 右鍵 = 取消,不必特地點到空白處
                return;
            }
            int enemyIndex = RaycastEnemyIndex(eventData);
            if (enemyIndex < 0 || !_engine.State.Enemies[enemyIndex].IsAlive)
            {
                CancelPotionAim();   // 沒點在活著的敵人身上 = 取消,不當錯誤
                return;
            }
            ConfirmPotionAim(enemyIndex);
        }

        /// <summary>箭頭從藥水格拉到游標;還沒收到指標位置時先指向自己,避免出現一條指到畫面角落的線。</summary>
        private void UpdateAimArrow(Vector2 cursorScreen, bool hasCursor)
        {
            var chip = _topBar.GetPotionChip(_aimingPotionSlot);
            if (chip == null) return;
            var origin = chip.TransformPoint(new Vector3(0f, -chip.rect.height * 0.5f, 0f));
            _arrow.UpdateCurve(origin, hasCursor ? cursorScreen : (Vector2)origin);
        }

        private bool AnyLivingEnemy()
        {
            for (int i = 0; i < _engine.State.Enemies.Count; i++)
            {
                if (_engine.State.Enemies[i].IsAlive) return true;
            }
            return false;
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
            CancelPotionAim();   // 任何路徑開始播放都先收掉瞄準,箭頭不會留在畫面上
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
                RefreshAll();          // 手牌要是最新的,玩家才點得到正確的牌
                EnterChoiceMode();     // 點選湊滿張數後經 SubmitChoice 續播
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
                case EventKind.TurnStarted:
                    ShowTurnBanner($"第 {e.Amount} 回合");
                    return 0.35f;
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
            _energyOrb.RefreshFrom(_engine);
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
            _topBar.Refresh();
        }

        /// <summary>回合開始橫幅:大字掃過畫面中央後淡出,給回合切換一個節拍。</summary>
        private void ShowTurnBanner(string message)
        {
            var banner = UiKit.CreateText("回合橫幅", _overlayRoot, message, 72f, new Color(1f, 0.9f, 0.55f));
            UiKit.Place(banner.rectTransform, new Vector2(-220f, 60f), new Vector2(900f, 100f));
            var group = banner.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.alpha = 0f;
            DOTween.Sequence()
                .Append(banner.rectTransform.DOAnchorPosX(0f, 0.28f).SetEase(Ease.OutCubic))
                .Join(group.DOFade(1f, 0.2f))
                .AppendInterval(0.35f)
                .Append(banner.rectTransform.DOAnchorPosX(220f, 0.3f).SetEase(Ease.InCubic))
                .Join(group.DOFade(0f, 0.3f))
                .OnComplete(() => Destroy(banner.gameObject))
                .SetLink(banner.gameObject);
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
