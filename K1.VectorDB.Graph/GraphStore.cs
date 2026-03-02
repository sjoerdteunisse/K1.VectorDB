namespace K1.VectorDB.Graph;

/// <summary>
/// Shared in-memory state for the graph. All services read and write through this store.
/// Dictionary-keyed lookups give O(1) access to nodes and edges by ID.
/// </summary>
internal sealed class GraphStore
{
    internal readonly List<GraphLayer> Layers = [];
    internal readonly Dictionary<string, GraphNode> Nodes = [];
    internal readonly Dictionary<string, GraphEdge> Edges = [];

    // Adjacency index: nodeId -> edge IDs going out (or either direction for undirected)
    internal readonly Dictionary<string, List<string>> OutEdges = [];
    // Adjacency index: nodeId -> edge IDs coming in (or either direction for undirected)
    internal readonly Dictionary<string, List<string>> InEdges = [];

    internal void EnsureAdjacency(string nodeId)
    {
        OutEdges.TryAdd(nodeId, []);
        InEdges.TryAdd(nodeId, []);
    }

    internal void RegisterEdge(GraphEdge edge)
    {
        EnsureAdjacency(edge.SourceNodeId);
        EnsureAdjacency(edge.TargetNodeId);

        OutEdges[edge.SourceNodeId].Add(edge.Id);
        InEdges[edge.TargetNodeId].Add(edge.Id);

        if (!edge.IsDirected)
        {
            OutEdges[edge.TargetNodeId].Add(edge.Id);
            InEdges[edge.SourceNodeId].Add(edge.Id);
        }
    }

    internal void UnregisterEdge(GraphEdge edge)
    {
        if (OutEdges.TryGetValue(edge.SourceNodeId, out var srcOut)) srcOut.Remove(edge.Id);
        if (InEdges.TryGetValue(edge.TargetNodeId, out var tgtIn)) tgtIn.Remove(edge.Id);

        if (!edge.IsDirected)
        {
            if (OutEdges.TryGetValue(edge.TargetNodeId, out var tgtOut)) tgtOut.Remove(edge.Id);
            if (InEdges.TryGetValue(edge.SourceNodeId, out var srcIn)) srcIn.Remove(edge.Id);
        }
    }
}
