using K1.VectorDB.Engine;

namespace K1.VectorDB.Graph.Services;

internal sealed class GraphNodeManager : IGraphNodeManager
{
    private readonly GraphStore _store;
    private readonly VectorDb _vectorDb;
    private readonly IGraphEdgeManager _edgeManager;

    internal GraphNodeManager(GraphStore store, VectorDb vectorDb, IGraphEdgeManager edgeManager)
    {
        _store = store;
        _vectorDb = vectorDb;
        _edgeManager = edgeManager;
    }

    public IReadOnlyList<GraphNode> AllNodes => _store.Nodes.Values.ToList().AsReadOnly();

    public GraphNode AddNode(string layerId, string label, string content,
        Dictionary<string, string>? metadata = null)
    {
        if (_store.Layers.All(l => l.Id != layerId))
            throw new ArgumentException($"Layer '{layerId}' does not exist.", nameof(layerId));

        EnforceContentUniqueness(layerId, content, excludeNodeId: null);

        var node = new GraphNode
        {
            Label = label,
            Content = content,
            LayerIds = [layerId],
            Metadata = metadata ?? []
        };

        _store.Nodes[node.Id] = node;
        _store.EnsureAdjacency(node.Id);
        _vectorDb.IndexDocument(layerId, content, node.Id);
        return node;
    }

    public bool PlaceNodeInLayer(string nodeId, string layerId)
    {
        if (!_store.Nodes.TryGetValue(nodeId, out var node))
            throw new ArgumentException($"Node '{nodeId}' does not exist.", nameof(nodeId));

        if (_store.Layers.All(l => l.Id != layerId))
            throw new ArgumentException($"Layer '{layerId}' does not exist.", nameof(layerId));

        if (node.LayerIds.Contains(layerId)) return false;

        EnforceContentUniqueness(layerId, node.Content, excludeNodeId: nodeId);

        node.LayerIds.Add(layerId);
        _vectorDb.IndexDocument(layerId, node.Content, node.Id);
        return true;
    }

    public bool RemoveNode(string nodeId)
    {
        if (!_store.Nodes.ContainsKey(nodeId)) return false;

        // Use adjacency lists to find incident edges in O(degree), not O(E)
        var edgeIds = (_store.OutEdges.TryGetValue(nodeId, out var outIds) ? outIds : [])
            .Concat(_store.InEdges.TryGetValue(nodeId, out var inIds) ? inIds : [])
            .Distinct()
            .ToList(); // snapshot before adjacency is modified

        foreach (var edgeId in edgeIds)
            _edgeManager.RemoveEdge(edgeId);

        _vectorDb.DeleteDocument(nodeId);
        _store.Nodes.Remove(nodeId);
        _store.OutEdges.Remove(nodeId);
        _store.InEdges.Remove(nodeId);
        return true;
    }

    public GraphNode? GetNode(string nodeId) =>
        _store.Nodes.GetValueOrDefault(nodeId);

    public IReadOnlyList<GraphNode> GetNodesInLayer(string layerId) =>
        _store.Nodes.Values.Where(n => n.LayerIds.Contains(layerId)).ToList().AsReadOnly();

    private void EnforceContentUniqueness(string layerId, string content, string? excludeNodeId)
    {
        var duplicate = _store.Nodes.Values.FirstOrDefault(n =>
            n.Id != excludeNodeId &&
            n.LayerIds.Contains(layerId) &&
            n.Content == content);

        if (duplicate != null)
            throw new InvalidOperationException(
                $"A node with the same content already exists in layer '{layerId}'.");
    }
}
