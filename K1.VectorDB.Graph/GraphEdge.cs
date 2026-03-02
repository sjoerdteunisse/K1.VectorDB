using MessagePack;

namespace K1.VectorDB.Graph;

[MessagePackObject]
public class GraphEdge
{
    [Key(0)] public string Id { get; set; } = Guid.NewGuid().ToString();
    [Key(1)] public string SourceNodeId { get; set; } = string.Empty;
    [Key(2)] public string TargetNodeId { get; set; } = string.Empty;
    [Key(3)] public string RelationLabel { get; set; } = string.Empty;
    [Key(4)] public double Weight { get; set; } = 1.0;
    [Key(5)] public bool IsDirected { get; set; } = true;
}
