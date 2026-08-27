using System.Collections.Generic;
using STS.Core.Content;
using STS.Core.Rng;

namespace STS.Core.Map
{
    /// <summary>
    /// 爬塔地圖生成([近似] 原作風格重建,結構參數全在 BalanceDef):
    /// 網格 Columns×Rows,生成 PathCount 條由下而上的路徑(每列走 ±1 欄、同格合併、禁止交叉),
    /// 固定列:第 0 列戰鬥、中段寶箱列、最後一列燈火;其餘列按權重擲型別,受最早出現列等限制。
    /// 只消耗 Map 亂數流,同種子同圖(測試鎖定)。
    /// </summary>
    public static class MapGenerator
    {
        public static MapGraph Generate(BalanceDef balance, RngStream mapRng)
        {
            var graph = new MapGraph();
            int cols = balance.MapColumns;
            int rows = balance.MapRows;
            var nodeByCell = new Dictionary<(int row, int col), MapNode>();
            var edgeSet = new HashSet<(int, int)>();
            // 每個列轉換記錄 (fromCol → toCol),防交叉判定用
            var transitions = new List<(int fromCol, int toCol)>[rows - 1];
            for (int i = 0; i < rows - 1; i++) transitions[i] = new List<(int, int)>();
            int nextId = 0;

            MapNode GetOrCreate(int row, int col)
            {
                if (nodeByCell.TryGetValue((row, col), out var existing)) return existing;
                var node = new MapNode { Id = nextId++, Row = row, Col = col, Type = MapNodeType.Combat };
                nodeByCell[(row, col)] = node;
                graph.Nodes.Add(node);
                return node;
            }

            // ---- 走出 PathCount 條路徑 ----
            var usedStartCols = new HashSet<int>();
            for (int path = 0; path < balance.MapPathCount; path++)
            {
                int col = mapRng.NextInt(cols);
                // 前兩條路徑保證不同起點欄,確保第 0 列至少兩個選擇
                if (path < 2)
                {
                    int guard = 0;
                    while (usedStartCols.Contains(col) && guard++ < 32)
                    {
                        col = mapRng.NextInt(cols);
                    }
                }
                usedStartCols.Add(col);

                var current = GetOrCreate(0, col);
                for (int row = 0; row < rows - 1; row++)
                {
                    int nextCol = Clamp(col + mapRng.Range(-1, 1), 0, cols - 1);
                    // 防交叉:與本列既有邊交叉時,把目的地併到既有邊的目的地(共用節點=合法合流)
                    bool adjusted = true;
                    int guard = 0;
                    while (adjusted && guard++ < cols)
                    {
                        adjusted = false;
                        foreach (var (fromCol, toCol) in transitions[row])
                        {
                            if ((col < fromCol && nextCol > toCol) || (col > fromCol && nextCol < toCol))
                            {
                                nextCol = toCol;
                                adjusted = true;
                            }
                        }
                    }

                    var next = GetOrCreate(row + 1, nextCol);
                    if (edgeSet.Add((current.Id, next.Id)))
                    {
                        graph.Edges.Add(new MapEdge(current.Id, next.Id));
                        transitions[row].Add((col, nextCol));
                    }
                    current = next;
                    col = nextCol;
                }
            }

            // ---- Boss 虛擬節點:最後一列全部連向它 ----
            var boss = new MapNode { Id = nextId++, Row = rows, Col = cols / 2, Type = MapNodeType.Boss };
            graph.Nodes.Add(boss);
            graph.BossNodeId = boss.Id;
            foreach (var node in graph.Nodes)
            {
                if (node.Row == rows - 1)
                {
                    graph.Edges.Add(new MapEdge(node.Id, boss.Id));
                }
            }

            AssignTypes(graph, balance, mapRng, rows);
            return graph;
        }

        private static void AssignTypes(MapGraph graph, BalanceDef balance, RngStream mapRng, int rows)
        {
            int treasureRow = rows / 2;   // 中段整列寶箱
            foreach (var node in graph.Nodes)
            {
                if (node.Type == MapNodeType.Boss) continue;
                if (node.Row == 0)
                {
                    node.Type = MapNodeType.Combat;
                }
                else if (node.Row == treasureRow)
                {
                    node.Type = MapNodeType.Treasure;
                }
                else if (node.Row == rows - 1)
                {
                    node.Type = MapNodeType.Rest;   // Boss 前整列燈火
                }
                else
                {
                    node.Type = RollType(balance, mapRng, node.Row);
                }
            }

            // 沿路徑不連續兩個同型特殊房([近似] 簡化版:子節點退回戰鬥)
            foreach (var edge in graph.Edges)
            {
                var from = graph.NodeById(edge.From);
                var to = graph.NodeById(edge.To);
                if (from.Type == to.Type && IsSpecial(to.Type) && to.Row != rows - 1 && to.Row != rows / 2)
                {
                    to.Type = MapNodeType.Combat;
                }
            }
        }

        private static MapNodeType RollType(BalanceDef balance, RngStream mapRng, int row)
        {
            int total = balance.MapCombatWeight + balance.MapEliteWeight + balance.MapRestWeight
                + balance.MapShopWeight + balance.MapTreasureWeight;
            int roll = mapRng.NextInt(total <= 0 ? 1 : total);

            MapNodeType picked;
            if ((roll -= balance.MapCombatWeight) < 0) picked = MapNodeType.Combat;
            else if ((roll -= balance.MapEliteWeight) < 0) picked = MapNodeType.Elite;
            else if ((roll -= balance.MapRestWeight) < 0) picked = MapNodeType.Rest;
            else if ((roll -= balance.MapShopWeight) < 0) picked = MapNodeType.Shop;
            else picked = MapNodeType.Treasure;

            // 出現列限制:太早的精英/燈火退回戰鬥;倒數第二列不放燈火(下一列整列都是)
            if (picked == MapNodeType.Elite && row < balance.MapMinRowForElite) picked = MapNodeType.Combat;
            if (picked == MapNodeType.Rest && (row < balance.MapMinRowForRest || row == balance.MapNoRestRow)) picked = MapNodeType.Combat;
            return picked;
        }

        private static bool IsSpecial(MapNodeType type)
        {
            return type == MapNodeType.Elite || type == MapNodeType.Rest || type == MapNodeType.Shop;
        }

        private static int Clamp(int value, int min, int max)
        {
            return value < min ? min : (value > max ? max : value);
        }
    }
}
