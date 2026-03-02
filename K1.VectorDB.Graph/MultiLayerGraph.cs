using K1.VectorDB.Engine.EmbeddingProviders;
using K1.VectorDB.Engine;
using K1.VectorDB.Graph.Services;
using MessagePack;

namespace K1.VectorDB.Graph;

/// <summary>
/// Public facade for the multi-layer graph. Composes focused services, each with a single
/// responsibility, and exposes them through their respective interfaces.
/// </summary>
public class MultiLayerGraph : IGraphLayerManager, IGraphNodeManager, IGraphEdgeManager,
    IGraphSearch, IGraphTraversal
{
    private readonly IGraphLayerManager _layerManager;
    private readonly IGraphNodeManager _nodeManager;
    private readonly IGraphEdgeManager _edgeManager;
    private readonly IGraphSearch _search;
    private readonly IGraphTraversal _traversal;
    private readonly GraphPersistenceService _persistence;

    public MultiLayerGraph(IEmbedder embedder, string path)
    {
        var mpOptions = MessagePackSerializerOptions.Standard
            .WithSecurity(MessagePackSecurity.UntrustedData)
            .WithCompression(MessagePackCompression.Lz4BlockArray);

        var store = new GraphStore();
        var vectorDb = new VectorDb(embedder, Path.Combine(path, "vectors"), autoIndexCount: 0);

        var edgeManager = new GraphEdgeManager(store);
        var nodeManager = new GraphNodeManager(store, vectorDb, edgeManager);

        _edgeManager = edgeManager;
        _nodeManager = nodeManager;
        _layerManager = new GraphLayerManager(store, vectorDb, nodeManager);
        _search = new GraphSearchService(store, vectorDb);
        _traversal = new GraphTraversalService(store);
        _persistence = new GraphPersistenceService(store, vectorDb, path, mpOptions);
    }

    // ── Layers ────────────────────────────────────────────────────────────────
    public IReadOnlyList<GraphLayer> Layers => _layerManager.Layers;
    public GraphLayer AddLayer(string name, string description = "") => _layerManager.AddLayer(name, description);
    public bool RemoveLayer(string layerId) => _layerManager.RemoveLayer(layerId);
    public GraphLayer? GetLayer(string layerId) => _layerManager.GetLayer(layerId);

    // ── Nodes ─────────────────────────────────────────────────────────────────
    public IReadOnlyList<GraphNode> AllNodes => _nodeManager.AllNodes;
    public GraphNode AddNode(string layerId, string label, string content, Dictionary<string, string>? metadata = null) =>
        _nodeManager.AddNode(layerId, label, content, metadata);
    public bool PlaceNodeInLayer(string nodeId, string layerId) => _nodeManager.PlaceNodeInLayer(nodeId, layerId);
    public bool RemoveNode(string nodeId) => _nodeManager.RemoveNode(nodeId);
    public GraphNode? GetNode(string nodeId) => _nodeManager.GetNode(nodeId);
    public IReadOnlyList<GraphNode> GetNodesInLayer(string layerId) => _nodeManager.GetNodesInLayer(layerId);

    // ── Edges ─────────────────────────────────────────────────────────────────
    public IReadOnlyList<GraphEdge> AllEdges => _edgeManager.AllEdges;
    public GraphEdge AddEdge(string sourceNodeId, string targetNodeId, string relation, double weight = 1.0, bool directed = true) =>
        _edgeManager.AddEdge(sourceNodeId, targetNodeId, relation, weight, directed);
    public bool RemoveEdge(string edgeId) => _edgeManager.RemoveEdge(edgeId);
    public GraphEdge? GetEdge(string edgeId) => _edgeManager.GetEdge(edgeId);
    public IReadOnlyList<GraphEdge> GetOutEdges(string nodeId) => _edgeManager.GetOutEdges(nodeId);
    public IReadOnlyList<GraphEdge> GetInEdges(string nodeId) => _edgeManager.GetInEdges(nodeId);
    public IReadOnlyList<GraphEdge> GetIntraLayerEdges(string layerId) => _edgeManager.GetIntraLayerEdges(layerId);
    public IReadOnlyList<GraphEdge> GetInterLayerEdges() => _edgeManager.GetInterLayerEdges();

    // ── Search ────────────────────────────────────────────────────────────────
    public IReadOnlyList<GraphSearchResult> SearchNodes(string query, int topK = 5) => _search.SearchNodes(query, topK);
    public IReadOnlyList<GraphSearchResult> SearchNodesInLayer(string layerId, string query, int topK = 5) =>
        _search.SearchNodesInLayer(layerId, query, topK);

    // ── Traversal ─────────────────────────────────────────────────────────────
    public IReadOnlyList<GraphNode> GetNeighbors(string nodeId, bool includeInterLayer = true) =>
        _traversal.GetNeighbors(nodeId, includeInterLayer);
    public IReadOnlyList<GraphNode> BreadthFirstSearch(string startNodeId, int maxDepth = 3) =>
        _traversal.BreadthFirstSearch(startNodeId, maxDepth);

    // ── Persistence ───────────────────────────────────────────────────────────
    public void Save() => _persistence.Save();
    public void Load() => _persistence.Load();
}
