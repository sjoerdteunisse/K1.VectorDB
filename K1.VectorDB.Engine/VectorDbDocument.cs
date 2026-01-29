using MessagePack;

namespace K1.VectorDB.Engine;

[MessagePackObject]
public class VectorDbDocument(string id, string documentContent)
{
    public VectorDbDocument() : this(Guid.NewGuid().ToString(), string.Empty)
    {
    }

    public VectorDbDocument(string documentContent) : this(Guid.NewGuid().ToString(), documentContent)
    {
    }

    [Key(0)] public string Id { get; set; } = id;

    [Key(1)] public string DocumentContent { get; set; } = documentContent;
}