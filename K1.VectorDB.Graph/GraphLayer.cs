using MessagePack;

namespace K1.VectorDB.Graph;

[MessagePackObject]
public class GraphLayer
{
    [Key(0)] public string Id { get; set; } = Guid.NewGuid().ToString();
    [Key(1)] public string Name { get; set; } = string.Empty;
    [Key(2)] public string Description { get; set; } = string.Empty;
}
