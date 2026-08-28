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
        /// <summary>全域提示框:所有畫面(戰鬥/商店/獎勵/地圖)共用同一個,才不會有的畫面能預覽有的不能。</summary>
        public TooltipView Tooltip { get; private set; }
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
            var root = UiKit.CreateRect(screenName, _screenLayer);
            UiKit.Stretch(root);
            var group = root.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.DOFade(1f, 0.2f).SetEase(Ease.OutCubic).SetLink(root.gameObject);
            _currentScreen = root;
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
                case RunPhase.ChoosingNode:
                    // 寶箱:已自動開箱回到地圖
                    string relicName = entry.TreasureRelicId != null
                        ? Db.GetRelicDef(entry.TreasureRelicId).Name
                        : null;
                    ShowMap(relicName != null ? $"開啟寶箱:獲得「{relicName}」!" : "寶箱是空的…");
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
            Combat = CombatScreenController.Build(root, _overlayLayer, this, engine);
            Combat.OnCombatEnded = victory => OnCombatFinished(engine, victory);
        }

        private void OnCombatFinished(CombatEngine engine, bool victory)
        {
            Run.ApplyCombatResult(victory, engine.State.Player.Hp, ComputeUsedPotions(engine));
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
            BackToRewardList();
        }

        public void RewardSkipCard()
        {
            Run.SkipCardReward();
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
            var screen = FindRewardScreen();
            if (screen != null) screen.Render();
        }

        private RewardScreenController FindRewardScreen()
        {
            return _currentScreen != null ? _currentScreen.GetComponentInChildren<RewardScreenController>() : null;
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
            DeckViewOverlay.Open(_overlayLayer, "選擇要移除的卡", Run.State.Deck,
                card => Db.GetCard(card.ResolvedCardId), null,
                deckIndex =>
                {
                    if (Run.BuyRemoveCard(deckIndex)) RerenderShop();
                });
        }

        public void ShopLeave()
        {
            Run.LeaveShop();
            ShowMap(null);
        }

        private void RerenderShop()
        {
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
                        // 等播放結束才下指令;打得動就出牌,否則結束回合
                        yield return new WaitUntil(() => Combat == null || Combat.InputEnabled
                            || Run.State.Phase != RunPhase.InCombat);
                        if (Run.State.Phase != RunPhase.InCombat || Combat == null) break;
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
