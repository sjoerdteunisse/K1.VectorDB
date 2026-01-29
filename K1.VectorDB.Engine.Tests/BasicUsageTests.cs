using K1.VectorDB.Engine.EmbeddingProviders.LMStudio;

namespace K1.VectorDB.Engine.Tests;

[TestFixture]
public class BasicUsageTests
{
    [SetUp]
    public void Setup()
    {
        if (Directory.Exists("TestDatabase")) Directory.Delete("TestDatabase", true);
    }

    [TearDown]
    public void Teardown()
    {
        if (Directory.Exists("TestDatabase")) Directory.Delete("TestDatabase", true);
    }
    
    [Test]
    public void EdgeCases_ComprehensiveTest()
    {
        var db = new VectorDb(new LMStudioEmbedder(), "EdgeCaseTestDb");
        db.CreateIndex("TestIndex");

        var emptyResult = db.QueryCosineSimilarity("test", 5);
        Assert.That(emptyResult.Documents.Count, Is.EqualTo(0), "Empty database should return no results");

        // Edge Case 2: Add various edge case documents
        db.IndexDocument("TestIndex", "");  // Empty string
        db.IndexDocument("TestIndex", "     ");  // Whitespace only
        db.IndexDocument("TestIndex", "a");  // Single character
        db.IndexDocument("TestIndex", "!@#$%^&*()");  // Special characters only
        db.IndexDocument("TestIndex", "Hello 世界 🌍");  // Unicode and emoji
        db.IndexDocument("TestIndex", "Normal document about dogs");
        db.IndexDocument("TestIndex", "Normal document about dogs");  // Duplicate
        db.IndexDocument("TestIndex", string.Join(" ", Enumerable.Repeat("word", 1000)));  // Very long

        var result = db.QueryCosineSimilarity("dogs", topK: 100);
        Assert.That(result.Documents.Count, Is.LessThanOrEqualTo(8), "Should return at most total documents");

        result = db.QueryCosineSimilarity("dogs", topK: 1);
        Assert.That(result.Documents.Count, Is.EqualTo(1), "Should return exactly 1 result");

        result = db.QueryCosineSimilarity("dogs", topK: 5);
        for (int i = 0; i < result.Distances.Count - 1; i++)
        {
            Assert.That(result.Distances[i], Is.GreaterThanOrEqualTo(result.Distances[i + 1]),
                "Results must be ordered by descending similarity");
        }

        result = db.QueryCosineSimilarity("Hello 世界 🌍", topK: 1);
        Assert.That(result.Documents, Is.Not.Null);
        

        db.Save();
        db = new VectorDb(new LMStudioEmbedder(), "EdgeCaseTestDb");
        db.Load();
        result = db.QueryCosineSimilarity("dogs", 5);
        Assert.That(result.Documents.Count, Is.GreaterThan(0), "Loaded database should have documents");

        Assert.Pass();
    }

    [Test]
    public void BasicUsage()
    {
        var Db = new VectorDb(new LMStudioEmbedder(), "TestDatabase");
        Db.CreateIndex("Index");

        Db.IndexDocument("Index", "This is a test document about dogs");
        Db.IndexDocument("Index", "This is a test document about cats");
        Db.IndexDocument("Index", "This is a test document about fish");
        Db.IndexDocument("Index", "This is a test document about birds");
        Db.IndexDocument("Index", "This is a test document about dogs and cats");
        Db.IndexDocument("Index", "This is a test document about cats and fish");
        Db.IndexDocument("Index", "This is a test document about fish and birds");
        Db.IndexDocument("Index", "This is a test document about birds and dogs");
        Db.IndexDocument("Index", "This is a test document about dogs and cats and fish");
        Db.IndexDocument("Index", "This is a test document about cats and fish and birds");
        Db.IndexDocument("Index", "This is a test document about fish and birds and dogs");
        Db.IndexDocument("Index", "This is a test document about birds and dogs and cats");
        Db.IndexDocument("Index", "This is a test document about dogs and cats and fish and birds");
        Db.IndexDocument("Index", "This is a test document about cats and fish and birds and dogs");
        Db.IndexDocument("Index", "This is a test document about fish and birds and dogs and cats");
        Db.IndexDocument("Index", "This is a test document about birds and dogs and cats and fish");

        Db.Save();
        Db = new VectorDb(new LMStudioEmbedder(), "TestDatabase");
        Db.Load();

        var result = Db.QueryCosineSimilarity("dogs");
        Assert.That(result.Documents.Count == 5, Is.True);
        result = Db.QueryCosineSimilarity("cats", 10);
        Assert.That(result.Documents.Count == 10, Is.True);
        result = Db.QueryCosineSimilarity("fish", 3);
        Assert.That(result.Documents.Count == 3, Is.True);
        result = Db.QueryCosineSimilarity("birds", 1);
        Assert.That(result.Documents.Count == 1, Is.True);

        Assert.Pass();
    }
}