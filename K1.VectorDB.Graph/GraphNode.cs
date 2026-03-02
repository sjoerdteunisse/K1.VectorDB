using MessagePack;

namespace K1.VectorDB.Graph;

[MessagePackObject]
public class GraphNode
{
    [Key(0)] public string Id { get; set; } = Guid.NewGuid().ToString();
    [Key(1)] public string Label { get; set; } = string.Empty;
    [Key(2)] public string Content { get; set; } = string.Empty;
    [Key(3)] public List<string> LayerIds { get; set; } = [];
    [Key(4)] public Dictionary<string, string> Metadata { get; set; } = [];
}
