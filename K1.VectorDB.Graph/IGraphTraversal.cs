namespace K1.VectorDB.Graph;

public interface IGraphTraversal
{
    IReadOnlyList<GraphNode> GetNeighbors(string nodeId, bool includeInterLayer = true);
    IReadOnlyList<GraphNode> BreadthFirstSearch(string startNodeId, int maxDepth = 3);
}
