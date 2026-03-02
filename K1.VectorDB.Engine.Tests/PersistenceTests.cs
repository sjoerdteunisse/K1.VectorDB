using K1.VectorDB.Engine.EmbeddingProviders;

namespace K1.VectorDB.Engine.Tests;

/// <summary>
/// Deterministic embedder for tests — maps known strings to fixed vectors.
/// Falls back to a default vector for unknown inputs.
/// </summary>
internal class MockEmbedder : IEmbedder
{
    private readonly Dictionary<string, double[]> _vectors;
    private readonly double[] _default;

    public MockEmbedder(Dictionary<string, double[]>? vectors = null, double[]? defaultVector = null)
    {
        _vectors = vectors ?? new Dictionary<string, double[]>();
        _default = defaultVector ?? [0.1, 0.2, 0.3];
    }

    public double[] GetVector(string document) =>
        _vectors.TryGetValue(document, out var v) ? v : _default;

    public double[][] GetVectors(string[] documents) =>
        documents.Select(GetVector).ToArray();
}

[TestFixture]
public class PersistenceTests
{
    private const string DbPath = "PersistenceTestDb";

    private static IEmbedder MockEmbedder() => new MockEmbedder(new Dictionary<string, double[]>
    {
        ["dogs"] = [1.0, 0.0, 0.0],
        ["cats"] = [0.0, 1.0, 0.0],
        ["fish"] = [0.0, 0.0, 1.0],
    });

    [SetUp]
    public void Setup()
    {
        if (Directory.Exists(DbPath)) Directory.Delete(DbPath, true);
    }

    [TearDown]
    public void Teardown()
    {
        if (Directory.Exists(DbPath)) Directory.Delete(DbPath, true);
    }

    // ── File-level checks ────────────────────────────────────────────────────

    [Test]
    public void Save_CreatesIndexListFile()
    {
        var db = new VectorDb(MockEmbedder(), DbPath);
        db.CreateIndex("Animals");
        db.IndexDocument("Animals", "dogs");
        db.Save();

        Assert.That(File.Exists(Path.Combine(DbPath, "indexs.txt")), Is.True,
            "indexs.txt must exist after Save");
    }

    [Test]
    public void Save_IndexListFileContainsAllIndexNames()
    {
        var db = new VectorDb(MockEmbedder(), DbPath);
        db.CreateIndex("Animals");
        db.CreateIndex("Other");
        db.Save();

        var lines = File.ReadAllLines(Path.Combine(DbPath, "indexs.txt"));
        Assert.That(lines, Does.Contain("Default"), "Default index should be listed");
        Assert.That(lines, Does.Contain("Animals"), "Animals index should be listed");
        Assert.That(lines, Does.Contain("Other"), "Other index should be listed");
    }

    [Test]
    public void Save_CreatesVectorAndDocumentBinFiles()
    {
        var db = new VectorDb(MockEmbedder(), DbPath);
        db.CreateIndex("Animals");
        db.IndexDocument("Animals", "dogs");
        db.Save();

        Assert.That(File.Exists(Path.Combine(DbPath, "Animals", "vectors.bin")), Is.True,
            "vectors.bin must be created for each index");
        Assert.That(File.Exists(Path.Combine(DbPath, "Animals", "documents.bin")), Is.True,
            "documents.bin must be created for each index");
    }

    [Test]
    public void Save_BinFilesHaveNonZeroLength()
    {
        var db = new VectorDb(MockEmbedder(), DbPath);
        db.CreateIndex("Animals");
        db.IndexDocument("Animals", "dogs");
        db.Save();

        var vectorsSize = new FileInfo(Path.Combine(DbPath, "Animals", "vectors.bin")).Length;
        var docsSize    = new FileInfo(Path.Combine(DbPath, "Animals", "documents.bin")).Length;

        Assert.That(vectorsSize, Is.GreaterThan(0), "vectors.bin must not be empty");
        Assert.That(docsSize,    Is.GreaterThan(0), "documents.bin must not be empty");
    }

    [Test]
    public void Save_MultipleDocuments_FileSizeGrowsWithMoreData()
    {
        // One doc
        var db1 = new VectorDb(MockEmbedder(), DbPath);
        db1.CreateIndex("Animals");
        db1.IndexDocument("Animals", "dogs");
        db1.Save();
        var size1 = new FileInfo(Path.Combine(DbPath, "Animals", "vectors.bin")).Length;

        Directory.Delete(DbPath, true);

        // Three docs
        var db3 = new VectorDb(MockEmbedder(), DbPath);
        db3.CreateIndex("Animals");
        db3.IndexDocument("Animals", "dogs");
        db3.IndexDocument("Animals", "cats");
        db3.IndexDocument("Animals", "fish");
        db3.Save();
        var size3 = new FileInfo(Path.Combine(DbPath, "Animals", "vectors.bin")).Length;

        Assert.That(size3, Is.GreaterThan(size1),
            "vectors.bin should be larger when more documents are stored");
    }

