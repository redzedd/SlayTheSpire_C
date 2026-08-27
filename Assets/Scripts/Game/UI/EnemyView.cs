using System.Text;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using STS.Core.Combat;
using STS.Core.Combat.Enemies;

namespace STS.Game.UI
{
    /// <summary>
    /// 單一敵人的佔位視圖:色塊軀體+名稱+血條+格擋+意圖+狀態列。
    /// 只讀引擎快照(RefreshFrom),不持有引擎參照——資料流單向。
    /// </summary>
    public sealed class EnemyView : MonoBehaviour
    {
        public int EnemyIndex { get; private set; }

        private TextMeshProUGUI _nameText;
        private TextMeshProUGUI _intentText;
        private TextMeshProUGUI _blockText;
        private TextMeshProUGUI _statusText;
        private TextMeshProUGUI _hpText;
        private RectTransform _hpFill;
        private Image _body;

        public static EnemyView Build(Transform parent, int enemyIndex, Vector2 anchoredPos)
        {
            var body = UiKit.CreatePanel($"敵人{enemyIndex}", parent, UiKit.敵人色);
            UiKit.Place(body.rectTransform, anchoredPos, new Vector2(200f, 240f));
            var view = body.gameObject.AddComponent<EnemyView>();
            view.EnemyIndex = enemyIndex;
            view._body = body;

            view._intentText = UiKit.CreateText("意圖", body.transform, "", 30f, new Color(1f, 0.85f, 0.4f));
            UiKit.Place(view._intentText.rectTransform, new Vector2(0f, 150f), new Vector2(240f, 40f));

            view._nameText = UiKit.CreateText("名稱", body.transform, "", 26f);
            UiKit.Place(view._nameText.rectTransform, new Vector2(0f, 90f), new Vector2(220f, 34f));

            view._hpFill = UiKit.CreateBar("血條", body.transform, new Vector2(0f, -132f), new Vector2(190f, 18f),
                new Vector2(0.5f, 0.5f), new Color(0.2f, 0.05f, 0.05f), new Color(0.85f, 0.2f, 0.2f));
            view._hpText = UiKit.CreateText("血量", body.transform, "", 20f);
            UiKit.Place(view._hpText.rectTransform, new Vector2(0f, -158f), new Vector2(200f, 26f));

            view._blockText = UiKit.CreateText("格擋", body.transform, "", 26f, new Color(0.55f, 0.8f, 1f));
            UiKit.Place(view._blockText.rectTransform, new Vector2(-110f, -132f), new Vector2(70f, 32f));

            view._statusText = UiKit.CreateText("狀態列", body.transform, "", 20f, new Color(0.9f, 0.9f, 0.6f));
            UiKit.Place(view._statusText.rectTransform, new Vector2(0f, -192f), new Vector2(240f, 30f));
            return view;
        }

        /// <summary>受擊回饋:閃白+punch 縮放(dotween 紀律:先殺舊 tween、SetLink)。</summary>
        public void PlayHitFeedback()
        {
            if (_body == null) return;
            _body.DOKill();
            _body.color = Color.white;
            _body.DOColor(UiKit.敵人色, 0.25f).SetEase(Ease.OutQuad).SetLink(gameObject);
            transform.DOKill(true);
            transform.DOPunchScale(new Vector3(0.12f, 0.12f, 0f), 0.25f, 8).SetLink(gameObject);
        }

        /// <summary>出招前撲:往玩家方向(左下)快速一衝。</summary>
        public void PlayAttackLunge()
        {
            transform.DOKill(true);
            transform.DOPunchPosition(new Vector3(-40f, -25f, 0f), 0.3f, 1, 0.5f).SetLink(gameObject);
        }

        /// <summary>死亡:縮小(灰化由 RefreshFrom 處理)。</summary>
        public void PlayDeath()
        {
            transform.DOKill(true);
            transform.DOScale(0.85f, 0.3f).SetEase(Ease.OutQuad).SetLink(gameObject);
        }

        public void RefreshFrom(CombatEngine engine)
        {
            var enemy = engine.State.Enemies[EnemyIndex];
            _nameText.text = enemy.Name;
            _hpText.text = $"{enemy.Hp}/{enemy.MaxHp}";
            UiKit.TweenBarFill(_hpFill, enemy.MaxHp > 0 ? (float)enemy.Hp / enemy.MaxHp : 0f);
            _blockText.text = enemy.Block > 0 ? $"盾{enemy.Block}" : "";
            _statusText.text = StatusRowText.Build(enemy);

            if (!enemy.IsAlive)
            {
                _body.color = new Color(0.2f, 0.2f, 0.2f, 0.4f);
                _intentText.text = "";
                _statusText.text = "";
                return;
            }
            string intent = IntentText(engine.GetIntentPreview(EnemyIndex));
            if (intent != _lastIntentText)
            {
                _lastIntentText = intent;
                _intentText.text = intent;
                _intentText.rectTransform.DOKill(true);
                _intentText.rectTransform.DOPunchScale(Vector3.one * 0.22f, 0.3f, 6).SetLink(gameObject);
            }
        }

        private string _lastIntentText;

        private static string IntentText(IntentInfo intent)
        {
            switch (intent.Type)
            {
                case IntentType.Attack:
                    return intent.Hits > 1 ? $"攻 {intent.Damage}×{intent.Hits}" : $"攻 {intent.Damage}";
                case IntentType.Defend: return "防禦";
                case IntentType.Buff: return "強化";
                case IntentType.Debuff: return "弱化";
                default: return "?";
            }
        }
    }

    /// <summary>狀態列文字(玩家/敵人共用):「力+3 易2 弱1」式縮寫。</summary>
    internal static class StatusRowText
    {
        private static readonly StringBuilder Buffer = new StringBuilder(64);

        internal static string Build(CombatantState combatant)
        {
            Buffer.Clear();
            foreach (var status in combatant.Statuses)
            {
                if (Buffer.Length > 0) Buffer.Append(' ');
                Buffer.Append(縮寫(status.Id)).Append(status.Stacks);
            }
            return Buffer.ToString();
        }

        private static string 縮寫(Core.Combat.Statuses.StatusId id)
        {
            switch (id)
            {
                case Core.Combat.Statuses.StatusId.Strength: return "力";
                case Core.Combat.Statuses.StatusId.Dexterity: return "敏";
                case Core.Combat.Statuses.StatusId.Weak: return "弱";
                case Core.Combat.Statuses.StatusId.Vulnerable: return "易";
                case Core.Combat.Statuses.StatusId.Frail: return "脆";
                case Core.Combat.Statuses.StatusId.NoDraw: return "禁抽";
                case Core.Combat.Statuses.StatusId.Ritual: return "儀";
                case Core.Combat.Statuses.StatusId.Enrage: return "怒";
                case Core.Combat.Statuses.StatusId.Curl: return "捲";
                case Core.Combat.Statuses.StatusId.Metallicize: return "金";
                case Core.Combat.Statuses.StatusId.DemonForm: return "魔";
                case Core.Combat.Statuses.StatusId.SharpHide: return "刺";
                case Core.Combat.Statuses.StatusId.LoseStrengthAtTurnEnd: return "失力";
                default: return "?";
            }
        }
    }
}
