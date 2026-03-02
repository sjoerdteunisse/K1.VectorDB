using K1.VectorDB.Engine;

namespace K1.VectorDB.Graph.Services;

internal sealed class GraphSearchService : IGraphSearch
{
    private readonly GraphStore _store;
    private readonly VectorDb _vectorDb;

    internal GraphSearchService(GraphStore store, VectorDb vectorDb)
    {
        _store = store;
        _vectorDb = vectorDb;
    }

    public IReadOnlyList<GraphSearchResult> SearchNodes(string query, int topK = 5)
    {
        var result = _vectorDb.QueryCosineSimilarity(query, topK);
        return BuildResults(result, topK);
    }

    public IReadOnlyList<GraphSearchResult> SearchNodesInLayer(string layerId, string query, int topK = 5)
    {
        var result = _vectorDb.QueryCosineSimilarity(query, layerId, topK);
        return BuildResults(result, topK);
    }

    private List<GraphSearchResult> BuildResults(VectorDbQueryResult queryResult, int topK)
    {
        var results = new List<GraphSearchResult>();
        var seen = new HashSet<string>();

        for (var i = 0; i < queryResult.Documents.Count; i++)
        {
            var doc = queryResult.Documents[i];
            if (!_store.Nodes.TryGetValue(doc.Id, out var node) || !seen.Add(node.Id)) continue;
            results.Add(new GraphSearchResult { Node = node, Score = queryResult.Distances[i] });
            if (results.Count >= topK) break;
        }

        return results;
    }
}
