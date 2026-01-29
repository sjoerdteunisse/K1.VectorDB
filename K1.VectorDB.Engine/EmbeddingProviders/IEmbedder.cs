namespace K1.VectorDB.Engine.EmbeddingProviders;

public interface IEmbedder
{
    public double[] GetVector(string document);
    public double[][] GetVectors(string[] documents);
}