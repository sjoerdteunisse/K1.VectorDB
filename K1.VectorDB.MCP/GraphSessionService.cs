using K1.VectorDB.Engine.EmbeddingProviders.LMStudio;
using K1.VectorDB.Graph;

namespace K1.VectorDB.MCP;

/// <summary>
/// Singleton that holds the live MultiLayerGraph for the duration of the MCP server process.
/// Tools obtain the graph through this service; it must be initialized or loaded before use.
/// </summary>
public sealed class GraphSessionService
{
    private MultiLayerGraph? _graph;

    public MultiLayerGraph Graph => _graph
        ?? throw new InvalidOperationException(
            "Graph is not initialized. Call initialize_graph or load_graph first.");

    public bool IsInitialized => _graph is not null;

    /// <summary>
    /// Create a brand-new graph at <paramref name="path"/> with the 9 standard abstraction layers.
    /// </summary>
    public MultiLayerGraph Initialize(string path, string lmStudioUrl, string model)
    {
        var embedder = new LMStudioEmbedder { URL = lmStudioUrl, Model = model };
        _graph = new MultiLayerGraph(embedder, path);

        foreach (var (name, desc) in StandardLayers.All)
            _graph.AddLayer(name, desc);

        _graph.Save();
        return _graph;
    }

    /// <summary>
    /// Load an existing graph from disk at <paramref name="path"/>.
    /// </summary>
    public MultiLayerGraph Load(string path, string lmStudioUrl, string model)
    {
        var embedder = new LMStudioEmbedder { URL = lmStudioUrl, Model = model };
        _graph = new MultiLayerGraph(embedder, path);
        _graph.Load();
        return _graph;
    }

    /// <summary>
    /// Resolve the internal GUID layer ID for a friendly layer name (case-insensitive).
    /// </summary>
    public string ResolveLayerId(string layerName)
    {
        var layer = Graph.Layers.FirstOrDefault(l =>
            l.Name.Equals(layerName, StringComparison.OrdinalIgnoreCase))
            ?? throw new ArgumentException(
                $"Layer '{layerName}' not found. Available layers: " +
                string.Join(", ", Graph.Layers.Select(l => l.Name)));

        return layer.Id;
    }
}

/// <summary>
/// The nine abstraction layers that map to the documentation agent's hierarchy levels.
/// </summary>
internal static class StandardLayers
{
    public static readonly (string name, string desc)[] All =
    [
        ("PURPOSE",   "Goals, stakeholders, and rationale — who uses the system and why"),
        ("CONTEXT",   "External systems, APIs, and third-party integrations the system interacts with"),
        ("CONTAINER", "Deployable units: services, applications, workers, scheduled jobs"),
        ("COMPONENT", "Internals of each container — modules, subsystems, libraries"),
        ("FLOW",      "Runtime interactions: what calls what, when, and over which protocol or topic"),
        ("DATA",      "Data models, schemas, DTOs, entities, and event/message payload types"),
        ("STATE",     "State machines and lifecycle transitions for key entities or processes"),
        ("DEPLOY",    "Infrastructure topology: hosts, clusters, cloud regions, networking"),
        ("CLASS",     "Code-level types, class hierarchies, and interface contracts"),
    ];
}
