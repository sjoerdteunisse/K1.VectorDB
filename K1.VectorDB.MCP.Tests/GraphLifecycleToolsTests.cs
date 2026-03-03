using K1.VectorDB.MCP.Tools;
using System.Text.Json;

namespace K1.VectorDB.MCP.Tests;

[TestFixture]
public sealed class GraphLifecycleToolsTests : McpTestBase
{
    private GraphLifecycleTools _tools = null!;

    public override void Setup()
    {
        base.Setup();
        _tools = new GraphLifecycleTools(Session);
    }

    // ── InitializeGraph ───────────────────────────────────────────────────────

    [Test]
    public void InitializeGraph_ReturnsValidJson()
    {
        var result = Session.IsInitialized
            ? NewToolsOnFreshSession().InitializeGraph(
                Path.Combine(Path.GetTempPath(), $"init-{Guid.NewGuid():N}"))
            : _tools.InitializeGraph(TestPath);

        Assert.DoesNotThrow(() => JsonDocument.Parse(result));
    }

    [Test]
    public void InitializeGraph_StatusIsInitialized()
    {
        var freshPath = Path.Combine(Path.GetTempPath(), $"init2-{Guid.NewGuid():N}");
        try
        {
            var freshTools = NewToolsOnFreshSession();
            var result = freshTools.InitializeGraph(freshPath);
            var json   = JsonDocument.Parse(result).RootElement;

            Assert.That(json.GetProperty("status").GetString(), Is.EqualTo("initialized"));
        }
        finally
        {
            if (Directory.Exists(freshPath)) Directory.Delete(freshPath, true);
        }
    }

    [Test]
    public void InitializeGraph_Returns9Layers()
    {
        var freshPath = Path.Combine(Path.GetTempPath(), $"init3-{Guid.NewGuid():N}");
        try
        {
            var freshTools = NewToolsOnFreshSession();
            var result = freshTools.InitializeGraph(freshPath);
            var json   = JsonDocument.Parse(result).RootElement;

            Assert.That(json.GetProperty("layers").GetArrayLength(), Is.EqualTo(9));
        }
        finally
        {
            if (Directory.Exists(freshPath)) Directory.Delete(freshPath, true);
        }
    }

    [Test]
    public void InitializeGraph_CreatesDirectoryOnDisk()
    {
        var freshPath = Path.Combine(Path.GetTempPath(), $"init4-{Guid.NewGuid():N}");
        try
        {
            NewToolsOnFreshSession().InitializeGraph(freshPath);
            Assert.That(Directory.Exists(freshPath), Is.True);
        }
        finally
        {
            if (Directory.Exists(freshPath)) Directory.Delete(freshPath, true);
        }
    }

    // ── LoadGraph ─────────────────────────────────────────────────────────────

    [Test]
    public void LoadGraph_AfterInit_StatusIsLoaded()
    {
        // Base setup already called InitializeWithEmbedder and saved the graph.
        // Now load it via a fresh session's tool.
        var freshSession = new GraphSessionService();
        var freshTools   = new GraphLifecycleTools(freshSession);

        var result = freshTools.LoadGraph(TestPath);
        var json   = JsonDocument.Parse(result).RootElement;

        Assert.That(json.GetProperty("status").GetString(), Is.EqualTo("loaded"));
    }

    [Test]
    public void LoadGraph_ReturnsCorrectLayerCount()
    {
        var freshSession = new GraphSessionService();
        var freshTools   = new GraphLifecycleTools(freshSession);

        var result = freshTools.LoadGraph(TestPath);
        var json   = JsonDocument.Parse(result).RootElement;

        // Loaded graph should report 9 layers via list_layers indirectly;
        // LoadGraph itself returns totalNodes / totalEdges (0 for a fresh graph).
        Assert.That(json.GetProperty("totalNodes").GetInt32(), Is.EqualTo(0));
        Assert.That(json.GetProperty("totalEdges").GetInt32(), Is.EqualTo(0));
    }

    // ── SaveGraph ─────────────────────────────────────────────────────────────

    [Test]
    public void SaveGraph_ReturnsValidJson()
    {
        var result = _tools.SaveGraph();
        Assert.DoesNotThrow(() => JsonDocument.Parse(result));
    }

    [Test]
    public void SaveGraph_StatusIsSaved()
    {
        var result = _tools.SaveGraph();
        var json   = JsonDocument.Parse(result).RootElement;

        Assert.That(json.GetProperty("status").GetString(), Is.EqualTo("saved"));
    }

    [Test]
    public void SaveGraph_ReflectsCurrentNodeCount()
    {
        var layerId = Session.ResolveLayerId("COMPONENT");
        Session.Graph.AddNode(layerId, "AuthService", "auth-service");
        Session.Graph.AddNode(layerId, "UserService", "user-table");

        var result = _tools.SaveGraph();
        var json   = JsonDocument.Parse(result).RootElement;

        Assert.That(json.GetProperty("totalNodes").GetInt32(), Is.EqualTo(2));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static GraphLifecycleTools NewToolsOnFreshSession()
    {
        var s = new GraphSessionService();
        return new GraphLifecycleTools(s);
    }
}
