using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using STS.Core.Map;
using STS.Core.Run;

namespace STS.Game.UI
{
    /// <summary>
    /// 爬塔地圖畫面:節點色塊+單字標籤、連線、可達節點脈動高亮、目前位置標記、垂直捲動、
    /// 頂部 Run 狀態列(血/金/樓層/牌組)。點節點 → GameController.EnterNodeFromMap(同煙霧路徑)。
    /// </summary>
    public sealed class MapScreenController : MonoBehaviour
    {
        private const float 列高 = 130f;
        private const float 欄距 = 150f;

        private GameController _game;

        public static MapScreenController Build(Transform parent, GameController game)
        {
            var root = UiKit.CreateRect("地圖", parent);
            UiKit.Stretch(root);
            var controller = root.gameObject.AddComponent<MapScreenController>();
            controller._game = game;

            var run = game.Run.State;
            var map = run.Map;

            // 捲動視口與內容。視口必須有一張(近乎透明的)Image 當射線目標——
            // 沒有它,滾輪與拖曳只在剛好指到節點時有反應,指在空白處會完全捲不動。
            var viewportImage = UiKit.CreatePanel("視口", root, new Color(0f, 0f, 0f, 0.004f));
            var viewport = viewportImage.rectTransform;
            UiKit.Stretch(viewport, 0f);
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = UiKit.CreateRect("內容", viewport);
            float contentHeight = (map.NodeById(map.BossNodeId).Row + 1) * 列高 + 240f;
            content.anchorMin = new Vector2(0.5f, 0f);
            content.anchorMax = new Vector2(0.5f, 0f);
            content.pivot = new Vector2(0.5f, 0f);
            content.sizeDelta = new Vector2(1200f, contentHeight);
            content.anchoredPosition = Vector2.zero;

            var scroll = root.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.elasticity = 0.08f;
            scroll.inertia = true;
            scroll.decelerationRate = 0.12f;
            scroll.scrollSensitivity = 55f;

            Vector2 節點座標(MapNode node)
            {
                float x = (node.Col - (game.Db.Balance.MapColumns - 1) / 2f) * 欄距;
                if (node.Id == map.BossNodeId) x = 0f;
                return new Vector2(x, node.Row * 列高 + 140f);
            }

            // 連線(先畫,墊在節點下面)
            foreach (var edge in map.Edges)
            {
                var a = 節點座標(map.NodeById(edge.From));
                var b = 節點座標(map.NodeById(edge.To));
                var line = UiKit.CreatePanel("連線", content, new Color(0.45f, 0.42f, 0.38f, 0.8f));
                line.raycastTarget = false;
                var rect = line.rectTransform;
                rect.anchorMin = new Vector2(0.5f, 0f);
                rect.anchorMax = new Vector2(0.5f, 0f);
                rect.pivot = new Vector2(0f, 0.5f);
                rect.anchoredPosition = a;
                var delta = b - a;
                rect.sizeDelta = new Vector2(delta.magnitude, 5f);
                rect.localEulerAngles = new Vector3(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            }

            // 節點
            var reachable = game.Run.GetReachableNodeIds();
            foreach (var node in map.Nodes)
            {
                bool isCurrent = node.Id == run.CurrentNodeId;
                bool isReachable = reachable.Contains(node.Id);
                var block = UiKit.CreatePanel($"節點{node.Id}", content, 節點顏色(node.Type, isReachable, isCurrent));
                UiKit.Place(block.rectTransform, 節點座標(node),
                    node.Type == MapNodeType.Boss ? new Vector2(120f, 120f) : new Vector2(72f, 72f),
                    new Vector2(0.5f, 0f));
                var label = UiKit.CreateText("字", block.transform, 節點字(node.Type),
                    node.Type == MapNodeType.Boss ? 52f : 34f);
                UiKit.Stretch(label.rectTransform);

                if (isCurrent)
                {
                    var marker = UiKit.CreateText("目前", block.transform, "▲", 26f, new Color(1f, 0.9f, 0.4f));
                    UiKit.Place(marker.rectTransform, new Vector2(0f, -52f), new Vector2(60f, 30f));
                }
                if (isReachable)
                {
                    var button = block.gameObject.AddComponent<Button>();
                    int nodeId = node.Id;
                    button.onClick.AddListener(() => game.EnterNodeFromMap(nodeId));
                    block.rectTransform.DOScale(1.12f, 0.5f)
                        .SetLoops(-1, LoopType.Yoyo)
                        .SetEase(Ease.InOutSine)
                        .SetLink(block.gameObject);
                }
            }

            // 自動捲到目前進度附近
            int focusRow = run.CurrentNodeId >= 0 ? map.NodeById(run.CurrentNodeId).Row : 0;
            scroll.verticalNormalizedPosition = Mathf.Clamp01(focusRow / (float)(map.NodeById(map.BossNodeId).Row));

            controller.BuildHud(root, game);
            return controller;
        }

        private void BuildHud(RectTransform root, GameController game)
        {
            var bar = UiKit.CreatePanel("狀態列", root, new Color(0.1f, 0.1f, 0.13f, 0.95f));
            bar.rectTransform.anchorMin = new Vector2(0f, 1f);
            bar.rectTransform.anchorMax = new Vector2(1f, 1f);
            bar.rectTransform.pivot = new Vector2(0.5f, 1f);
            bar.rectTransform.anchoredPosition = Vector2.zero;
            bar.rectTransform.sizeDelta = new Vector2(0f, 64f);

            var run = game.Run.State;
            var info = UiKit.CreateText("資訊", bar.transform,
                $"生命 {run.Hp}/{run.MaxHp}   金幣 {run.Gold}   樓層 {run.Floor}   遺物 {run.Relics.Count}",
                26f, null, TextAlignmentOptions.Left);
            UiKit.Place(info.rectTransform, new Vector2(30f, 0f), new Vector2(900f, 50f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f));

            UiKit.Place((RectTransform)UiKit.CreateButton("牌組", bar.transform, $"牌組 {run.Deck.Count}", 24f,
                new Color(0.25f, 0.25f, 0.32f), () => game.OpenDeckView()).transform,
                new Vector2(-110f, 0f), new Vector2(160f, 48f), new Vector2(1f, 0.5f));
        }

        private static string 節點字(MapNodeType type)
        {
            switch (type)
            {
                case MapNodeType.Combat: return "戰";
                case MapNodeType.Elite: return "精";
                case MapNodeType.Rest: return "火";
                case MapNodeType.Shop: return "店";
                case MapNodeType.Treasure: return "寶";
                case MapNodeType.Boss: return "王";
                default: return "?";
            }
        }

        private static Color 節點顏色(MapNodeType type, bool reachable, bool current)
        {
            Color baseColor;
            switch (type)
            {
                case MapNodeType.Elite: baseColor = new Color(0.6f, 0.2f, 0.2f); break;
                case MapNodeType.Rest: baseColor = new Color(0.75f, 0.5f, 0.2f); break;
                case MapNodeType.Shop: baseColor = new Color(0.3f, 0.45f, 0.65f); break;
                case MapNodeType.Treasure: baseColor = new Color(0.7f, 0.6f, 0.2f); break;
                case MapNodeType.Boss: baseColor = new Color(0.55f, 0.15f, 0.4f); break;
                default: baseColor = new Color(0.4f, 0.35f, 0.32f); break;
            }
            if (current) return Color.Lerp(baseColor, new Color(1f, 0.9f, 0.4f), 0.45f);
            if (!reachable) return Color.Lerp(baseColor, Color.black, 0.45f);
            return baseColor;
        }
    }
}
