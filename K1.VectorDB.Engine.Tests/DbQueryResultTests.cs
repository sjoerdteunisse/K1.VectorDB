namespace K1.VectorDB.Engine.Tests;

[TestFixture]
public class DbQueryResultTests
{
    [Test]
    public void ConstructorValidation()
    {
        var random = new Random(0);

        List<VectorDbDocument> documents = [];
        List<double> distances = [];

        for (var i = 0; i < 100; i++)
        {
            documents.Add(new VectorDbDocument(random.Next().ToString()));
            distances.Add(random.NextDouble());
        }

        VectorDbQueryResult vectorDbQueryResult = new(documents, distances);

        Assert.Pass();
    }
}