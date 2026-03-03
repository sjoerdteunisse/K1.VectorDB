using K1.VectorDB.MCP;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;

namespace K1.VectorDB.MCP.Tools;

[McpServerToolType]
public sealed class NodeTools(GraphSessionService session)
{
    private static readonly JsonSerializerOptions _json = new() { WriteIndented = true };

    // ── add_node ──────────────────────────────────────────────────────────────

    [McpServerTool, Description(
        "Add a node to a named abstraction layer. The content string is semantically " +
        "indexed (embedded as a vector) so it can be retrieved by search later. " +
        "Use the correct layer name for the type of knowledge: PURPOSE for goals, " +
        "CONTEXT for external systems, CONTAINER for deployable units, COMPONENT for " +
        "internals, FLOW for runtime interactions, DATA for schemas, STATE for lifecycles, " +
        "DEPLOY for infrastructure, CLASS for code-level types. Returns the new node ID.")]
    public string AddNode(
        [Description("Abstraction layer name: PURPOSE | CONTEXT | CONTAINER | COMPONENT | FLOW | DATA | STATE | DEPLOY | CLASS")] string layerName,
        [Description("Short human-readable label for this node (e.g. 'AuthService', 'User entity')")] string label,
        [Description("Full descriptive content that will be semantically indexed for vector search")] string content,
        [Description("Optional JSON object with extra key-value metadata (e.g. '{\"file\":\"src/auth.cs\",\"line\":\"42\"}')")] string? metadataJson = null)
    {
        var layerId  = session.ResolveLayerId(layerName);
        var metadata = ParseMetadata(metadataJson);

        var node = session.Graph.AddNode(layerId, label, content, metadata);

        return JsonSerializer.Serialize(new
        {
            nodeId    = node.Id,
            label     = node.Label,
            layerName,
            layerId
        }, _json);
    }

    // ── get_node ──────────────────────────────────────────────────────────────

    [McpServerTool, Description(
        "Retrieve a single node by its ID. Returns label, content, layer memberships, and metadata.")]
    public string GetNode(
        [Description("The node ID returned by add_node")] string nodeId)
    {
        var node = session.Graph.GetNode(nodeId)
            ?? throw new ArgumentException($"Node '{nodeId}' not found.");

        var layerNames = node.LayerIds
            .Select(id => session.Graph.GetLayer(id)?.Name ?? id)
            .ToList();

        return JsonSerializer.Serialize(new
        {
            nodeId    = node.Id,
            label     = node.Label,
            content   = node.Content,
            layers    = layerNames,
            metadata  = node.Metadata
        }, _json);
    }

    // ── get_nodes_in_layer ────────────────────────────────────────────────────

    [McpServerTool, Description(
        "List all nodes that belong to a named abstraction layer.")]
    public string GetNodesInLayer(
        [Description("Abstraction layer name: PURPOSE | CONTEXT | CONTAINER | COMPONENT | FLOW | DATA | STATE | DEPLOY | CLASS")] string layerName)
    {
        var layerId = session.ResolveLayerId(layerName);
        var nodes   = session.Graph.GetNodesInLayer(layerId);

        var result = nodes.Select(n => new
        {
            nodeId   = n.Id,
            label    = n.Label,
            content  = n.Content,
            metadata = n.Metadata
        });

        return JsonSerializer.Serialize(new
        {
            layerName,
            nodeCount = nodes.Count,
            nodes     = result
        }, _json);
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private static Dictionary<string, string>? ParseMetadata(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json);
        }
        catch
        {
            return new Dictionary<string, string> { ["raw"] = json };
        }
    }
}
