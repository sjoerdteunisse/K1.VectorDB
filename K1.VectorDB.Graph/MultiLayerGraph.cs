using K1.VectorDB.Engine;
using K1.VectorDB.Engine.EmbeddingProviders;
using K1.VectorDB.Graph.Persistence;
using MessagePack;

namespace K1.VectorDB.Graph;

public class MultiLayerGraph
{
    private readonly VectorDb _vectorDb;
    private readonly string _path;

    private readonly List<GraphLayer> _layers = [];
    private readonly List<GraphNode> _nodes = [];
    private readonly List<GraphEdge> _edges = [];

    // adjacency: nodeId -> list of edgeIds (out + undirected)
    private readonly Dictionary<string, List<string>> _outEdges = [];
    // adjacency: nodeId -> list of edgeIds for inbound directed + undirected
    private readonly Dictionary<string, List<string>> _inEdges = [];

    private readonly MessagePackSerializerOptions _mpOptions = MessagePackSerializerOptions.Standard
        .WithSecurity(MessagePackSecurity.UntrustedData)
        .WithCompression(MessagePackCompression.Lz4BlockArray);

    public MultiLayerGraph(IEmbedder embedder, string path)
    {
        _path = path;
        _vectorDb = new VectorDb(embedder, Path.Combine(path, "vectors"), autoIndexCount: 0);
    }

    // ── Layers ────────────────────────────────────────────────────────────────

    public IReadOnlyList<GraphLayer> Layers => _layers.AsReadOnly();

    public GraphLayer AddLayer(string name, string description = "")
    {
        var layer = new GraphLayer { Name = name, Description = description };
        _layers.Add(layer);
        _vectorDb.CreateIndex(layer.Id);
        return layer;
    }

    public bool RemoveLayer(string layerId)
    {
        var layer = _layers.FirstOrDefault(l => l.Id == layerId);
        if (layer == null) return false;

        // Cascade: remove all nodes exclusively in this layer (or detach)
        var nodesToRemove = _nodes
            .Where(n => n.LayerIds.Contains(layerId) && n.LayerIds.Count == 1)
            .Select(n => n.Id)
            .ToList();

        foreach (var nodeId in nodesToRemove)
            RemoveNode(nodeId);

        // Detach nodes that are in multiple layers
        foreach (var node in _nodes.Where(n => n.LayerIds.Contains(layerId)))
            node.LayerIds.Remove(layerId);

        _vectorDb.DeleteIndex(layerId);
        _layers.Remove(layer);
        return true;
    }

    public GraphLayer? GetLayer(string layerId) =>
        _layers.FirstOrDefault(l => l.Id == layerId);

    // ── Nodes ─────────────────────────────────────────────────────────────────

    public IReadOnlyList<GraphNode> AllNodes => _nodes.AsReadOnly();

    public GraphNode AddNode(string layerId, string label, string content,
        Dictionary<string, string>? metadata = null)
    {
        if (_layers.All(l => l.Id != layerId))
            throw new ArgumentException($"Layer '{layerId}' does not exist.", nameof(layerId));

        EnforceContentUniquenessInLayer(layerId, content, excludeNodeId: null);

        var node = new GraphNode
        {
            Label = label,
            Content = content,
            LayerIds = [layerId],
            Metadata = metadata ?? []
        };

        _nodes.Add(node);
        _outEdges[node.Id] = [];
        _inEdges[node.Id] = [];

        _vectorDb.IndexDocument(layerId, content, node.Id);
        return node;
    }

    public bool PlaceNodeInLayer(string nodeId, string layerId)
    {
        var node = _nodes.FirstOrDefault(n => n.Id == nodeId)
            ?? throw new ArgumentException($"Node '{nodeId}' does not exist.", nameof(nodeId));

        if (_layers.All(l => l.Id != layerId))
            throw new ArgumentException($"Layer '{layerId}' does not exist.", nameof(layerId));

        if (node.LayerIds.Contains(layerId)) return false;

        EnforceContentUniquenessInLayer(layerId, node.Content, excludeNodeId: nodeId);

        node.LayerIds.Add(layerId);
        _vectorDb.IndexDocument(layerId, node.Content, node.Id);
        return true;
    }

    public bool RemoveNode(string nodeId)
    {
        var node = _nodes.FirstOrDefault(n => n.Id == nodeId);
        if (node == null) return false;

        // Remove all edges touching this node
        var edgesToRemove = _edges
            .Where(e => e.SourceNodeId == nodeId || e.TargetNodeId == nodeId)
            .Select(e => e.Id)
            .ToList();

        foreach (var edgeId in edgesToRemove)
            RemoveEdge(edgeId);

        _vectorDb.DeleteDocument(nodeId);
        _nodes.Remove(node);
        _outEdges.Remove(nodeId);
        _inEdges.Remove(nodeId);
        return true;
    }

