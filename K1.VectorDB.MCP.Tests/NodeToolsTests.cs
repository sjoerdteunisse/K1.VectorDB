using K1.VectorDB.MCP.Tools;
using System.Text.Json;

namespace K1.VectorDB.MCP.Tests;

[TestFixture]
public sealed class NodeToolsTests : McpTestBase
{
    private NodeTools _tools = null!;

    public override void Setup()
    {
        base.Setup();
        _tools = new NodeTools(Session);
    }

    // ── AddNode ───────────────────────────────────────────────────────────────

    [Test]
    public void AddNode_ReturnsValidJson()
    {
        var result = _tools.AddNode("COMPONENT", "AuthService", "auth-service");
        Assert.DoesNotThrow(() => JsonDocument.Parse(result));
    }

    [Test]
    public void AddNode_ReturnsNodeId()
    {
        var result = _tools.AddNode("COMPONENT", "AuthService", "auth-service");
        var json   = JsonDocument.Parse(result).RootElement;

        var nodeId = json.GetProperty("nodeId").GetString();
        Assert.That(nodeId, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public void AddNode_ReturnsCorrectLabel()
    {
        var result = _tools.AddNode("COMPONENT", "AuthService", "auth-service");
        var json   = JsonDocument.Parse(result).RootElement;

        Assert.That(json.GetProperty("label").GetString(), Is.EqualTo("AuthService"));
    }

    [Test]
    public void AddNode_ReturnsCorrectLayerName()
    {
        var result = _tools.AddNode("DATA", "UserTable", "user-table");
        var json   = JsonDocument.Parse(result).RootElement;

        Assert.That(json.GetProperty("layerName").GetString(), Is.EqualTo("DATA"));
    }

    [Test]
    public void AddNode_NodeStoredInGraph()
    {
        var result  = _tools.AddNode("COMPONENT", "AuthService", "auth-service");
        var nodeId  = JsonDocument.Parse(result).RootElement
            .GetProperty("nodeId").GetString()!;

        var node = Session.Graph.GetNode(nodeId);
        Assert.That(node, Is.Not.Null);
        Assert.That(node!.Label, Is.EqualTo("AuthService"));
        Assert.That(node.Content, Is.EqualTo("auth-service"));
    }

    [Test]
    public void AddNode_WithMetadata_StoresMetadata()
    {
        var result = _tools.AddNode(
            "CLASS", "IAuthService", "auth-service",
            metadataJson: """{"file":"src/auth.cs","line":"10"}""");

        var nodeId = JsonDocument.Parse(result).RootElement
            .GetProperty("nodeId").GetString()!;

        var node = Session.Graph.GetNode(nodeId);
        Assert.That(node!.Metadata.ContainsKey("file"), Is.True);
        Assert.That(node.Metadata["file"], Is.EqualTo("src/auth.cs"));
        Assert.That(node.Metadata["line"], Is.EqualTo("10"));
    }

    [Test]
    public void AddNode_WithInvalidMetadataJson_FallsBackToRaw()
    {
        // Malformed JSON should not throw — falls back to a raw entry
        Assert.DoesNotThrow(() =>
            _tools.AddNode("COMPONENT", "X", "node-a", metadataJson: "not-json{{"));
    }

    [Test]
    public void AddNode_CaseInsensitiveLayerName()
    {
        // Both "component" and "COMPONENT" should resolve to the same layer
        Assert.DoesNotThrow(() =>
            _tools.AddNode("component", "SvcA", "node-a"));
    }

    [Test]
    public void AddNode_InvalidLayerName_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            _tools.AddNode("UNKNOWN_LAYER", "X", "auth-service"));
    }

    // ── GetNode ───────────────────────────────────────────────────────────────

    [Test]
    public void GetNode_ExistingNode_ReturnsLabel()
    {
        var addResult = _tools.AddNode("COMPONENT", "AuthService", "auth-service");
        var nodeId    = JsonDocument.Parse(addResult).RootElement
            .GetProperty("nodeId").GetString()!;

        var getResult = _tools.GetNode(nodeId);
        var json      = JsonDocument.Parse(getResult).RootElement;

        Assert.That(json.GetProperty("label").GetString(), Is.EqualTo("AuthService"));
    }

    [Test]
    public void GetNode_ExistingNode_ReturnsContent()
    {
        var addResult = _tools.AddNode("DATA", "UserTable", "user-table");
        var nodeId    = JsonDocument.Parse(addResult).RootElement
            .GetProperty("nodeId").GetString()!;

        var getResult = _tools.GetNode(nodeId);
        var json      = JsonDocument.Parse(getResult).RootElement;

        Assert.That(json.GetProperty("content").GetString(), Is.EqualTo("user-table"));
    }

    [Test]
    public void GetNode_ExistingNode_ReturnsLayers()
    {
        var addResult = _tools.AddNode("COMPONENT", "AuthService", "auth-service");
        var nodeId    = JsonDocument.Parse(addResult).RootElement
            .GetProperty("nodeId").GetString()!;

        var getResult = _tools.GetNode(nodeId);
        var layers    = JsonDocument.Parse(getResult).RootElement
            .GetProperty("layers").EnumerateArray()
            .Select(e => e.GetString())
            .ToList();

        Assert.That(layers, Does.Contain("COMPONENT"));
    }

    [Test]
    public void GetNode_NonExistentId_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            _tools.GetNode("00000000-0000-0000-0000-000000000000"));
    }

    // ── GetNodesInLayer ───────────────────────────────────────────────────────

    [Test]
    public void GetNodesInLayer_EmptyLayer_ReturnsZeroCount()
    {
        var result = _tools.GetNodesInLayer("DEPLOY");
        var json   = JsonDocument.Parse(result).RootElement;

        Assert.That(json.GetProperty("nodeCount").GetInt32(), Is.EqualTo(0));
    }

    [Test]
    public void GetNodesInLayer_AfterAddingNodes_ReturnsCorrectCount()
    {
        _tools.AddNode("COMPONENT", "AuthService", "auth-service");
        _tools.AddNode("COMPONENT", "UserService", "user-table");

        var result = _tools.GetNodesInLayer("COMPONENT");
        var json   = JsonDocument.Parse(result).RootElement;

        Assert.That(json.GetProperty("nodeCount").GetInt32(), Is.EqualTo(2));
    }

    [Test]
    public void GetNodesInLayer_DoesNotReturnNodesFromOtherLayers()
    {
        _tools.AddNode("COMPONENT", "AuthService", "auth-service");
        _tools.AddNode("DATA",      "UserTable",   "user-table");

        var result = _tools.GetNodesInLayer("DATA");
        var nodes  = JsonDocument.Parse(result).RootElement
            .GetProperty("nodes").EnumerateArray()
            .Select(n => n.GetProperty("label").GetString())
            .ToList();

        Assert.That(nodes, Does.Contain("UserTable"));
        Assert.That(nodes, Does.Not.Contain("AuthService"));
    }

    [Test]
    public void GetNodesInLayer_ReturnsLayerNameInResponse()
    {
        var result = _tools.GetNodesInLayer("FLOW");
        var json   = JsonDocument.Parse(result).RootElement;

        Assert.That(json.GetProperty("layerName").GetString(), Is.EqualTo("FLOW"));
    }
}
