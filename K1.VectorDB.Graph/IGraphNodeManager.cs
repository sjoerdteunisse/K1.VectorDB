namespace K1.VectorDB.Graph;

public interface IGraphNodeManager
{
    IReadOnlyList<GraphNode> AllNodes { get; }
    GraphNode AddNode(string layerId, string label, string content, Dictionary<string, string>? metadata = null);
    bool PlaceNodeInLayer(string nodeId, string layerId);
    bool RemoveNode(string nodeId);
    GraphNode? GetNode(string nodeId);
    IReadOnlyList<GraphNode> GetNodesInLayer(string layerId);
}
