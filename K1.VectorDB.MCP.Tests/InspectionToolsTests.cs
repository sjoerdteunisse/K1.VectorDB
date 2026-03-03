using K1.VectorDB.MCP.Tools;
using System.Text.Json;

namespace K1.VectorDB.MCP.Tests;

[TestFixture]
public sealed class InspectionToolsTests : McpTestBase
{
    private InspectionTools _tools     = null!;
    private NodeTools       _nodeTools = null!;

    public override void Setup()
    {
        base.Setup();
        _tools     = new InspectionTools(Session);
        _nodeTools = new NodeTools(Session);
    }

    // ── ListLayers ────────────────────────────────────────────────────────────

    [Test]
    public void ListLayers_ReturnsValidJson()
    {
        var result = _tools.ListLayers();
        Assert.DoesNotThrow(() => JsonDocument.Parse(result));
    }

    [Test]
    public void ListLayers_Returns9Layers()
    {
        var result = _tools.ListLayers();
        var count  = JsonDocument.Parse(result).RootElement
            .GetProperty("layerCount").GetInt32();

        Assert.That(count, Is.EqualTo(9));
    }

    [Test]
    public void ListLayers_AllStandardNamesPresent()
    {
        var result = _tools.ListLayers();
        var names  = JsonDocument.Parse(result).RootElement
            .GetProperty("layers")
            .EnumerateArray()
            .Select(l => l.GetProperty("name").GetString())
            .ToHashSet();

        foreach (var expected in new[]
            { "PURPOSE", "CONTEXT", "CONTAINER", "COMPONENT",
              "FLOW", "DATA", "STATE", "DEPLOY", "CLASS" })
        {
            Assert.That(names, Does.Contain(expected));
        }
    }

    [Test]
    public void ListLayers_FreshGraph_AllNodeCountsAreZero()
    {
        var result = _tools.ListLayers();
        var layers = JsonDocument.Parse(result).RootElement
            .GetProperty("layers").EnumerateArray();

        foreach (var layer in layers)
        {
            Assert.That(layer.GetProperty("nodeCount").GetInt32(), Is.EqualTo(0),
                $"Layer '{layer.GetProperty("name").GetString()}' should have 0 nodes");
        }
    }

    [Test]
    public void ListLayers_AfterAddingNode_NodeCountUpdated()
    {
        _nodeTools.AddNode("COMPONENT", "AuthService", "auth-service");
        _nodeTools.AddNode("COMPONENT", "UserService", "user-table");

        var result = _tools.ListLayers();
        var layers = JsonDocument.Parse(result).RootElement
            .GetProperty("layers").EnumerateArray()
            .ToDictionary(
                l => l.GetProperty("name").GetString()!,
                l => l.GetProperty("nodeCount").GetInt32());

        Assert.That(layers["COMPONENT"], Is.EqualTo(2));
        Assert.That(layers["DATA"],      Is.EqualTo(0));
    }

    [Test]
    public void ListLayers_ReportsTotalNodeCount()
    {
        _nodeTools.AddNode("COMPONENT", "AuthService", "auth-service");
        _nodeTools.AddNode("DATA",      "UserTable",   "user-table");

        var result = _tools.ListLayers();
        var total  = JsonDocument.Parse(result).RootElement
            .GetProperty("totalNodes").GetInt32();

        Assert.That(total, Is.EqualTo(2));
    }

    [Test]
    public void ListLayers_ReportsTotalEdgeCount()
    {
        var session  = Session;
        var compId   = session.ResolveLayerId("COMPONENT");
        var dataId   = session.ResolveLayerId("DATA");
        var authNode = session.Graph.AddNode(compId, "AuthService", "auth-service");
        var dbNode   = session.Graph.AddNode(dataId, "UserTable",   "user-table");
        session.Graph.AddEdge(authNode.Id, dbNode.Id, "queries");

        var result = _tools.ListLayers();
        var total  = JsonDocument.Parse(result).RootElement
            .GetProperty("totalEdges").GetInt32();

        Assert.That(total, Is.EqualTo(1));
    }

    [Test]
    public void ListLayers_EachLayerHasId()
    {
        var result = _tools.ListLayers();
        var layers = JsonDocument.Parse(result).RootElement
            .GetProperty("layers").EnumerateArray();

        foreach (var layer in layers)
        {
            var id = layer.GetProperty("id").GetString();
            Assert.That(id, Is.Not.Null.And.Not.Empty);
        }
    }

    [Test]
    public void ListLayers_EachLayerHasDescription()
    {
        var result = _tools.ListLayers();
        var layers = JsonDocument.Parse(result).RootElement
            .GetProperty("layers").EnumerateArray();

        foreach (var layer in layers)
        {
            var desc = layer.GetProperty("desc").GetString();
            Assert.That(desc, Is.Not.Null.And.Not.Empty,
                $"Layer '{layer.GetProperty("name")}' should have a description");
        }
    }
}
