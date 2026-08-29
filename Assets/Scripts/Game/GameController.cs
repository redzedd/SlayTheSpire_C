using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using STS.Core.Combat;
using STS.Core.Run;
using STS.Data;
using STS.Game.DataAssets;
using STS.Game.UI;

namespace STS.Game
{
    /// <summary>
    /// 遊戲入口與畫面路由(M6:完整一輪)。
    /// 設計原則:所有畫面按鈕都呼叫本類的動作方法(RewardTakeCard/ShopLeave/…),
    /// 煙霧測試走一模一樣的方法——「驗證走真路徑」。
    /// 場景零手工佈局;欄位空時 Resources 自癒載入。
    /// </summary>
    public sealed class GameController : MonoBehaviour
    {
        [SerializeField, Tooltip("內容總庫;留空會自動從 Resources 載入(Assets/Data/Resources/GameDatabase.asset)")]
        private GameDatabaseAsset database;

        [SerializeField, Tooltip("主字型:繁中動態 TMP 字型資產;留空會自動從 Resources 載入(Assets/Fonts/Resources/NotoSansTC SDF.asset)")]
        private TMP_FontAsset mainFont;

        [SerializeField, Tooltip("隨機種子;0 = 每輪隨機")]
        private long seed;

        public ContentDb Db { get; private set; }
        public RunEngine Run { get; private set; }
        public CombatScreenController Combat { get; private set; }
        /// <summary>戰鬥中的引擎;不在戰鬥時為 null(頂部資訊列靠它分辨要讀誰的血量與藥水)。</summary>
        public CombatEngine CombatEngine { get; private set; }
        /// <summary>全域提示框:所有畫面(戰鬥/商店/獎勵/地圖)共用同一個,才不會有的畫面能預覽有的不能。</summary>
        public TooltipView Tooltip { get; private set; }
        /// <summary>頂部資訊列:整輪常駐,不隨畫面切換重建(原作也是一直掛在上面)。</summary>
        public TopBarView TopBar { get; private set; }
        /// <summary>整輪煙霧的即時狀態(RunCommand 輪詢用)。</summary>
        public string 煙霧狀態 { get; private set; } = "未啟動";

        private Transform _screenLayer;
        private Transform _overlayLayer;
        private RectTransform _currentScreen;
        private List<string> _combatPotionsBefore = new List<string>();

        private void Awake()
        {
            if (database == null)
            {
                database = Resources.Load<GameDatabaseAsset>("GameDatabase");
            }
            if (mainFont == null)
            {
                mainFont = Resources.Load<TMP_FontAsset>("NotoSansTC SDF");
            }
            if (database == null)
            {
                Debug.LogError("GameController 找不到 GameDatabase(Resources 也沒有)——先跑選單 STS/重新匯入資料");
                enabled = false;
                return;
            }
            if (mainFont == null)
            {
                Debug.LogWarning("GameController 找不到繁中字型資產,文字將顯示為豆腐字——先跑選單 STS/生成 TMP 繁中字型資產");
            }
            // 編輯器失焦時 play mode 會停幀,自動化驗證(整輪煙霧)因此假死——常駐開啟
            Application.runInBackground = true;
            UiKit.MainFont = mainFont;
            Db = database.BuildDb();
            BuildCanvases();
            StartNewRun();
        }

        private void BuildCanvases()
        {
            _screenLayer = CreateCanvas("ScreenCanvas", 0);
            _overlayLayer = CreateCanvas("OverlayCanvas", 10);
            // 資訊列建在 overlay 層:畫面切換會銷毀 screen 層的整棵樹,常駐的東西不能放那裡
            TopBar = TopBarView.Build(_overlayLayer, this);
            Tooltip = TooltipView.Build(_overlayLayer);   // Show 時會 SetAsLastSibling,永遠壓在最上層
        }

