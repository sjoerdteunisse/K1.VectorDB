namespace K1.VectorDB.Engine;

public class VectorDbQueryResult(List<VectorDbDocument> documents, List<double> distances)
{
    public List<VectorDbDocument> Documents { get; set; } = documents;

    public List<double> Distances { get; set; } = distances;
}