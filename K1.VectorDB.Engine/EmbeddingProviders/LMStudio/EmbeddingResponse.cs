namespace K1.VectorDB.Engine.EmbeddingProviders.LMStudio;

internal class EmbeddingResponse
{
    public required string @object { get; set; }
    public required List<Data> data { get; set; }
    public required string model { get; set; }
    public required Usage usage { get; set; }
}