        private Transform CreateCanvas(string canvasName, int sortingOrder)
        {
            var go = new GameObject(canvasName);
            go.transform.SetParent(transform, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            return go.transform;
        }

        // ---- 路由 ----

        private Transform NewScreenRoot(string screenName)
        {
            if (_currentScreen != null)
            {
                Destroy(_currentScreen.gameObject);
            }
            Combat = null;
            CombatEngine = null;   // 離開戰鬥後資訊列要改讀 RunState
            var root = UiKit.CreateRect(screenName, _screenLayer);
            UiKit.Stretch(root);
            var group = root.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.DOFade(1f, 0.2f).SetEase(Ease.OutCubic).SetLink(root.gameObject);
            _currentScreen = root;
            if (TopBar != null) TopBar.Refresh();   // 金幣/樓層/牌組在每次換畫面時都可能已經變了
            return root;
        }

        public void StartNewRun()
        {
            ulong runSeed = seed == 0 ? (ulong)System.Environment.TickCount : (ulong)seed;
            Run = RunEngine.NewRun(Db, runSeed);
            ShowMap(null);
        }

        private void ShowMap(string toastMessage)
        {
            MapScreenController.Build(NewScreenRoot("地圖畫面根"), this);
            if (!string.IsNullOrEmpty(toastMessage))
            {
                ShowToast(toastMessage);
            }
        }

        private void ShowToast(string message)
        {
            var toast = UiKit.CreateText("提示", _overlayLayer, message, 32f, new Color(1f, 0.9f, 0.5f));
            UiKit.Place(toast.rectTransform, new Vector2(0f, -180f), new Vector2(900f, 48f), new Vector2(0.5f, 1f));
            var group = toast.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            DOTween.Sequence()
                .AppendInterval(1.6f)
                .Append(group.DOFade(0f, 0.5f))
                .OnComplete(() => Destroy(toast.gameObject))
                .SetLink(toast.gameObject);
        }

        // ---- 地圖動作(地圖按鈕與煙霧共用) ----

        public void EnterNodeFromMap(int nodeId)
        {
            var entry = Run.EnterNode(nodeId);
            switch (Run.State.Phase)
            {
                case RunPhase.InCombat:
                    StartCombat(entry.EncounterId);
                    break;
                case RunPhase.InShop:
                    // 先進商人房間,點商人才看貨架
                    ShopRoomScreenController.Build(NewScreenRoot("商人房間根"), this);
                    break;
                case RunPhase.AtRest:
                    RestScreenController.Build(NewScreenRoot("燈火畫面根"), this);
                    break;
                case RunPhase.AtTreasure:
                    TreasureScreenController.Build(NewScreenRoot("寶箱畫面根"), this);
                    break;
            }
        }

        public void OpenDeckView()
        {
            DeckViewOverlay.Open(_overlayLayer, "牌組", Run.State.Deck,
                card => Db.GetCard(card.ResolvedCardId), null, null);
        }

        // ---- 戰鬥 ----

        private void StartCombat(string encounterId)
        {
            var setup = Run.BuildCombatSetup(encounterId);
            _combatPotionsBefore = new List<string>(setup.PotionIds);
            var engine = new CombatEngine(Db, Run.State.Rng, setup);
            engine.StartCombat();
            var root = NewScreenRoot("戰鬥畫面根");
            CombatEngine = engine;   // 要在建畫面之前:資訊列重繪時就會改讀戰鬥狀態
            Combat = CombatScreenController.Build(root, _overlayLayer, this, engine);
            Combat.OnCombatEnded = victory => OnCombatFinished(engine, victory);
            TopBar.Refresh();
        }

        private void OnCombatFinished(CombatEngine engine, bool victory)
        {
            Run.ApplyCombatResult(victory, engine.State.Player.Hp, ComputeUsedPotions(engine),
                engine.State.Player.MaxHp);
            switch (Run.State.Phase)
            {
                case RunPhase.ChoosingReward:
                    RewardScreenController.Build(NewScreenRoot("獎勵畫面根"), this);
                    break;
                case RunPhase.GameOver:
                    EndScreenController.Build(NewScreenRoot("終局畫面根"), this, false);
                    break;
                case RunPhase.RunClear:
                    EndScreenController.Build(NewScreenRoot("終局畫面根"), this, true);
                    break;
            }
        }

        /// <summary>開戰前後藥水清單的數量差 = 戰鬥中用掉的。</summary>
        private List<string> ComputeUsedPotions(CombatEngine engine)
        {
            var remaining = new List<string>();
            foreach (var id in engine.State.PotionSlots)
            {
                if (id != null) remaining.Add(id);
            }
            var used = new List<string>();
            foreach (var id in _combatPotionsBefore)
            {
                if (remaining.Contains(id))
                {
                    remaining.Remove(id);
                }
                else
                {
                    used.Add(id);
                }
            }
            return used;
        }

        // ---- 獎勵/商店/燈火動作(畫面按鈕與煙霧共用) ----

        public void RewardClaimGold()
        {
            if (Run.ClaimRewardGold()) RerenderReward();
        }

        public void RewardClaimPotion()
        {
            if (Run.ClaimRewardPotion()) RerenderReward();
            else ShowToast("藥水欄滿了,先用掉一瓶。");
        }

        public void RewardClaimRelic()
        {
            if (Run.ClaimRewardRelic()) RerenderReward();
        }

        public void RewardOpenCardPick()
        {
            var screen = FindRewardScreen();
            if (screen != null) screen.OpenCardPick();
        }

        public void RewardTakeCard(int choiceIndex)
        {
            Run.TakeCardReward(choiceIndex);
            TopBar.Refresh();   // 牌組張數變了
            BackToRewardList();
        }

        /// <summary>
        /// 從選卡畫面退回獎勵清單,卡牌那一項「不」結案——玩家可以再點進來重選。
        /// 真的放棄是從清單按「跳過」離開整個獎勵畫面(RewardLeave)。
        /// </summary>
        public void RewardBackToCardList()
        {
            BackToRewardList();
        }

        /// <summary>離開獎勵畫面回地圖——沒領的就是不要了。</summary>
        public void RewardLeave()
        {
            Run.LeaveRewards();
            ShowMap(null);
        }

        private void BackToRewardList()
        {
            var screen = FindRewardScreen();
            if (screen != null) screen.BackToList();
        }

        private void RerenderReward()
        {
            TopBar.Refresh();   // 領到的金幣/藥水/遺物都顯示在資訊列上
            var screen = FindRewardScreen();
            if (screen != null) screen.Render();
        }

        private RewardScreenController FindRewardScreen()
        {
            return _currentScreen != null ? _currentScreen.GetComponentInChildren<RewardScreenController>() : null;
        }

        // ---- 藥水(頂部資訊列常駐,戰鬥內外都會被點到) ----

        /// <summary>
        /// 點藥水格的單一入口。戰鬥中交給戰鬥畫面(要處理瞄準與播放),
        /// 戰鬥外自己開選單——能不能喝問資料,不是問「現在在不在戰鬥」。
        /// </summary>
        public void PotionClicked(int slot)
        {
            if (Combat != null)
            {
                Combat.OpenPotionMenu(slot);
                return;
            }
            if (Run == null) return;
            string potionId = slot >= 0 && slot < Run.State.PotionSlots.Length ? Run.State.PotionSlots[slot] : null;
            if (potionId == null) return;

            bool canUse = Run.CanUsePotionOutOfCombat(slot);
            PotionMenuView.Open(_overlayLayer as RectTransform ?? (RectTransform)_overlayLayer,
                PotionAnchor(slot), Db.GetPotion(potionId).Name,
                () => PotionUseOutOfCombat(slot), () => PotionDiscardOutOfCombat(slot),
                canUse, canUse ? null : "這瓶只能在戰鬥中喝");
        }

        private Vector2 PotionAnchor(int slot)
        {
            var chip = TopBar != null ? TopBar.GetPotionChip(slot) : null;
            return chip != null
                ? (Vector2)chip.TransformPoint(new Vector3(0f, -chip.rect.height * 0.5f, 0f))
                : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        }

        public void PotionUseOutOfCombat(int slot)
        {
            if (Run == null || !Run.UsePotionOutOfCombat(slot)) return;
            TopBar.Refresh();   // 血量與藥水欄都只顯示在資訊列上,重繪它就夠
        }

        public void PotionDiscardOutOfCombat(int slot)
        {
            if (Run == null || !Run.DiscardPotionOutOfCombat(slot)) return;
            TopBar.Refresh();
        }

        // ---- 寶箱動作(畫面按鈕與煙霧共用) ----

        /// <summary>掀開箱蓋(還沒收東西)。</summary>
        public void TreasureOpen()
        {
            var screen = FindTreasureScreen();
            if (screen != null) screen.Open();
        }

        /// <summary>
        /// 收下寶物,但留在寶箱畫面——要不要走由玩家按右邊的離開鍵決定。
        /// 拿完就被踢回地圖會讓人來不及看清楚拿到了什麼。
        /// </summary>
        public void TreasureClaim()
        {
            string relicName = Run.PendingTreasureRelicId != null
                ? Db.GetRelicDef(Run.PendingTreasureRelicId).Name
                : null;
            if (!Run.ClaimTreasure()) return;
            TopBar.Refresh();   // 遺物數在資訊列上,收下就要跟著加
            var screen = FindTreasureScreen();
            if (screen != null) screen.OnClaimed(relicName);
        }

        public void TreasureLeave()
        {
            Run.LeaveTreasure();
            ShowMap(null);
        }

        private TreasureScreenController FindTreasureScreen()
        {
            return _currentScreen != null ? _currentScreen.GetComponentInChildren<TreasureScreenController>() : null;
        }

        /// <summary>從商人房間進貨架。</summary>
        public void ShopOpenCounter()
        {
            ShopScreenController.Build(NewScreenRoot("商店畫面根"), this);
        }

        /// <summary>從貨架退回商人房間(離開整個商店要按房間裡的「前進」)。</summary>
        public void ShopBackToRoom()
        {
            ShopRoomScreenController.Build(NewScreenRoot("商人房間根"), this);
        }

        public void ShopBuyCard(int index)
        {
            if (Run.BuyCard(index)) RerenderShop();
        }

        public void ShopBuyRelic(int index)
        {
            if (Run.BuyRelic(index)) RerenderShop();
        }

        public void ShopBuyPotion(int index)
        {
            if (Run.BuyPotion(index)) RerenderShop();
        }

        public void ShopOpenRemovePicker()
        {
            // 移除是不可逆又要花錢的,點下去先跳確認頁
            DeckViewOverlay.Open(_overlayLayer, "選擇要移除的卡", Run.State.Deck,
                card => Db.GetCard(card.ResolvedCardId), null,
                deckIndex =>
                {
                    if (Run.BuyRemoveCard(deckIndex)) RerenderShop();
                },
                confirmVerb: "移除");
        }

        public void ShopLeave()
        {
            Run.LeaveShop();
            ShowMap(null);
        }

        private void RerenderShop()
        {
            TopBar.Refresh();   // 買賣會動金幣、藥水欄與牌組張數
            var shopScreen = _currentScreen != null ? _currentScreen.GetComponentInChildren<ShopScreenController>() : null;
            if (shopScreen != null) shopScreen.Render();
        }

        public void RestHealAction()
        {
            Run.RestHeal();
            ShowMap("休息完畢,精神一振。");
        }

        public void RestOpenUpgradePicker()
        {
            DeckViewOverlay.Open(_overlayLayer, "選擇要升級的卡", Run.State.Deck,
                card => Db.GetCard(card.ResolvedCardId),
                // 已升級、或本來就沒有升級版的卡(狀態卡)不可選
                card => !card.Upgraded && Db.AllCards.ContainsKey(card.CardId + "+"),
                deckIndex =>
                {
                    Run.RestUpgrade(deckIndex);
                    ShowMap("鍛造完成,卡牌升級!");
                },
                upgradedLookup: card => Db.GetCard(card.CardId + "+"));
        }

        // ---- 整輪煙霧(verify 管道 3):走與 UI 按鈕相同的動作方法 ----

        public void 煙霧_啟動自動一輪(int maxActions = 400)
        {
            StartCoroutine(SmokeRoutine(maxActions));
        }

        private IEnumerator SmokeRoutine(int maxActions)
        {
            煙霧狀態 = "進行中";
            int actions = 0;
            while (actions < maxActions
                && Run.State.Phase != RunPhase.GameOver
                && Run.State.Phase != RunPhase.RunClear)
            {
                actions++;
                switch (Run.State.Phase)
                {
                    case RunPhase.ChoosingNode:
                    {
                        var reachable = Run.GetReachableNodeIds();
                        if (reachable.Count == 0)
                        {
                            煙霧狀態 = "失敗:無可達節點";
                            WriteSmokeResult(actions);
                            yield break;
                        }
                        EnterNodeFromMap(reachable[0]);
                        yield return new WaitForSeconds(0.3f);
                        break;
                    }
                    case RunPhase.InCombat:
                    {
                        // 等播放結束才下指令;打得動就出牌,否則結束回合。
                        // 選卡模式也要算「可以下指令」——播放會停在那裡等玩家點牌,
                        // 不把它列進等待條件,任何觸發選卡的牌都會讓整輪煙霧永遠卡死。
                        yield return new WaitUntil(() => Combat == null || Combat.InputEnabled
                            || Combat.IsChoiceMode || Run.State.Phase != RunPhase.InCombat);
                        if (Run.State.Phase != RunPhase.InCombat || Combat == null) break;
                        if (Combat.IsChoiceMode)
                        {
                            Combat.煙霧_選滿要消耗的牌();
                            yield return new WaitForSeconds(0.15f);
                            break;
                        }
                        string outcome = Combat.煙霧_出第一張可出的牌();
                        if (outcome == "無可出之牌")
                        {
                            Combat.OnEndTurnClicked();
                        }
                        yield return new WaitForSeconds(0.15f);
                        break;
                    }
                    case RunPhase.ChoosingReward:
                    {
                        // 逐項領完再離開,走的是玩家按鈕的同一批方法
                        var pending = Run.PendingRewards;
                        if (pending.HasGold) RewardClaimGold();
                        else if (pending.HasRelic) RewardClaimRelic();
                        // 藥水欄滿了就拿不走,直接離開(玩家會看到提示,自動化沒必要卡在這)
                        else if (pending.HasPotion) { if (!Run.ClaimRewardPotion()) RewardLeave(); else RerenderReward(); }
                        else if (pending.HasCard) RewardTakeCard(0);
                        else RewardLeave();
                        yield return new WaitForSeconds(0.12f);
                        break;
                    }
                    case RunPhase.InShop:
                        ShopLeave();
                        yield return new WaitForSeconds(0.25f);
                        break;
                    case RunPhase.AtRest:
                        RestHealAction();
                        yield return new WaitForSeconds(0.25f);
                        break;
                    case RunPhase.AtTreasure:
                        // 開箱與收下都是純畫面動作,收完還要自己按離開才回地圖(與玩家同路徑)
                        TreasureOpen();
                        yield return new WaitForSeconds(0.15f);
                        if (Run.PendingTreasureRelicId != null) TreasureClaim();
                        yield return new WaitForSeconds(0.15f);
                        TreasureLeave();
                        yield return new WaitForSeconds(0.25f);
                        break;
                }
                煙霧狀態 = $"進行中:動作 {actions},樓層 {Run.State.Floor},階段 {Run.State.Phase}";
            }
            煙霧狀態 = $"結束:{Run.State.Phase},樓層 {Run.State.Floor},動作 {actions}";
            WriteSmokeResult(actions);
        }

        private void WriteSmokeResult(int actions)
        {
            System.IO.File.WriteAllText("Temp/STS_RunSmoke.txt",
                $"STS_RUN_SMOKE: {Run.State.Phase} | floor={Run.State.Floor} | actions={actions} | hp={Run.State.Hp}/{Run.State.MaxHp} | gold={Run.State.Gold} | deck={Run.State.Deck.Count}\n");
        }
    }
}
