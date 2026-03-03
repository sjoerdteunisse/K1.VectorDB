using K1.VectorDB.MCP;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;

namespace K1.VectorDB.MCP.Tools;

[McpServerToolType]
public sealed class EdgeTools(GraphSessionService session)
{
    private static readonly JsonSerializerOptions _json = new() { WriteIndented = true };

    // ── add_relation ──────────────────────────────────────────────────────────

    [McpServerTool, Description(
        "Add a typed relationship (edge) between two nodes. Both nodes must already exist. " +
        "Use this to encode structural or runtime relationships discovered during analysis, " +
        "e.g. 'calls', 'depends-on', 'owns', 'publishes', 'subscribes-to', 'implements'. " +
        "Edges can cross abstraction layers (inter-layer) or stay within the same layer " +
        "(intra-layer). Returns the new edge ID.")]
    public string AddRelation(
        [Description("Node ID of the source / caller / owner")] string sourceNodeId,
        [Description("Node ID of the target / callee / owned")] string targetNodeId,
        [Description("Relationship label (e.g. 'calls', 'depends-on', 'implements', 'publishes')")] string relation,
        [Description("Edge weight; higher values indicate stronger or more frequent relationships")] double weight = 1.0,
        [Description("True for a directed edge (source → target); false for undirected")] bool directed = true)
    {
        var edge = session.Graph.AddEdge(sourceNodeId, targetNodeId, relation, weight, directed);

        return JsonSerializer.Serialize(new
        {
            edgeId       = edge.Id,
            sourceNodeId = edge.SourceNodeId,
            targetNodeId = edge.TargetNodeId,
            relation     = edge.RelationLabel,
            weight       = edge.Weight,
            directed     = edge.IsDirected
        }, _json);
    }

    // ── get_neighbors ─────────────────────────────────────────────────────────

    [McpServerTool, Description(
        "Return all nodes directly connected to the given node via any edge. " +
        "Set includeInterLayer to false to restrict results to nodes in the same layer.")]
    public string GetNeighbors(
        [Description("Node ID to start from")] string nodeId,
        [Description("Include neighbors reached via inter-layer edges (default true)")] bool includeInterLayer = true)
    {
        var neighbors = session.Graph.GetNeighbors(nodeId, includeInterLayer);

        var result = neighbors.Select(n =>
        {
            var layerNames = n.LayerIds
                .Select(id => session.Graph.GetLayer(id)?.Name ?? id)
                .ToList();
            return new
            {
                nodeId  = n.Id,
                label   = n.Label,
                layers  = layerNames
            };
        });

        return JsonSerializer.Serialize(new
        {
            startNodeId       = nodeId,
            includeInterLayer,
            neighborCount     = neighbors.Count,
            neighbors         = result
        }, _json);
    }

    // ── traverse ──────────────────────────────────────────────────────────────

    [McpServerTool, Description(
        "Breadth-first traversal from a starting node, up to maxDepth hops. " +
        "Useful for discovering all components reachable from an entry point or " +
        "all dependencies of a given module.")]
    public string Traverse(
        [Description("Node ID to start the BFS from")] string startNodeId,
        [Description("Maximum number of hops to follow (1–10)")] int maxDepth = 3)
    {
        maxDepth = Math.Clamp(maxDepth, 1, 10);
        var visited = session.Graph.BreadthFirstSearch(startNodeId, maxDepth);

        var result = visited.Select(n =>
        {
            var layerNames = n.LayerIds
                .Select(id => session.Graph.GetLayer(id)?.Name ?? id)
                .ToList();
            return new
            {
                nodeId = n.Id,
                label  = n.Label,
                layers = layerNames
            };
        });

        return JsonSerializer.Serialize(new
        {
            startNodeId,
            maxDepth,
            visitedCount = visited.Count,
            nodes        = result
        }, _json);
    }
}
