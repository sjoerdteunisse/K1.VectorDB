using K1.VectorDB.Engine;
using K1.VectorDB.Graph.Persistence;
using MessagePack;

namespace K1.VectorDB.Graph.Services;

internal sealed class GraphPersistenceService
{
    private readonly GraphStore _store;
    private readonly VectorDb _vectorDb;
    private readonly string _path;
    private readonly MessagePackSerializerOptions _mpOptions;

    internal GraphPersistenceService(GraphStore store, VectorDb vectorDb, string path,
        MessagePackSerializerOptions mpOptions)
    {
        _store = store;
        _vectorDb = vectorDb;
        _path = path;
        _mpOptions = mpOptions;
    }

    internal void Save()
    {
        if (!Directory.Exists(_path)) Directory.CreateDirectory(_path);

        var state = new GraphState
        {
            Layers = [.. _store.Layers],
            Nodes = [.. _store.Nodes.Values],
            Edges = [.. _store.Edges.Values]
        };

        var bytes = MessagePackSerializer.Serialize(state, _mpOptions);
        File.WriteAllBytes(Path.Combine(_path, "graph_state.bin"), bytes);

        _vectorDb.Save();
    }

    internal void Load()
    {
        var statePath = Path.Combine(_path, "graph_state.bin");
        if (!File.Exists(statePath)) return;

        var bytes = File.ReadAllBytes(statePath);
        var state = MessagePackSerializer.Deserialize<GraphState>(bytes, _mpOptions);

        _store.Layers.Clear();
        _store.Nodes.Clear();
        _store.Edges.Clear();
        _store.OutEdges.Clear();
        _store.InEdges.Clear();

        _store.Layers.AddRange(state.Layers);

        foreach (var node in state.Nodes)
        {
            _store.Nodes[node.Id] = node;
            _store.EnsureAdjacency(node.Id);
        }

        foreach (var edge in state.Edges)
        {
            _store.Edges[edge.Id] = edge;
            _store.RegisterEdge(edge);
        }

        _vectorDb.Load();
    }
}
