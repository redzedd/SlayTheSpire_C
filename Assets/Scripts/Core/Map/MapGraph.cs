using System.Collections.Generic;

namespace STS.Core.Map
{
    public enum MapNodeType
    {
        Combat,
        Elite,
        Rest,
        Shop,
        Treasure,
        Boss
    }

    public sealed class MapNode
    {
        public int Id;
        public int Row;
        public int Col;
        public MapNodeType Type;
    }

    public readonly struct MapEdge
    {
        public readonly int From;
        public readonly int To;

        public MapEdge(int from, int to)
        {
            From = from;
            To = to;
        }
    }

    /// <summary>
    /// 爬塔地圖:由下而上的節點圖。Boss 是最頂端的虛擬節點,最後一列全部連向它。
    /// 純資料聚合;生成邏輯在 MapGenerator,查詢輔助放這裡。
    /// </summary>
    public sealed class MapGraph
    {
        public readonly List<MapNode> Nodes = new List<MapNode>();
        public readonly List<MapEdge> Edges = new List<MapEdge>();
        public int BossNodeId = -1;

        public MapNode NodeById(int id)
        {
            for (int i = 0; i < Nodes.Count; i++)
            {
                if (Nodes[i].Id == id) return Nodes[i];
            }
            throw new System.InvalidOperationException($"地圖上沒有節點 id {id}");
        }

        public List<int> NodeIdsAtRow(int row)
        {
            var result = new List<int>();
            for (int i = 0; i < Nodes.Count; i++)
            {
                if (Nodes[i].Row == row) result.Add(Nodes[i].Id);
            }
            return result;
        }

        public List<int> NextNodeIds(int nodeId)
        {
            var result = new List<int>();
            for (int i = 0; i < Edges.Count; i++)
            {
                if (Edges[i].From == nodeId && !result.Contains(Edges[i].To)) result.Add(Edges[i].To);
            }
            return result;
        }
    }
}