    public GraphNode? GetNode(string nodeId) =>
        _nodes.FirstOrDefault(n => n.Id == nodeId);

    public IReadOnlyList<GraphNode> GetNodesInLayer(string layerId) =>
        _nodes.Where(n => n.LayerIds.Contains(layerId)).ToList().AsReadOnly();

    // ── Edges ─────────────────────────────────────────────────────────────────

    public IReadOnlyList<GraphEdge> AllEdges => _edges.AsReadOnly();

    public GraphEdge AddEdge(string sourceNodeId, string targetNodeId, string relation,
        double weight = 1.0, bool directed = true)
    {
        if (_nodes.All(n => n.Id != sourceNodeId))
            throw new ArgumentException($"Source node '{sourceNodeId}' does not exist.", nameof(sourceNodeId));
        if (_nodes.All(n => n.Id != targetNodeId))
            throw new ArgumentException($"Target node '{targetNodeId}' does not exist.", nameof(targetNodeId));

        var edge = new GraphEdge
        {
            SourceNodeId = sourceNodeId,
            TargetNodeId = targetNodeId,
            RelationLabel = relation,
            Weight = weight,
            IsDirected = directed
        };

        _edges.Add(edge);
        RegisterEdgeInAdjacency(edge);
        return edge;
    }

    public bool RemoveEdge(string edgeId)
    {
        var edge = _edges.FirstOrDefault(e => e.Id == edgeId);
        if (edge == null) return false;

        UnregisterEdgeFromAdjacency(edge);
        _edges.Remove(edge);
        return true;
    }

    public GraphEdge? GetEdge(string edgeId) =>
        _edges.FirstOrDefault(e => e.Id == edgeId);

    public IReadOnlyList<GraphEdge> GetOutEdges(string nodeId)
    {
        if (!_outEdges.TryGetValue(nodeId, out var edgeIds)) return [];
        return edgeIds.Select(id => _edges.First(e => e.Id == id)).ToList().AsReadOnly();
    }

    public IReadOnlyList<GraphEdge> GetInEdges(string nodeId)
    {
        if (!_inEdges.TryGetValue(nodeId, out var edgeIds)) return [];
        return edgeIds.Select(id => _edges.First(e => e.Id == id)).ToList().AsReadOnly();
    }

    public IReadOnlyList<GraphEdge> GetIntraLayerEdges(string layerId)
    {
        var layerNodeIds = _nodes
            .Where(n => n.LayerIds.Contains(layerId))
            .Select(n => n.Id)
            .ToHashSet();

        return _edges
            .Where(e => layerNodeIds.Contains(e.SourceNodeId) && layerNodeIds.Contains(e.TargetNodeId))
            .ToList()
            .AsReadOnly();
    }

    public IReadOnlyList<GraphEdge> GetInterLayerEdges()
    {
        // An edge is inter-layer if source and target share no common layer
        return _edges.Where(e =>
        {
            var src = _nodes.FirstOrDefault(n => n.Id == e.SourceNodeId);
            var tgt = _nodes.FirstOrDefault(n => n.Id == e.TargetNodeId);
            if (src == null || tgt == null) return false;
            return !src.LayerIds.Any(lid => tgt.LayerIds.Contains(lid));
        }).ToList().AsReadOnly();
    }

    // ── Search ────────────────────────────────────────────────────────────────

    public IReadOnlyList<GraphSearchResult> SearchNodes(string query, int topK = 5)
    {
        var result = _vectorDb.QueryCosineSimilarity(query, topK);
        return BuildSearchResults(result, topK);
    }

    public IReadOnlyList<GraphSearchResult> SearchNodesInLayer(string layerId, string query, int topK = 5)
    {
        var result = _vectorDb.QueryCosineSimilarity(query, layerId, topK);
        return BuildSearchResults(result, topK);
    }

    // ── Traversal ─────────────────────────────────────────────────────────────

