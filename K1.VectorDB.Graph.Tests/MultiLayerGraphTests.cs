using K1.VectorDB.Engine.EmbeddingProviders;
using K1.VectorDB.Graph;

namespace K1.VectorDB.Graph.Tests;

// ── MockEmbedder ──────────────────────────────────────────────────────────────

internal class MockEmbedder : IEmbedder
{
    private static readonly Dictionary<string, double[]> _vectors = new()
    {
        ["dogs"]     = [1.0, 0.0, 0.0, 0.0],
        ["cats"]     = [0.0, 1.0, 0.0, 0.0],
        ["fish"]     = [0.0, 0.0, 1.0, 0.0],
        ["birds"]    = [0.0, 0.0, 0.0, 1.0],
        ["mammals"]  = [0.9, 0.4, 0.0, 0.0],
        ["pets"]     = [0.7, 0.7, 0.0, 0.0],
        ["query:dogs"]    = [1.0, 0.0, 0.0, 0.0],
        ["query:cats"]    = [0.0, 1.0, 0.0, 0.0],
        ["query:animals"] = [0.5, 0.5, 0.5, 0.5],
    };

    private static readonly double[] _default = [0.25, 0.25, 0.25, 0.25];

    public double[] GetVector(string text) =>
        _vectors.TryGetValue(text, out var v) ? v : _default;

    public double[][] GetVectors(string[] texts) =>
        texts.Select(GetVector).ToArray();
}

// ── Test fixture ──────────────────────────────────────────────────────────────

[TestFixture]
public class MultiLayerGraphTests
{
    private const string TestPath = "TestGraphDb";
    private MultiLayerGraph _graph = null!;

    [SetUp]
    public void Setup()
    {
        if (Directory.Exists(TestPath)) Directory.Delete(TestPath, true);
        _graph = new MultiLayerGraph(new MockEmbedder(), TestPath);
    }

    [TearDown]
    public void Teardown()
    {
        if (Directory.Exists(TestPath)) Directory.Delete(TestPath, true);
    }

    // ── Layer tests ───────────────────────────────────────────────────────────

    [Test]
    public void AddLayer_LayerIsRetrievable()
    {
        var layer = _graph.AddLayer("Animals", "All animals");

        Assert.That(_graph.GetLayer(layer.Id), Is.Not.Null);
        Assert.That(_graph.GetLayer(layer.Id)!.Name, Is.EqualTo("Animals"));
        Assert.That(_graph.GetLayer(layer.Id)!.Description, Is.EqualTo("All animals"));
    }

    // ── Node tests ────────────────────────────────────────────────────────────

    [Test]
    public void AddNode_NodeStoredInLayer()
    {
        var layer = _graph.AddLayer("Animals");
        var node = _graph.AddNode(layer.Id, "Dog", "dogs");

        var nodes = _graph.GetNodesInLayer(layer.Id);
        Assert.That(nodes.Count, Is.EqualTo(1));
        Assert.That(nodes[0].Id, Is.EqualTo(node.Id));
        Assert.That(nodes[0].Label, Is.EqualTo("Dog"));
    }

    [Test]
    public void AddNode_DuplicateContentSameLayer_Throws()
    {
        var layer = _graph.AddLayer("Animals");
        _graph.AddNode(layer.Id, "Dog1", "dogs");

        Assert.Throws<InvalidOperationException>(() =>
            _graph.AddNode(layer.Id, "Dog2", "dogs"));
    }

    [Test]
    public void PlaceNodeInLayer_NodeAppearsInBothLayers()
    {
        var layerA = _graph.AddLayer("LayerA");
        var layerB = _graph.AddLayer("LayerB");
        var node = _graph.AddNode(layerA.Id, "Dog", "dogs");

        _graph.PlaceNodeInLayer(node.Id, layerB.Id);

        Assert.That(_graph.GetNodesInLayer(layerA.Id).Any(n => n.Id == node.Id), Is.True);
        Assert.That(_graph.GetNodesInLayer(layerB.Id).Any(n => n.Id == node.Id), Is.True);
    }

    [Test]
    public void PlaceNodeInLayer_DuplicateContentSameLayer_Throws()
    {
        var layerA = _graph.AddLayer("LayerA");
        var layerB = _graph.AddLayer("LayerB");
        var nodeA = _graph.AddNode(layerA.Id, "Dog1", "dogs");
        _graph.AddNode(layerB.Id, "Dog2", "dogs");

        Assert.Throws<InvalidOperationException>(() =>
            _graph.PlaceNodeInLayer(nodeA.Id, layerB.Id));
    }

    // ── Edge tests ────────────────────────────────────────────────────────────

    [Test]
    public void AddEdge_IntraLayer_Retrievable()
    {
        var layer = _graph.AddLayer("Animals");
        var dog = _graph.AddNode(layer.Id, "Dog", "dogs");
        var cat = _graph.AddNode(layer.Id, "Cat", "cats");

        var edge = _graph.AddEdge(dog.Id, cat.Id, "eats");

        var intra = _graph.GetIntraLayerEdges(layer.Id);
        Assert.That(intra.Any(e => e.Id == edge.Id), Is.True);
    }

