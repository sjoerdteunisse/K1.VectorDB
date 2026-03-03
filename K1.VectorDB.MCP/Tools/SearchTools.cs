using K1.VectorDB.MCP;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;

namespace K1.VectorDB.MCP.Tools;

[McpServerToolType]
public sealed class SearchTools(GraphSessionService session)
{
    private static readonly JsonSerializerOptions _json = new() { WriteIndented = true };

    // ── search ────────────────────────────────────────────────────────────────

    [McpServerTool, Description(
        "Semantic vector search across the graph. The query is embedded and compared " +
        "against all stored node content vectors using cosine similarity. Optionally " +
        "scope the search to a single abstraction layer by supplying layerName. " +
        "Returns the top-K most similar nodes with their similarity scores.")]
    public string Search(
        [Description("Natural-language search query (e.g. 'authentication service', 'database schema for users')")] string query,
        [Description("Maximum number of results to return")] int topK = 5,
        [Description("Optional layer to restrict the search to: PURPOSE | CONTEXT | CONTAINER | COMPONENT | FLOW | DATA | STATE | DEPLOY | CLASS")] string? layerName = null)
    {
        topK = Math.Clamp(topK, 1, 50);

        var results = layerName is not null
            ? session.Graph.SearchNodesInLayer(session.ResolveLayerId(layerName), query, topK)
            : session.Graph.SearchNodes(query, topK);

        var hits = results.Select(r =>
        {
            var layerNames = r.Node.LayerIds
                .Select(id => session.Graph.GetLayer(id)?.Name ?? id)
                .ToList();
            return new
            {
                nodeId   = r.Node.Id,
                label    = r.Node.Label,
                content  = r.Node.Content,
                score    = r.Score,
                layers   = layerNames,
                metadata = r.Node.Metadata
            };
        });

        return JsonSerializer.Serialize(new
        {
            query,
            layerName = layerName ?? "all",
            topK,
            resultCount = results.Count,
            results     = hits
        }, _json);
    }
}