    public IReadOnlyList<GraphNode> GetNeighbors(string nodeId, bool includeInterLayer = true)
    {
        var neighborIds = new HashSet<string>();

        if (_outEdges.TryGetValue(nodeId, out var outEdgeIds))
            foreach (var edgeId in outEdgeIds)
            {
                var edge = _edges.First(e => e.Id == edgeId);
                var otherId = edge.SourceNodeId == nodeId ? edge.TargetNodeId : edge.SourceNodeId;
                neighborIds.Add(otherId);
            }

        if (_inEdges.TryGetValue(nodeId, out var inEdgeIds))
            foreach (var edgeId in inEdgeIds)
            {
                var edge = _edges.First(e => e.Id == edgeId);
                var otherId = edge.SourceNodeId == nodeId ? edge.TargetNodeId : edge.SourceNodeId;
                neighborIds.Add(otherId);
            }

        var node = _nodes.FirstOrDefault(n => n.Id == nodeId);
        if (!includeInterLayer && node != null)
        {
            neighborIds = neighborIds
                .Where(nid =>
                {
                    var neighbor = _nodes.FirstOrDefault(n => n.Id == nid);
                    return neighbor != null && neighbor.LayerIds.Any(lid => node.LayerIds.Contains(lid));
                })
                .ToHashSet();
        }

        return _nodes.Where(n => neighborIds.Contains(n.Id)).ToList().AsReadOnly();
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
            var current = _nodes.FirstOrDefault(n => n.Id == currentId);
            if (current != null && currentId != startNodeId)
                result.Add(current);

            if (depth >= maxDepth) continue;

            foreach (var neighbor in GetNeighbors(currentId))
            {
                if (visited.Add(neighbor.Id))
                    queue.Enqueue((neighbor.Id, depth + 1));
            }
        }

        return result.AsReadOnly();
    }

    // ── Persistence ───────────────────────────────────────────────────────────

    public void Save()
    {
        if (!Directory.Exists(_path)) Directory.CreateDirectory(_path);

        var state = new GraphState
        {
            Layers = [.. _layers],
            Nodes = [.. _nodes],
            Edges = [.. _edges]
        };

        var bytes = MessagePackSerializer.Serialize(state, _mpOptions);
        File.WriteAllBytes(Path.Combine(_path, "graph_state.bin"), bytes);

        _vectorDb.Save();
    }

    public void Load()
    {
        var statePath = Path.Combine(_path, "graph_state.bin");
        if (!File.Exists(statePath)) return;

        var bytes = File.ReadAllBytes(statePath);
        var state = MessagePackSerializer.Deserialize<GraphState>(bytes, _mpOptions);

        _layers.Clear();
        _nodes.Clear();
        _edges.Clear();
        _outEdges.Clear();
        _inEdges.Clear();

        _layers.AddRange(state.Layers);
        _nodes.AddRange(state.Nodes);
        _edges.AddRange(state.Edges);

        foreach (var node in _nodes)
        {
            _outEdges[node.Id] = [];
            _inEdges[node.Id] = [];
        }

        foreach (var edge in _edges)
            RegisterEdgeInAdjacency(edge);

        _vectorDb.Load();
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private void EnforceContentUniquenessInLayer(string layerId, string content, string? excludeNodeId)
    {
        var duplicate = _nodes.FirstOrDefault(n =>
            n.Id != excludeNodeId &&
            n.LayerIds.Contains(layerId) &&
            n.Content == content);

        if (duplicate != null)
            throw new InvalidOperationException(
                $"A node with the same content already exists in layer '{layerId}'.");
    }

    private void RegisterEdgeInAdjacency(GraphEdge edge)
    {
        EnsureAdjacency(edge.SourceNodeId);
        EnsureAdjacency(edge.TargetNodeId);

        _outEdges[edge.SourceNodeId].Add(edge.Id);
        _inEdges[edge.TargetNodeId].Add(edge.Id);

        if (!edge.IsDirected)
        {
            _outEdges[edge.TargetNodeId].Add(edge.Id);
            _inEdges[edge.SourceNodeId].Add(edge.Id);
        }
    }

    private void UnregisterEdgeFromAdjacency(GraphEdge edge)
    {
        if (_outEdges.TryGetValue(edge.SourceNodeId, out var srcOut)) srcOut.Remove(edge.Id);
        if (_inEdges.TryGetValue(edge.TargetNodeId, out var tgtIn)) tgtIn.Remove(edge.Id);

        if (!edge.IsDirected)
        {
            if (_outEdges.TryGetValue(edge.TargetNodeId, out var tgtOut)) tgtOut.Remove(edge.Id);
            if (_inEdges.TryGetValue(edge.SourceNodeId, out var srcIn)) srcIn.Remove(edge.Id);
        }
    }

    private void EnsureAdjacency(string nodeId)
    {
        _outEdges.TryAdd(nodeId, []);
        _inEdges.TryAdd(nodeId, []);
    }

    private List<GraphSearchResult> BuildSearchResults(K1.VectorDB.Engine.VectorDbQueryResult queryResult, int topK)
    {
        var results = new List<GraphSearchResult>();
        var seen = new HashSet<string>();

        for (var i = 0; i < queryResult.Documents.Count; i++)
        {
            var doc = queryResult.Documents[i];
            var node = _nodes.FirstOrDefault(n => n.Id == doc.Id);
            if (node == null || !seen.Add(node.Id)) continue;
            results.Add(new GraphSearchResult { Node = node, Score = queryResult.Distances[i] });
            if (results.Count >= topK) break;
        }

        return results;
    }
}
