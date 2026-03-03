namespace K1.VectorDB.MCP.Tests;

[TestFixture]
public sealed class GraphSessionServiceTests
{
    private const string ExpectedLayerCount = "9 standard abstraction layers";
    private static readonly string[] AllLayerNames =
        ["PURPOSE", "CONTEXT", "CONTAINER", "COMPONENT", "FLOW", "DATA", "STATE", "DEPLOY", "CLASS"];

    private string _path = null!;
    private GraphSessionService _session = null!;

    [SetUp]
    public void Setup()
    {
        _path = Path.Combine(Path.GetTempPath(), $"session-test-{Guid.NewGuid():N}");
        _session = new GraphSessionService();
    }

    [TearDown]
    public void Teardown()
    {
        if (Directory.Exists(_path)) Directory.Delete(_path, recursive: true);
    }

    // ── Uninitialized state ───────────────────────────────────────────────────

    [Test]
    public void Graph_BeforeInit_ThrowsInvalidOperation()
    {
        Assert.Throws<InvalidOperationException>(() => _ = _session.Graph);
    }

    [Test]
    public void IsInitialized_BeforeInit_ReturnsFalse()
    {
        Assert.That(_session.IsInitialized, Is.False);
    }

    // ── InitializeWithEmbedder ────────────────────────────────────────────────

    [Test]
    public void InitializeWithEmbedder_Creates9Layers()
    {
        _session.InitializeWithEmbedder(_path, new FakeEmbedder());

        Assert.That(_session.Graph.Layers.Count, Is.EqualTo(9),
            $"Expected {ExpectedLayerCount}");
    }

    [Test]
    public void InitializeWithEmbedder_AllStandardLayerNamesPresent()
    {
        _session.InitializeWithEmbedder(_path, new FakeEmbedder());

        var names = _session.Graph.Layers.Select(l => l.Name).ToHashSet();

        foreach (var expected in AllLayerNames)
            Assert.That(names, Does.Contain(expected), $"Layer '{expected}' missing");
    }

    [Test]
    public void InitializeWithEmbedder_LayersHaveNonEmptyDescriptions()
    {
        _session.InitializeWithEmbedder(_path, new FakeEmbedder());

        foreach (var layer in _session.Graph.Layers)
            Assert.That(layer.Description, Is.Not.Empty,
                $"Layer '{layer.Name}' has no description");
    }

    [Test]
    public void InitializeWithEmbedder_SetsIsInitializedTrue()
    {
        _session.InitializeWithEmbedder(_path, new FakeEmbedder());

        Assert.That(_session.IsInitialized, Is.True);
    }

    [Test]
    public void InitializeWithEmbedder_PersistsGraphToDisk()
    {
        _session.InitializeWithEmbedder(_path, new FakeEmbedder());

        Assert.That(Directory.Exists(_path), Is.True,
            "Graph directory should be created on disk");
    }

    // ── LoadWithEmbedder ──────────────────────────────────────────────────────

    [Test]
    public void LoadWithEmbedder_RestoresLayers()
    {
        // Initialize and persist
        _session.InitializeWithEmbedder(_path, new FakeEmbedder());

        // Load into a fresh session
        var session2 = new GraphSessionService();
        session2.LoadWithEmbedder(_path, new FakeEmbedder());

        Assert.That(session2.Graph.Layers.Count, Is.EqualTo(9));
        var names = session2.Graph.Layers.Select(l => l.Name).ToHashSet();
        foreach (var expected in AllLayerNames)
            Assert.That(names, Does.Contain(expected));
    }

    [Test]
    public void LoadWithEmbedder_RestoresNodes()
    {
        _session.InitializeWithEmbedder(_path, new FakeEmbedder());
        var layerId = _session.ResolveLayerId("COMPONENT");
        var node = _session.Graph.AddNode(layerId, "AuthService", "auth-service");
        _session.Graph.Save();

        var session2 = new GraphSessionService();
        session2.LoadWithEmbedder(_path, new FakeEmbedder());

        var loaded = session2.Graph.GetNode(node.Id);
        Assert.That(loaded, Is.Not.Null);
        Assert.That(loaded!.Label, Is.EqualTo("AuthService"));
        Assert.That(loaded.Content, Is.EqualTo("auth-service"));
    }

    // ── ResolveLayerId ────────────────────────────────────────────────────────

    [Test]
    public void ResolveLayerId_ValidName_ReturnsId()
    {
        _session.InitializeWithEmbedder(_path, new FakeEmbedder());

        var id = _session.ResolveLayerId("COMPONENT");

        Assert.That(id, Is.Not.Empty);
        Assert.That(_session.Graph.GetLayer(id), Is.Not.Null);
        Assert.That(_session.Graph.GetLayer(id)!.Name, Is.EqualTo("COMPONENT"));
    }

    [Test]
    public void ResolveLayerId_CaseInsensitive()
    {
        _session.InitializeWithEmbedder(_path, new FakeEmbedder());

        var upper = _session.ResolveLayerId("COMPONENT");
        var lower = _session.ResolveLayerId("component");
        var mixed = _session.ResolveLayerId("Component");

        Assert.That(lower, Is.EqualTo(upper));
        Assert.That(mixed, Is.EqualTo(upper));
    }

    [Test]
    public void ResolveLayerId_UnknownLayer_ThrowsArgumentException()
    {
        _session.InitializeWithEmbedder(_path, new FakeEmbedder());

        var ex = Assert.Throws<ArgumentException>(() =>
            _session.ResolveLayerId("NONEXISTENT"));

        Assert.That(ex!.Message, Does.Contain("NONEXISTENT"));
    }

    [Test]
    public void ResolveLayerId_ErrorMessageListsAvailableLayers()
    {
        _session.InitializeWithEmbedder(_path, new FakeEmbedder());

        var ex = Assert.Throws<ArgumentException>(() =>
            _session.ResolveLayerId("WRONG"));

        // The error message should hint at valid layer names
        Assert.That(ex!.Message, Does.Contain("PURPOSE").Or.Contain("COMPONENT"));
    }
}
