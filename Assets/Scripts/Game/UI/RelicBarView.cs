using UnityEngine;
using STS.Core.Content;
using STS.Core.Relics;

namespace STS.Game.UI
{
    /// <summary>
    /// 遺物列(戰鬥畫面左上):每顆遺物一個色塊,滑鼠指上去看名稱與效果。
    /// 佔位美術=色塊+名稱首字;計數型遺物(雙節棍)在提示裡顯示目前計數。
    /// </summary>
    public sealed class RelicBarView : MonoBehaviour
    {
        private const float 格寬 = 64f;
        private const float 間距 = 8f;

        public static RelicBarView Build(Transform parent, IContentCatalog catalog,
            System.Collections.Generic.IReadOnlyList<RelicInstance> relics, TooltipView tooltip)
        {
            var root = UiKit.CreateRect("遺物列", parent);
            UiKit.Place(root, new Vector2(40f, -110f), new Vector2(900f, 64f), new Vector2(0f, 1f), new Vector2(0f, 0.5f));
            var view = root.gameObject.AddComponent<RelicBarView>();

            for (int i = 0; i < relics.Count; i++)
            {
                var relic = relics[i];
                RelicDef def;
                try
                {
                    def = catalog.GetRelicDef(relic.Id);
                }
                catch (System.Collections.Generic.KeyNotFoundException)
                {
                    continue;   // 資料缺這顆遺物:不顯示,但不擋住戰鬥
                }
                var chip = UiKit.CreatePanel($"遺物_{relic.Id}", root, new Color(0.55f, 0.45f, 0.2f));
                UiKit.Place(chip.rectTransform, new Vector2(i * (格寬 + 間距), 0f), new Vector2(格寬, 格寬),
                    new Vector2(0f, 0.5f), new Vector2(0f, 0.5f));
                var label = UiKit.CreateText("字", chip.transform,
                    string.IsNullOrEmpty(def.Name) ? "?" : def.Name.Substring(0, 1), 30f);
                UiKit.Stretch(label.rectTransform);
                TooltipTrigger.Attach(chip.gameObject, tooltip, () => TooltipText.遺物(def, relic.Counter));
            }
            return view;
        }
    }
}
