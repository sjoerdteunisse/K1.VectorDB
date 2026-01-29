namespace K1.VectorDB.Engine.EmbeddingProviders.LMStudio;

internal class EmbeddingRequest
{
    public required string input { get; set; }
    public required string model { get; set; }
}