    // ── Round-trip correctness ────────────────────────────────────────────────

    [Test]
    public void SaveAndLoad_DocumentCountIsPreserved()
    {
        var db = new VectorDb(MockEmbedder(), DbPath);
        db.CreateIndex("Animals");
        db.IndexDocument("Animals", "dogs");
        db.IndexDocument("Animals", "cats");
        db.IndexDocument("Animals", "fish");
        db.Save();

        var loaded = new VectorDb(MockEmbedder(), DbPath);
        loaded.Load();

        var result = loaded.QueryCosineSimilarity("dogs", 10);
        Assert.That(result.Documents.Count, Is.EqualTo(3),
            "All three documents must survive the save/load round-trip");
    }

    [Test]
    public void SaveAndLoad_DocumentContentIsPreserved()
    {
        var db = new VectorDb(MockEmbedder(), DbPath);
        db.CreateIndex("Animals");
        db.IndexDocument("Animals", "dogs");
        db.IndexDocument("Animals", "cats");
        db.Save();

        var loaded = new VectorDb(MockEmbedder(), DbPath);
        loaded.Load();

        var result   = loaded.QueryCosineSimilarity("dogs", 10);
        var contents = result.Documents.Select(d => d.DocumentContent).ToList();

        Assert.That(contents, Does.Contain("dogs"), "Document 'dogs' must be present after load");
        Assert.That(contents, Does.Contain("cats"), "Document 'cats' must be present after load");
    }

    [Test]
    public void SaveAndLoad_DocumentIdsArePreserved()
    {
        var db = new VectorDb(MockEmbedder(), DbPath);
        db.CreateIndex("Animals");
        db.IndexDocument("Animals", "dogs");
        db.Save();

        var originalId = db.QueryCosineSimilarity("dogs", 1).Documents[0].Id;

        var loaded = new VectorDb(MockEmbedder(), DbPath);
        loaded.Load();

        var restoredId = loaded.QueryCosineSimilarity("dogs", 1).Documents[0].Id;

        Assert.That(restoredId, Is.EqualTo(originalId),
            "Document GUID must survive serialization unchanged");
    }

    [Test]
    public void SaveAndLoad_VectorsAreRestoredCorrectly()
    {
        // With known orthogonal vectors, the top-1 result for "dogs" must be "dogs"
        var db = new VectorDb(MockEmbedder(), DbPath);
        db.CreateIndex("Animals");
        db.IndexDocument("Animals", "dogs");  // [1, 0, 0]
        db.IndexDocument("Animals", "cats");  // [0, 1, 0]
        db.IndexDocument("Animals", "fish");  // [0, 0, 1]
        db.Save();

        var loaded = new VectorDb(MockEmbedder(), DbPath);
        loaded.Load();

        var result = loaded.QueryCosineSimilarity("dogs", 1);

        Assert.That(result.Documents.Count, Is.EqualTo(1));
        Assert.That(result.Documents[0].DocumentContent, Is.EqualTo("dogs"),
            "Cosine similarity must pick the correct vector after load");
    }

    [Test]
    public void SaveAndLoad_MultipleIndexes_AllIndexesRestored()
    {
        var db = new VectorDb(MockEmbedder(), DbPath);
        db.CreateIndex("Animals");
        db.CreateIndex("Other");
        db.IndexDocument("Animals", "dogs");
        db.IndexDocument("Animals", "cats");
        db.IndexDocument("Other", "fish");
        db.Save();

        var loaded = new VectorDb(MockEmbedder(), DbPath);
        loaded.Load();

        Assert.That(loaded.IndexNames, Does.Contain("Animals"));
        Assert.That(loaded.IndexNames, Does.Contain("Other"));
    }

    [Test]
    public void SaveTwice_SecondSaveAfterMutation_OverwritesCorrectly()
    {
        var db = new VectorDb(MockEmbedder(), DbPath);
        db.CreateIndex("Animals");
        db.IndexDocument("Animals", "dogs");
        db.Save();

        // Mutate and save again
        db.IndexDocument("Animals", "cats");
        db.Save();

        var loaded = new VectorDb(MockEmbedder(), DbPath);
        loaded.Load();

        var result = loaded.QueryCosineSimilarity("dogs", 10);
        Assert.That(result.Documents.Count, Is.EqualTo(2),
            "Second save must persist the newly added document");
    }

    [Test]
    public void Load_RestoredDb_CanAcceptNewDocuments()
    {
        var db = new VectorDb(MockEmbedder(), DbPath);
        db.CreateIndex("Animals");
        db.IndexDocument("Animals", "dogs");
        db.Save();

        var loaded = new VectorDb(MockEmbedder(), DbPath);
        loaded.Load();
        loaded.IndexDocument("Animals", "cats");

        var result = loaded.QueryCosineSimilarity("dogs", 10);
        Assert.That(result.Documents.Count, Is.EqualTo(2),
            "A loaded database must accept new documents normally");
    }
}