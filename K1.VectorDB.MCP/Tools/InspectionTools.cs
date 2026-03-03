using K1.VectorDB.MCP;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;

namespace K1.VectorDB.MCP.Tools;

[McpServerToolType]
public sealed class InspectionTools(GraphSessionService session)
{
    private static readonly JsonSerializerOptions _json = new() { WriteIndented = true };

    // ── list_layers ───────────────────────────────────────────────────────────

    [McpServerTool, Description(
        "List all abstraction layers in the current graph, including their IDs, names, " +
        "descriptions, and how many nodes each layer contains. Use this to get an " +
        "overview of what has been stored in the graph so far.")]
    public string ListLayers()
    {
        var graph = session.Graph;

        var layers = graph.Layers.Select(l => new
        {
            id        = l.Id,
            name      = l.Name,
            desc      = l.Description,
            nodeCount = graph.GetNodesInLayer(l.Id).Count
        });

        return JsonSerializer.Serialize(new
        {
            layerCount = graph.Layers.Count,
            totalNodes = graph.AllNodes.Count,
            totalEdges = graph.AllEdges.Count,
            layers
        }, _json);
    }
}
