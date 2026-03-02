using K1.VectorDB.Engine;

namespace K1.VectorDB.Graph.Services;

internal sealed class GraphLayerManager : IGraphLayerManager
{
    private readonly GraphStore _store;
    private readonly VectorDb _vectorDb;
    private readonly IGraphNodeManager _nodeManager;

    internal GraphLayerManager(GraphStore store, VectorDb vectorDb, IGraphNodeManager nodeManager)
    {
        _store = store;
        _vectorDb = vectorDb;
        _nodeManager = nodeManager;
    }

    public IReadOnlyList<GraphLayer> Layers => _store.Layers.AsReadOnly();

    public GraphLayer AddLayer(string name, string description = "")
    {
        var layer = new GraphLayer { Name = name, Description = description };
        _store.Layers.Add(layer);
        _vectorDb.CreateIndex(layer.Id);
        return layer;
    }

    public bool RemoveLayer(string layerId)
    {
        var layer = _store.Layers.FirstOrDefault(l => l.Id == layerId);
        if (layer == null) return false;

        // Remove nodes that exist only in this layer
        var exclusiveNodeIds = _store.Nodes.Values
            .Where(n => n.LayerIds.Contains(layerId) && n.LayerIds.Count == 1)
            .Select(n => n.Id)
            .ToList();

        foreach (var nodeId in exclusiveNodeIds)
            _nodeManager.RemoveNode(nodeId);

        // Detach nodes that also belong to other layers
        foreach (var node in _store.Nodes.Values.Where(n => n.LayerIds.Contains(layerId)))
            node.LayerIds.Remove(layerId);

        _vectorDb.DeleteIndex(layerId);
        _store.Layers.Remove(layer);
        return true;
    }

    public GraphLayer? GetLayer(string layerId) =>
        _store.Layers.FirstOrDefault(l => l.Id == layerId);
}
