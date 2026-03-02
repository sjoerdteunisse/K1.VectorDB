namespace K1.VectorDB.Graph.Services;

internal sealed class GraphTraversalService : IGraphTraversal
{
    private readonly GraphStore _store;

    internal GraphTraversalService(GraphStore store)
    {
        _store = store;
    }

    public IReadOnlyList<GraphNode> GetNeighbors(string nodeId, bool includeInterLayer = true)
    {
        var neighborIds = new HashSet<string>();

        if (_store.OutEdges.TryGetValue(nodeId, out var outEdgeIds))
            foreach (var edgeId in outEdgeIds)
            {
                var edge = _store.Edges[edgeId];
                neighborIds.Add(edge.SourceNodeId == nodeId ? edge.TargetNodeId : edge.SourceNodeId);
            }

        if (_store.InEdges.TryGetValue(nodeId, out var inEdgeIds))
            foreach (var edgeId in inEdgeIds)
            {
                var edge = _store.Edges[edgeId];
                neighborIds.Add(edge.SourceNodeId == nodeId ? edge.TargetNodeId : edge.SourceNodeId);
            }

        if (!includeInterLayer && _store.Nodes.TryGetValue(nodeId, out var node))
        {
            neighborIds = neighborIds
                .Where(nid => _store.Nodes.TryGetValue(nid, out var neighbor)
                    && neighbor.LayerIds.Any(lid => node.LayerIds.Contains(lid)))
                .ToHashSet();
        }

        return neighborIds
            .Select(nid => _store.Nodes.GetValueOrDefault(nid))
            .OfType<GraphNode>()
            .ToList()
            .AsReadOnly();
    }

    public IReadOnlyList<GraphNode> BreadthFirstSearch(string startNodeId, int maxDepth = 3)
    {
        var visited = new HashSet<string> { startNodeId };
        var result = new List<GraphNode>();
        var queue = new Queue<(string nodeId, int depth)>();
        queue.Enqueue((startNodeId, 0));

        while (queue.Count > 0)
        {
            var (currentId, depth) = queue.Dequeue();

            if (currentId != startNodeId && _store.Nodes.TryGetValue(currentId, out var current))
                result.Add(current);

            if (depth >= maxDepth) continue;

            foreach (var neighbor in GetNeighbors(currentId))
                if (visited.Add(neighbor.Id))
                    queue.Enqueue((neighbor.Id, depth + 1));
        }

        return result.AsReadOnly();
    }
}
