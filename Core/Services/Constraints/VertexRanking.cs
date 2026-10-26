using PolygonStudio.Core.Model;

namespace PolygonStudio.Core.Services.Constraints;

internal static class VertexRanking
{
    internal static Dictionary<VertexKey, int> ComputeVertexRanks(Scene scene, SolverState state, VertexKey? seed)
    {
        var ranks = new Dictionary<VertexKey, int>();
        var queue = new Queue<VertexKey>();
        
        if (seed is not null && ranks.TryAdd(seed.Value, 0)) 
            queue.Enqueue(seed.Value);
        
        var adj = new Dictionary<VertexKey, List<VertexKey>>();

        foreach (var polygon in scene.Polygons)
        foreach (var edge in polygon.Edges)
            AddEdge(new VertexKey(polygon.Id, edge.FromVertexId), new VertexKey(polygon.Id, edge.ToVertexId));

        foreach (var pc in state.PerpendicularConstraints)
        {
            AddEdge(pc.From1, pc.From2);
            AddEdge(pc.To1, pc.To2);
        }

        while (queue.Count > 0)
        {
            var curr = queue.Dequeue();
            int nextRank = ranks[curr] + 1;

            if (!adj.TryGetValue(curr, out var neighbors))
                continue;

            foreach (var nei in neighbors)
            {
                if (ranks.TryAdd(nei, nextRank))
                    queue.Enqueue(nei);
            }
        }

        return ranks;

        void AddEdge(VertexKey a, VertexKey b)
        {
            if (!adj.TryGetValue(a, out var listA)) adj[a] = listA = [];
            if (!adj.TryGetValue(b, out var listB)) adj[b] = listB = [];
            listA.Add(b);
            listB.Add(a);
        }
    }

    internal static int GetRank(Dictionary<VertexKey, int> ranks, VertexKey key) =>
        ranks.GetValueOrDefault(key, int.MaxValue);

    internal static (double weightA, double weightB) GetVertexWeights(
        VertexKey keyA, VertexKey keyB,
        Dictionary<VertexKey, int> ranks,
        HashSet<VertexKey> locked)
    {
        bool lockA = locked.Contains(keyA);
        bool lockB = locked.Contains(keyB);

        if (lockA && lockB) return (0.0, 0.0);
        if (lockA) return (0.0, 1.0);
        if (lockB) return (1.0, 0.0);

        int rankA = GetRank(ranks, keyA);
        int rankB = GetRank(ranks, keyB);

        // Closer one to cursor acts as the anchor
        if (rankA < rankB) return (0.15, 0.85);
        if (rankB < rankA) return (0.85, 0.15);

        return (0.5, 0.5);
    }
}