namespace K1.VectorDB.Graph;

public interface IGraphSearch
{
    IReadOnlyList<GraphSearchResult> SearchNodes(string query, int topK = 5);
    IReadOnlyList<GraphSearchResult> SearchNodesInLayer(string layerId, string query, int topK = 5);
}
