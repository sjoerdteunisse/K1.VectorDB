namespace K1.VectorDB.Engine.EmbeddingProviders.LMStudio;

internal class Data
{
    public required string @object { get; set; }
    public required List<double> embedding { get; set; }
    public required int index { get; set; }
}