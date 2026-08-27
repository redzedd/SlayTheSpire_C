using System.Collections.Generic;
using NUnit.Framework;
using STS.Core.Content;
using STS.Core.Map;
using STS.Core.Rng;

namespace STS.Core.Tests
{
    /// <summary>地圖生成的結構鐵律:決定性、連通、不交叉、固定列與出現限制。</summary>
    public class MapGeneratorTests
    {
        private static BalanceDef 平衡() => new BalanceDef();   // 預設值即切片參數

        private static MapGraph 生圖(ulong seed)
        {
            return MapGenerator.Generate(平衡(), new RngStream(seed));
        }

        [Test]
        public void 同種子_同圖()
        {
            var a = 生圖(42UL);
            var b = 生圖(42UL);
            Assert.AreEqual(a.Nodes.Count, b.Nodes.Count);
            Assert.AreEqual(a.Edges.Count, b.Edges.Count);
            for (int i = 0; i < a.Nodes.Count; i++)
            {
                Assert.AreEqual(a.Nodes[i].Row, b.Nodes[i].Row);
                Assert.AreEqual(a.Nodes[i].Col, b.Nodes[i].Col);
                Assert.AreEqual(a.Nodes[i].Type, b.Nodes[i].Type);
            }
            for (int i = 0; i < a.Edges.Count; i++)
            {
                Assert.AreEqual(a.Edges[i].From, b.Edges[i].From);
                Assert.AreEqual(a.Edges[i].To, b.Edges[i].To);
            }
        }

        [Test]
        public void 邊_只往上一列且橫移不超過一欄()
        {
            var map = 生圖(7UL);
            foreach (var edge in map.Edges)
            {
                var from = map.NodeById(edge.From);
                var to = map.NodeById(edge.To);
                if (to.Id == map.BossNodeId) continue;   // Boss 虛擬節點不受欄限制
                Assert.AreEqual(from.Row + 1, to.Row, "邊必須往上一列");
                Assert.LessOrEqual(System.Math.Abs(to.Col - from.Col), 1, "橫移不得超過一欄");
            }
        }

        [Test]
        public void 全節點_從起點列可達()
        {
            var map = 生圖(99UL);
            var reachable = new HashSet<int>();
            var queue = new Queue<int>();
            foreach (int id in map.NodeIdsAtRow(0))
            {
                reachable.Add(id);
                queue.Enqueue(id);
            }
            while (queue.Count > 0)
            {
                foreach (int next in map.NextNodeIds(queue.Dequeue()))
                {
                    if (reachable.Add(next)) queue.Enqueue(next);
                }
            }
            Assert.AreEqual(map.Nodes.Count, reachable.Count, "所有節點(含 Boss)都必須可達");
        }

        [Test]
        public void 路徑_不交叉()
        {
            var map = 生圖(1234UL);
            // 依起始列分組列轉換,兩兩比對
            var byRow = new Dictionary<int, List<(int fromCol, int toCol)>>();
            foreach (var edge in map.Edges)
            {
                var from = map.NodeById(edge.From);
                var to = map.NodeById(edge.To);
                if (to.Id == map.BossNodeId) continue;
                if (!byRow.TryGetValue(from.Row, out var list))
                {
                    list = new List<(int, int)>();
                    byRow[from.Row] = list;
                }
                list.Add((from.Col, to.Col));
            }
            foreach (var pair in byRow)
            {
                var list = pair.Value;
                for (int i = 0; i < list.Count; i++)
                {
                    for (int j = i + 1; j < list.Count; j++)
                    {
                        bool cross = (list[i].fromCol < list[j].fromCol && list[i].toCol > list[j].toCol)
                            || (list[i].fromCol > list[j].fromCol && list[i].toCol < list[j].toCol);
                        Assert.IsFalse(cross, $"列 {pair.Key} 出現交叉:{list[i]} vs {list[j]}");
                    }
                }
            }
        }

        [Test]
        public void 固定列_型別正確()
        {
            var map = 生圖(555UL);
            var balance = 平衡();
            foreach (var node in map.Nodes)
            {
                if (node.Id == map.BossNodeId)
                {
                    Assert.AreEqual(MapNodeType.Boss, node.Type);
                }
                else if (node.Row == 0)
                {
                    Assert.AreEqual(MapNodeType.Combat, node.Type, "第 0 列必為戰鬥");
                }
                else if (node.Row == balance.MapRows / 2)
                {
                    Assert.AreEqual(MapNodeType.Treasure, node.Type, "中段列必為寶箱");
                }
                else if (node.Row == balance.MapRows - 1)
                {
                    Assert.AreEqual(MapNodeType.Rest, node.Type, "Boss 前一列必為燈火");
                }
            }
        }

        [Test]
        public void 限制_精英燈火不早於下限列()
        {
            var balance = 平衡();
            for (ulong seed = 1; seed <= 20; seed++)
            {
                var map = 生圖(seed);
                foreach (var node in map.Nodes)
                {
                    if (node.Id == map.BossNodeId || node.Row == balance.MapRows - 1) continue;
                    if (node.Type == MapNodeType.Elite)
                    {
                        Assert.GreaterOrEqual(node.Row, balance.MapMinRowForElite, $"種子 {seed}:精英過早出現於列 {node.Row}");
                    }
                    if (node.Type == MapNodeType.Rest)
                    {
                        Assert.GreaterOrEqual(node.Row, balance.MapMinRowForRest, $"種子 {seed}:燈火過早出現於列 {node.Row}");
                        Assert.AreNotEqual(balance.MapNoRestRow, node.Row, $"種子 {seed}:第 {balance.MapNoRestRow} 列不得有燈火");
                    }
                }
            }
        }

        [Test]
        public void Boss_由最後一列全部連入()
        {
            var map = 生圖(2026UL);
            var balance = 平衡();
            foreach (int id in map.NodeIdsAtRow(balance.MapRows - 1))
            {
                Assert.Contains(map.BossNodeId, map.NextNodeIds(id), $"最後一列節點 {id} 未連向 Boss");
            }
        }

        [Test]
        public void 起點列_至少兩個選擇()
        {
            for (ulong seed = 1; seed <= 20; seed++)
            {
                Assert.GreaterOrEqual(生圖(seed).NodeIdsAtRow(0).Count, 2, $"種子 {seed} 起點列只有一個節點");
            }
        }
    }
}
