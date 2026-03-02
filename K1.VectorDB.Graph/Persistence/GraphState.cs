using MessagePack;

namespace K1.VectorDB.Graph.Persistence;

[MessagePackObject(AllowPrivate = true)]
internal class GraphState
{
    [Key(0)] public List<GraphLayer> Layers { get; set; } = [];
    [Key(1)] public List<GraphNode> Nodes { get; set; } = [];
    [Key(2)] public List<GraphEdge> Edges { get; set; } = [];
}
