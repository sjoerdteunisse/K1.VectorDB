namespace K1.VectorDB.Graph.Services;

internal sealed class GraphEdgeManager : IGraphEdgeManager
{
    private readonly GraphStore _store;

    internal GraphEdgeManager(GraphStore store)
    {
        _store = store;
    }

    public IReadOnlyList<GraphEdge> AllEdges => _store.Edges.Values.ToList().AsReadOnly();

    public GraphEdge AddEdge(string sourceNodeId, string targetNodeId, string relation,
        double weight = 1.0, bool directed = true)
    {
        if (!_store.Nodes.ContainsKey(sourceNodeId))
            throw new ArgumentException($"Source node '{sourceNodeId}' does not exist.", nameof(sourceNodeId));
        if (!_store.Nodes.ContainsKey(targetNodeId))
            throw new ArgumentException($"Target node '{targetNodeId}' does not exist.", nameof(targetNodeId));

        var edge = new GraphEdge
        {
            SourceNodeId = sourceNodeId,
            TargetNodeId = targetNodeId,
            RelationLabel = relation,
            Weight = weight,
            IsDirected = directed
        };

        _store.Edges[edge.Id] = edge;
        _store.RegisterEdge(edge);
        return edge;
    }

    public bool RemoveEdge(string edgeId)
    {
        if (!_store.Edges.TryGetValue(edgeId, out var edge)) return false;

        _store.UnregisterEdge(edge);
        _store.Edges.Remove(edgeId);
        return true;
    }

    public GraphEdge? GetEdge(string edgeId) =>
        _store.Edges.GetValueOrDefault(edgeId);

    public IReadOnlyList<GraphEdge> GetOutEdges(string nodeId)
    {
        if (!_store.OutEdges.TryGetValue(nodeId, out var edgeIds)) return [];
        return edgeIds.Select(id => _store.Edges[id]).ToList().AsReadOnly();
    }

    public IReadOnlyList<GraphEdge> GetInEdges(string nodeId)
    {
        if (!_store.InEdges.TryGetValue(nodeId, out var edgeIds)) return [];
        return edgeIds.Select(id => _store.Edges[id]).ToList().AsReadOnly();
    }

    public IReadOnlyList<GraphEdge> GetIntraLayerEdges(string layerId)
    {
        var layerNodeIds = _store.Nodes.Values
            .Where(n => n.LayerIds.Contains(layerId))
            .Select(n => n.Id)
            .ToHashSet();

        return _store.Edges.Values
            .Where(e => layerNodeIds.Contains(e.SourceNodeId) && layerNodeIds.Contains(e.TargetNodeId))
            .ToList()
            .AsReadOnly();
    }

    public IReadOnlyList<GraphEdge> GetInterLayerEdges()
    {
        return _store.Edges.Values.Where(e =>
        {
            if (!_store.Nodes.TryGetValue(e.SourceNodeId, out var src)) return false;
            if (!_store.Nodes.TryGetValue(e.TargetNodeId, out var tgt)) return false;
            return !src.LayerIds.Any(lid => tgt.LayerIds.Contains(lid));
        }).ToList().AsReadOnly();
    }
}
