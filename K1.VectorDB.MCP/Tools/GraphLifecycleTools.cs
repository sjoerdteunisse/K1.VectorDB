using K1.VectorDB.MCP;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;

namespace K1.VectorDB.MCP.Tools;

[McpServerToolType]
public sealed class GraphLifecycleTools(GraphSessionService session)
{
    private static readonly JsonSerializerOptions _json = new() { WriteIndented = true };

    // ── initialize_graph ──────────────────────────────────────────────────────

    [McpServerTool, Description(
        "Create a brand-new graph at the given path and populate it with the 9 standard " +
        "abstraction layers (PURPOSE, CONTEXT, CONTAINER, COMPONENT, FLOW, DATA, STATE, " +
        "DEPLOY, CLASS). Call this once before adding nodes to a fresh analysis session. " +
        "Returns a JSON summary of all created layers.")]
    public string InitializeGraph(
        [Description("Local filesystem path where graph data will be stored (e.g. './my-repo-graph')")] string path,
        [Description("LM Studio embedding API URL")] string lmStudioUrl = "http://localhost:1234/v1/embeddings",
        [Description("Embedding model name loaded in LM Studio")] string model = "text-embedding-qwen3-embedding-0.6b")
    {
        var graph = session.Initialize(path, lmStudioUrl, model);

        var layers = graph.Layers.Select(l => new
        {
            id   = l.Id,
            name = l.Name,
            desc = l.Description
        });

        return JsonSerializer.Serialize(new
        {
            status = "initialized",
            path,
            layers
        }, _json);
    }

    // ── load_graph ────────────────────────────────────────────────────────────

    [McpServerTool, Description(
        "Load an existing graph from disk. Use this at the start of a session to resume " +
        "a previous analysis. Returns a summary of all layers and their node counts.")]
    public string LoadGraph(
        [Description("Local filesystem path where the graph data is stored")] string path,
        [Description("LM Studio embedding API URL")] string lmStudioUrl = "http://localhost:1234/v1/embeddings",
        [Description("Embedding model name loaded in LM Studio")] string model = "text-embedding-qwen3-embedding-0.6b")
    {
        var graph = session.Load(path, lmStudioUrl, model);

        var layers = graph.Layers.Select(l => new
        {
            id        = l.Id,
            name      = l.Name,
            nodeCount = graph.GetNodesInLayer(l.Id).Count
        });

        return JsonSerializer.Serialize(new
        {
            status = "loaded",
            path,
            totalNodes = graph.AllNodes.Count,
            totalEdges = graph.AllEdges.Count,
            layers
        }, _json);
    }

    // ── save_graph ────────────────────────────────────────────────────────────

    [McpServerTool, Description(
        "Persist the current graph state to disk. Call this after a batch of add_node / " +
        "add_relation operations to ensure data is not lost.")]
    public string SaveGraph()
    {
        session.Graph.Save();
        return JsonSerializer.Serialize(new
        {
            status     = "saved",
            totalNodes = session.Graph.AllNodes.Count,
            totalEdges = session.Graph.AllEdges.Count
        }, _json);
    }
}