    [Test]
    public void AddEdge_InterLayer_Retrievable()
    {
        var layerA = _graph.AddLayer("LayerA");
        var layerB = _graph.AddLayer("LayerB");
        var dog = _graph.AddNode(layerA.Id, "Dog", "dogs");
        var fish = _graph.AddNode(layerB.Id, "Fish", "fish");

        var edge = _graph.AddEdge(dog.Id, fish.Id, "related");

        var inter = _graph.GetInterLayerEdges();
        Assert.That(inter.Any(e => e.Id == edge.Id), Is.True);
    }

    [Test]
    public void AddEdge_UndirectedEdge_BothDirectionsTraversable()
    {
        var layer = _graph.AddLayer("Animals");
        var dog = _graph.AddNode(layer.Id, "Dog", "dogs");
        var cat = _graph.AddNode(layer.Id, "Cat", "cats");

        _graph.AddEdge(dog.Id, cat.Id, "friends", directed: false);

        var dogNeighbors = _graph.GetOutEdges(dog.Id);
        var catNeighbors = _graph.GetOutEdges(cat.Id);

        Assert.That(dogNeighbors.Count, Is.GreaterThan(0));
        Assert.That(catNeighbors.Count, Is.GreaterThan(0));
    }

    // ── Cascade delete tests ──────────────────────────────────────────────────

    [Test]
    public void RemoveNode_RemovesEdgesAndLayerMembership()
    {
        var layer = _graph.AddLayer("Animals");
        var dog = _graph.AddNode(layer.Id, "Dog", "dogs");
        var cat = _graph.AddNode(layer.Id, "Cat", "cats");
        _graph.AddEdge(dog.Id, cat.Id, "eats");

        _graph.RemoveNode(dog.Id);

        Assert.That(_graph.GetNode(dog.Id), Is.Null);
        Assert.That(_graph.AllEdges.Count, Is.EqualTo(0));
        Assert.That(_graph.GetNodesInLayer(layer.Id).Any(n => n.Id == dog.Id), Is.False);
    }

    [Test]
    public void RemoveLayer_RemovesNodesAndEdges()
    {
        var layerA = _graph.AddLayer("LayerA");
        var layerB = _graph.AddLayer("LayerB");
        var dog = _graph.AddNode(layerA.Id, "Dog", "dogs");
        var fish = _graph.AddNode(layerB.Id, "Fish", "fish");
        _graph.AddEdge(dog.Id, fish.Id, "related");

        _graph.RemoveLayer(layerA.Id);

        Assert.That(_graph.GetLayer(layerA.Id), Is.Null);
        Assert.That(_graph.GetNode(dog.Id), Is.Null);
        Assert.That(_graph.AllEdges.Count, Is.EqualTo(0));
    }

    // ── Traversal tests ───────────────────────────────────────────────────────

    [Test]
    public void GetNeighbors_ReturnsCorrectNodes()
    {
        var layer = _graph.AddLayer("Animals");
        var dog = _graph.AddNode(layer.Id, "Dog", "dogs");
        var cat = _graph.AddNode(layer.Id, "Cat", "cats");
        var fish = _graph.AddNode(layer.Id, "Fish", "fish");
        _graph.AddEdge(dog.Id, cat.Id, "eats");
        _graph.AddEdge(dog.Id, fish.Id, "ignores");

        var neighbors = _graph.GetNeighbors(dog.Id);

        Assert.That(neighbors.Count, Is.EqualTo(2));
        Assert.That(neighbors.Any(n => n.Id == cat.Id), Is.True);
        Assert.That(neighbors.Any(n => n.Id == fish.Id), Is.True);
    }

    [Test]
    public void BreadthFirstSearch_RespectsMaxDepth()
    {
        var layer = _graph.AddLayer("Chain");
        var a = _graph.AddNode(layer.Id, "A", "dogs");
        var b = _graph.AddNode(layer.Id, "B", "cats");
        var c = _graph.AddNode(layer.Id, "C", "fish");
        var d = _graph.AddNode(layer.Id, "D", "birds");
        var e = _graph.AddNode(layer.Id, "E", "mammals");
        _graph.AddEdge(a.Id, b.Id, "link");
        _graph.AddEdge(b.Id, c.Id, "link");
        _graph.AddEdge(c.Id, d.Id, "link");
        _graph.AddEdge(d.Id, e.Id, "link");

        var result = _graph.BreadthFirstSearch(a.Id, maxDepth: 2);

        // From A with maxDepth=2: B (depth 1), C (depth 2), but not D or E
        Assert.That(result.Any(n => n.Id == b.Id), Is.True);
        Assert.That(result.Any(n => n.Id == c.Id), Is.True);
        Assert.That(result.Any(n => n.Id == d.Id), Is.False);
        Assert.That(result.Any(n => n.Id == e.Id), Is.False);
    }

