namespace K1.VectorDB.MCP.Tests;

/// <summary>
/// Base class that bootstraps a <see cref="GraphSessionService"/> backed by a
/// <see cref="FakeEmbedder"/> for each test. Derived test fixtures inherit the
/// pre-initialized session and a unique, cleaned-up temp directory.
/// </summary>
internal abstract class McpTestBase
{
    protected string TestPath = null!;
    protected GraphSessionService Session = null!;

    [SetUp]
    public virtual void Setup()
    {
        // Unique directory per test run — avoids cross-test pollution.
        TestPath = Path.Combine(
            Path.GetTempPath(),
            $"mcp-test-{GetType().Name}-{Guid.NewGuid():N}");

        Session = new GraphSessionService();
        Session.InitializeWithEmbedder(TestPath, new FakeEmbedder());
    }

    [TearDown]
    public virtual void Teardown()
    {
        if (Directory.Exists(TestPath))
            Directory.Delete(TestPath, recursive: true);
    }
}
