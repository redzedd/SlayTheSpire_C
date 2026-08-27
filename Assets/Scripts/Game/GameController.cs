using TMPro;
using UnityEngine;
using UnityEngine.UI;
using STS.Core.Cards;
using STS.Core.Combat;
using STS.Core.Relics;
using STS.Core.Rng;
using STS.Data;
using STS.Game.DataAssets;
using STS.Game.UI;

namespace STS.Game
{
    /// <summary>
    /// 遊戲入口(M4:單場戰鬥)。Awake 時以程式建構全部 UI——場景只放本物件與 EventSystem,
    /// 場景檔零手工佈局(佔位美術期的刻意選擇;美術期再換 prefab 工作流)。
    /// </summary>
    public sealed class GameController : MonoBehaviour
    {
        [SerializeField, Tooltip("內容總庫;留空會自動從 Resources 載入(Assets/Data/Resources/GameDatabase.asset)")]
        private GameDatabaseAsset database;

        [SerializeField, Tooltip("主字型:繁中動態 TMP 字型資產;留空會自動從 Resources 載入(Assets/Fonts/Resources/NotoSansTC SDF.asset)")]
        private TMP_FontAsset mainFont;

        [SerializeField, Tooltip("要開打的遭遇 id(見 Assets/Data/Source/encounters.json)")]
        private string encounterId = "enc_slimes";

        [SerializeField, Tooltip("隨機種子;0 = 每次開場隨機")]
        private long seed;

        [SerializeField, Tooltip("玩家起始生命")]
        private int playerHp = 80;

        [SerializeField, Tooltip("起始遺物 id 清單(id 見 relics.json)")]
        private string[] relicIds = { RelicIds.BurningBlood };

        [SerializeField, Tooltip("起始藥水 id 清單(id 見 potions.json)")]
        private string[] potionIds = { "fire_potion" };

        public ContentDb Db { get; private set; }
        public CombatScreenController Combat { get; private set; }

        private Transform _screenLayer;
        private Transform _overlayLayer;

        private void Awake()
        {
            // 場景接線為可選:欄位為空時從 Resources 自行載入(自癒,場景建置流程不可靠也能開局)
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
            UiKit.MainFont = mainFont;   // static 注入:domain reload 後由場景重新設定
            Db = database.BuildDb();
            BuildCanvases();
            StartNewCombat();
        }

        private void BuildCanvases()
        {
            _screenLayer = CreateCanvas("ScreenCanvas", 0);
            _overlayLayer = CreateCanvas("OverlayCanvas", 10);
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

        private void StartNewCombat()
        {
            var encounter = Db.GetEncounter(encounterId);
            var setup = new CombatSetup { PlayerHp = playerHp, PlayerMaxHp = playerHp };

            int instanceId = 1;
            for (int i = 0; i < 5; i++) setup.Deck.Add(new CardInstance(instanceId++, "strike"));
            for (int i = 0; i < 4; i++) setup.Deck.Add(new CardInstance(instanceId++, "defend"));
            setup.Deck.Add(new CardInstance(instanceId++, "bash"));

            setup.EnemyIds.AddRange(encounter.EnemyIds);
            foreach (var relicId in relicIds) setup.Relics.Add(new RelicInstance(relicId));
            setup.PotionIds.AddRange(potionIds);

            ulong runSeed = seed == 0 ? (ulong)System.Environment.TickCount : (ulong)seed;
            var engine = new CombatEngine(Db, RunRng.FromSeed(runSeed), setup);
            engine.StartCombat();
            Combat = CombatScreenController.Build(_screenLayer, _overlayLayer, this, engine);
        }

        /// <summary>重開戰鬥:拆掉 UI 樹重建,不走場景重載(場景不必在 Build Settings)。</summary>
        public void RestartCombat()
        {
            Destroy(_screenLayer.gameObject);
            Destroy(_overlayLayer.gameObject);
            BuildCanvases();
            StartNewCombat();
        }
    }
}