    // ── Semantic search tests ─────────────────────────────────────────────────

    [Test]
    public void SearchNodes_CrossLayer_ReturnsSimilarNodes()
    {
        var layerA = _graph.AddLayer("LayerA");
        var layerB = _graph.AddLayer("LayerB");
        _graph.AddNode(layerA.Id, "Dog", "dogs");
        _graph.AddNode(layerB.Id, "Cat", "cats");

        var results = _graph.SearchNodes("query:dogs", topK: 1);

        Assert.That(results.Count, Is.EqualTo(1));
        Assert.That(results[0].Node.Content, Is.EqualTo("dogs"));
    }

    [Test]
    public void SearchNodesInLayer_ScopedToSingleLayer()
    {
        var layerA = _graph.AddLayer("LayerA");
        var layerB = _graph.AddLayer("LayerB");
        _graph.AddNode(layerA.Id, "Dog", "dogs");
        _graph.AddNode(layerB.Id, "Cat", "cats");

        var results = _graph.SearchNodesInLayer(layerB.Id, "query:cats", topK: 5);

        Assert.That(results.Count, Is.EqualTo(1));
        Assert.That(results[0].Node.Content, Is.EqualTo("cats"));
    }

    // ── Save / Load tests ─────────────────────────────────────────────────────

    [Test]
    public void SaveAndLoad_LayersPreserved()
    {
        var layer1 = _graph.AddLayer("Animals", "All animals");
        var layer2 = _graph.AddLayer("Plants", "All plants");
        _graph.Save();

        _graph = new MultiLayerGraph(new MockEmbedder(), TestPath);
        _graph.Load();

        Assert.That(_graph.Layers.Count, Is.EqualTo(2));
        Assert.That(_graph.Layers.Any(l => l.Name == "Animals"), Is.True);
        Assert.That(_graph.Layers.Any(l => l.Name == "Plants"), Is.True);
    }

    [Test]
    public void SaveAndLoad_NodesPreserved()
    {
        var layer = _graph.AddLayer("Animals");
        var node = _graph.AddNode(layer.Id, "Dog", "dogs", new Dictionary<string, string> { ["key"] = "val" });
        _graph.Save();

        _graph = new MultiLayerGraph(new MockEmbedder(), TestPath);
        _graph.Load();

        var loaded = _graph.GetNode(node.Id);
        Assert.That(loaded, Is.Not.Null);
        Assert.That(loaded!.Label, Is.EqualTo("Dog"));
        Assert.That(loaded.Content, Is.EqualTo("dogs"));
        Assert.That(loaded.Metadata["key"], Is.EqualTo("val"));
    }

    [Test]
    public void SaveAndLoad_EdgesPreserved()
    {
        var layer = _graph.AddLayer("Animals");
        var dog = _graph.AddNode(layer.Id, "Dog", "dogs");
        var cat = _graph.AddNode(layer.Id, "Cat", "cats");
        var edge = _graph.AddEdge(dog.Id, cat.Id, "eats", weight: 2.5, directed: false);
        _graph.Save();

        _graph = new MultiLayerGraph(new MockEmbedder(), TestPath);
        _graph.Load();

        var loaded = _graph.GetEdge(edge.Id);
        Assert.That(loaded, Is.Not.Null);
        Assert.That(loaded!.RelationLabel, Is.EqualTo("eats"));
        Assert.That(loaded.Weight, Is.EqualTo(2.5));
        Assert.That(loaded.IsDirected, Is.False);
    }

    [Test]
    public void SaveAndLoad_SearchWorksAfterLoad()
    {
        var layer = _graph.AddLayer("Animals");
        _graph.AddNode(layer.Id, "Dog", "dogs");
        _graph.AddNode(layer.Id, "Cat", "cats");
        _graph.Save();

        _graph = new MultiLayerGraph(new MockEmbedder(), TestPath);
        _graph.Load();

        var results = _graph.SearchNodes("query:dogs", topK: 1);
        Assert.That(results.Count, Is.EqualTo(1));
        Assert.That(results[0].Node.Content, Is.EqualTo("dogs"));
    }

    [Test]
    public void SaveAndLoad_MultiLayerMembershipPreserved()
    {
        var layerA = _graph.AddLayer("LayerA");
        var layerB = _graph.AddLayer("LayerB");
        var node = _graph.AddNode(layerA.Id, "Dog", "dogs");
        _graph.PlaceNodeInLayer(node.Id, layerB.Id);
        _graph.Save();

        _graph = new MultiLayerGraph(new MockEmbedder(), TestPath);
        _graph.Load();

        var loaded = _graph.GetNode(node.Id);
        Assert.That(loaded, Is.Not.Null);
        Assert.That(loaded!.LayerIds.Count, Is.EqualTo(2));
        Assert.That(loaded.LayerIds.Contains(layerA.Id), Is.True);
        Assert.That(loaded.LayerIds.Contains(layerB.Id), Is.True);
    }
}
