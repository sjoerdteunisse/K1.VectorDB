using K1.VectorDB.MCP.Tools;
using System.Text.Json;

namespace K1.VectorDB.MCP.Tests;

[TestFixture]
public sealed class EdgeToolsTests : McpTestBase
{
    private EdgeTools  _tools     = null!;
    private NodeTools  _nodeTools = null!;

    public override void Setup()
    {
        base.Setup();
        _tools     = new EdgeTools(Session);
        _nodeTools = new NodeTools(Session);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private string AddNode(string layer, string label, string content) =>
        JsonDocument.Parse(_nodeTools.AddNode(layer, label, content))
            .RootElement.GetProperty("nodeId").GetString()!;

    // ── AddRelation ───────────────────────────────────────────────────────────

    [Test]
    public void AddRelation_ReturnsValidJson()
    {
        var src = AddNode("COMPONENT", "AuthService", "auth-service");
        var tgt = AddNode("DATA",      "UserTable",   "user-table");

        var result = _tools.AddRelation(src, tgt, "queries");
        Assert.DoesNotThrow(() => JsonDocument.Parse(result));
    }

    [Test]
    public void AddRelation_ReturnsEdgeId()
    {
        var src = AddNode("COMPONENT", "AuthService", "auth-service");
        var tgt = AddNode("DATA",      "UserTable",   "user-table");

        var result = _tools.AddRelation(src, tgt, "queries");
        var edgeId = JsonDocument.Parse(result).RootElement
            .GetProperty("edgeId").GetString();

        Assert.That(edgeId, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public void AddRelation_ResponseContainsCorrectRelationLabel()
    {
        var src = AddNode("COMPONENT", "AuthService", "auth-service");
        var tgt = AddNode("DATA",      "UserTable",   "user-table");

        var result   = _tools.AddRelation(src, tgt, "calls");
        var relation = JsonDocument.Parse(result).RootElement
            .GetProperty("relation").GetString();

        Assert.That(relation, Is.EqualTo("calls"));
    }

    [Test]
    public void AddRelation_DefaultsToDirected()
    {
        var src = AddNode("COMPONENT", "AuthService", "auth-service");
        var tgt = AddNode("DATA",      "UserTable",   "user-table");

        var result   = _tools.AddRelation(src, tgt, "owns");
        var directed = JsonDocument.Parse(result).RootElement
            .GetProperty("directed").GetBoolean();

        Assert.That(directed, Is.True);
    }

    [Test]
    public void AddRelation_UndirectedFlag_StoredCorrectly()
    {
        var src = AddNode("COMPONENT", "AuthService", "auth-service");
        var tgt = AddNode("COMPONENT", "UserService", "user-table");

        var result   = _tools.AddRelation(src, tgt, "collaborates", directed: false);
        var edgeId   = JsonDocument.Parse(result).RootElement
            .GetProperty("edgeId").GetString()!;

        var edge = Session.Graph.GetEdge(edgeId);
        Assert.That(edge!.IsDirected, Is.False);
    }

    [Test]
    public void AddRelation_WeightStoredCorrectly()
    {
        var src = AddNode("FLOW", "Publisher", "node-a");
        var tgt = AddNode("FLOW", "Consumer",  "node-b");

        var result = _tools.AddRelation(src, tgt, "publishes", weight: 3.5);
        var edgeId = JsonDocument.Parse(result).RootElement
            .GetProperty("edgeId").GetString()!;

        var edge = Session.Graph.GetEdge(edgeId);
        Assert.That(edge!.Weight, Is.EqualTo(3.5).Within(0.001));
    }

    [Test]
    public void AddRelation_EdgeStoredInGraph()
    {
        var src    = AddNode("CONTAINER", "ApiGateway",  "api-gateway");
        var tgt    = AddNode("COMPONENT", "AuthService", "auth-service");
        var result = _tools.AddRelation(src, tgt, "routes-to");
        var edgeId = JsonDocument.Parse(result).RootElement
            .GetProperty("edgeId").GetString()!;

        Assert.That(Session.Graph.GetEdge(edgeId), Is.Not.Null);
    }

    // ── GetNeighbors ──────────────────────────────────────────────────────────

    [Test]
    public void GetNeighbors_ReturnsDirectNeighbor()
    {
        var src = AddNode("COMPONENT", "AuthService", "auth-service");
        var tgt = AddNode("DATA",      "UserTable",   "user-table");
        _tools.AddRelation(src, tgt, "queries");

        var result    = _tools.GetNeighbors(src);
        var neighbors = JsonDocument.Parse(result).RootElement
            .GetProperty("neighbors").EnumerateArray()
            .Select(n => n.GetProperty("nodeId").GetString())
            .ToList();

        Assert.That(neighbors, Does.Contain(tgt));
    }

    [Test]
    public void GetNeighbors_IsolatedNode_ReturnsEmpty()
    {
        var src    = AddNode("COMPONENT", "AuthService", "auth-service");
        var result = _tools.GetNeighbors(src);
        var count  = JsonDocument.Parse(result).RootElement
            .GetProperty("neighborCount").GetInt32();

        Assert.That(count, Is.EqualTo(0));
    }

    [Test]
    public void GetNeighbors_MultipleNeighbors_ReturnsAll()
    {
        var src  = AddNode("COMPONENT", "AuthService", "auth-service");
        var tgt1 = AddNode("DATA",      "UserTable",   "user-table");
        var tgt2 = AddNode("DATA",      "TokenTable",  "node-a");
        _tools.AddRelation(src, tgt1, "reads");
        _tools.AddRelation(src, tgt2, "writes");

        var result = _tools.GetNeighbors(src);
        var count  = JsonDocument.Parse(result).RootElement
            .GetProperty("neighborCount").GetInt32();

        Assert.That(count, Is.EqualTo(2));
    }

    // ── Traverse ──────────────────────────────────────────────────────────────

    [Test]
    public void Traverse_ReturnsReachableNodes()
    {
        var a = AddNode("COMPONENT", "A", "node-a");
        var b = AddNode("COMPONENT", "B", "node-b");
        var c = AddNode("COMPONENT", "C", "node-c");
        _tools.AddRelation(a, b, "link");
        _tools.AddRelation(b, c, "link");

        var result  = _tools.Traverse(a, maxDepth: 3);
        var nodeIds = JsonDocument.Parse(result).RootElement
            .GetProperty("nodes").EnumerateArray()
            .Select(n => n.GetProperty("nodeId").GetString())
            .ToList();

        Assert.That(nodeIds, Does.Contain(b));
        Assert.That(nodeIds, Does.Contain(c));
    }

    [Test]
    public void Traverse_MaxDepth1_DoesNotPassThroughIntermediate()
    {
        var a = AddNode("COMPONENT", "A", "node-a");
        var b = AddNode("COMPONENT", "B", "node-b");
        var c = AddNode("COMPONENT", "C", "node-c");
        _tools.AddRelation(a, b, "link");
        _tools.AddRelation(b, c, "link");

        var result  = _tools.Traverse(a, maxDepth: 1);
        var nodeIds = JsonDocument.Parse(result).RootElement
            .GetProperty("nodes").EnumerateArray()
            .Select(n => n.GetProperty("nodeId").GetString())
            .ToList();

        Assert.That(nodeIds, Does.Contain(b));
        Assert.That(nodeIds, Does.Not.Contain(c));
    }

    [Test]
    public void Traverse_MaxDepthAbove10_ClampedTo10()
    {
        var a = AddNode("COMPONENT", "A", "node-a");

        // Should not throw; maxDepth is clamped internally
        Assert.DoesNotThrow(() => _tools.Traverse(a, maxDepth: 999));
    }

    [Test]
    public void Traverse_ReturnsStartNodeIdInResponse()
    {
        var a      = AddNode("COMPONENT", "A", "node-a");
        var result = _tools.Traverse(a);
        var start  = JsonDocument.Parse(result).RootElement
            .GetProperty("startNodeId").GetString();

        Assert.That(start, Is.EqualTo(a));
    }
}
