namespace K1.VectorDB.Graph;

public interface IGraphEdgeManager
{
    IReadOnlyList<GraphEdge> AllEdges { get; }
    GraphEdge AddEdge(string sourceNodeId, string targetNodeId, string relation, double weight = 1.0, bool directed = true);
    bool RemoveEdge(string edgeId);
    GraphEdge? GetEdge(string edgeId);
    IReadOnlyList<GraphEdge> GetOutEdges(string nodeId);
    IReadOnlyList<GraphEdge> GetInEdges(string nodeId);
    IReadOnlyList<GraphEdge> GetIntraLayerEdges(string layerId);
    IReadOnlyList<GraphEdge> GetInterLayerEdges();
}
