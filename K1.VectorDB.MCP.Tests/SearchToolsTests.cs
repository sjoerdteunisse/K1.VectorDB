using K1.VectorDB.MCP.Tools;
using System.Text.Json;

namespace K1.VectorDB.MCP.Tests;

[TestFixture]
public sealed class SearchToolsTests : McpTestBase
{
    private SearchTools _tools     = null!;
    private NodeTools   _nodeTools = null!;

    public override void Setup()
    {
        base.Setup();
        _tools     = new SearchTools(Session);
        _nodeTools = new NodeTools(Session);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void AddNode(string layer, string label, string content) =>
        _nodeTools.AddNode(layer, label, content);

    // ── Basic search ──────────────────────────────────────────────────────────

    [Test]
    public void Search_ReturnsValidJson()
    {
        var result = _tools.Search("query:auth");
        Assert.DoesNotThrow(() => JsonDocument.Parse(result));
    }

    [Test]
    public void Search_EmptyGraph_ReturnsZeroResults()
    {
        var result = _tools.Search("query:auth", topK: 5);
        var count  = JsonDocument.Parse(result).RootElement
            .GetProperty("resultCount").GetInt32();

        Assert.That(count, Is.EqualTo(0));
    }

    [Test]
    public void Search_ReturnsTopMatchFirst()
    {
        // auth-service vector is [1,0,0,0]; query:auth is [0.95,0.05,0,0] — best match
        // user-table  vector is [0,1,0,0] — poor match for query:auth
        AddNode("COMPONENT", "AuthService", "auth-service");
        AddNode("DATA",      "UserTable",   "user-table");

        var result  = _tools.Search("query:auth", topK: 2);
        var results = JsonDocument.Parse(result).RootElement.GetProperty("results");
        var first   = results[0].GetProperty("label").GetString();

        Assert.That(first, Is.EqualTo("AuthService"));
    }

    [Test]
    public void Search_TopKLimitsResults()
    {
        AddNode("COMPONENT", "AuthService", "auth-service");
        AddNode("DATA",      "UserTable",   "user-table");
        AddNode("CONTAINER", "ApiGateway",  "api-gateway");

        var result = _tools.Search("query:auth", topK: 1);
        var count  = JsonDocument.Parse(result).RootElement
            .GetProperty("resultCount").GetInt32();

        Assert.That(count, Is.EqualTo(1));
    }

    [Test]
    public void Search_TopKAbove50_ClampedTo50()
    {
        // Should not throw; topK is clamped internally
        Assert.DoesNotThrow(() => _tools.Search("query:auth", topK: 9999));
    }

    [Test]
    public void Search_ResponseContainsQueryEcho()
    {
        var result = _tools.Search("query:auth");
        var query  = JsonDocument.Parse(result).RootElement
            .GetProperty("query").GetString();

        Assert.That(query, Is.EqualTo("query:auth"));
    }

    [Test]
    public void Search_ResultIncludesScoreField()
    {
        AddNode("COMPONENT", "AuthService", "auth-service");

        var result  = _tools.Search("query:auth", topK: 1);
        var results = JsonDocument.Parse(result).RootElement.GetProperty("results");

        Assert.That(results.GetArrayLength(), Is.GreaterThan(0));
        // Score field must exist and be a valid number
        var score = results[0].GetProperty("score").GetDouble();
        Assert.That(score, Is.GreaterThan(0.0));
    }

    // ── Layer-scoped search ───────────────────────────────────────────────────

    [Test]
    public void Search_ScopedToLayer_ReturnsOnlyFromThatLayer()
    {
        // Put the best match in COMPONENT but scope search to DATA
        AddNode("COMPONENT", "AuthService", "auth-service");
        AddNode("DATA",      "AuditLog",    "node-a");      // weak match for query:auth

        var result  = _tools.Search("query:auth", topK: 5, layerName: "DATA");
        var results = JsonDocument.Parse(result).RootElement.GetProperty("results");

        // AuthService (COMPONENT) must NOT appear — only DATA-layer nodes
        var labels = results.EnumerateArray()
            .Select(r => r.GetProperty("label").GetString())
            .ToList();

        Assert.That(labels, Does.Not.Contain("AuthService"));
    }

    [Test]
    public void Search_ScopedToLayer_FindsNodeInThatLayer()
    {
        AddNode("DATA",      "UserTable",  "user-table");
        AddNode("COMPONENT", "AuthService","auth-service");

        var result  = _tools.Search("query:user", topK: 1, layerName: "DATA");
        var results = JsonDocument.Parse(result).RootElement.GetProperty("results");

        Assert.That(results.GetArrayLength(), Is.GreaterThan(0));
        Assert.That(results[0].GetProperty("label").GetString(), Is.EqualTo("UserTable"));
    }

    [Test]
    public void Search_ScopedToLayer_ResponseReflectsLayerName()
    {
        var result     = _tools.Search("query:auth", layerName: "COMPONENT");
        var layerName  = JsonDocument.Parse(result).RootElement
            .GetProperty("layerName").GetString();

        Assert.That(layerName, Is.EqualTo("COMPONENT"));
    }

    [Test]
    public void Search_NoLayerScope_ResponseLayerNameIsAll()
    {
        var result    = _tools.Search("query:auth");
        var layerName = JsonDocument.Parse(result).RootElement
            .GetProperty("layerName").GetString();

        Assert.That(layerName, Is.EqualTo("all"));
    }

    [Test]
    public void Search_InvalidLayerName_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            _tools.Search("query:auth", layerName: "NONEXISTENT"));
    }
}
