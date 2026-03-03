using K1.VectorDB.MCP.Prompts;

namespace K1.VectorDB.MCP.Tests;

[TestFixture]
public sealed class RepositoryExplorerPromptTests
{
    // ── ExploreCodebase return value ──────────────────────────────────────────

    [Test]
    public void ExploreCodebase_ReturnsNonEmptyString()
    {
        var result = RepositoryExplorerPrompt.ExploreCodebase();
        Assert.That(result, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public void ExploreCodebase_ReturnsPromptText()
    {
        var result = RepositoryExplorerPrompt.ExploreCodebase();
        Assert.That(result, Is.EqualTo(RepositoryExplorerPrompt.PromptText));
    }

    // ── Prompt structural requirements ────────────────────────────────────────

    [Test]
    public void PromptText_ContainsPhase0()
    {
        Assert.That(RepositoryExplorerPrompt.PromptText, Does.Contain("PHASE 0"));
    }

    [Test]
    public void PromptText_ContainsPhase1()
    {
        Assert.That(RepositoryExplorerPrompt.PromptText, Does.Contain("PHASE 1"));
    }

    [Test]
    public void PromptText_ContainsPhase2()
    {
        Assert.That(RepositoryExplorerPrompt.PromptText, Does.Contain("PHASE 2"));
    }

    [Test]
    public void PromptText_ContainsAllAbstractionLayerLabels()
    {
        var labels = new[]
            { "PURPOSE", "CONTEXT", "CONTAINER", "COMPONENT",
              "FLOW", "DATA", "STATE", "DEPLOY", "CLASS" };

        foreach (var label in labels)
            Assert.That(RepositoryExplorerPrompt.PromptText, Does.Contain(label),
                $"Prompt must mention the '{label}' abstraction layer");
    }

    [Test]
    public void PromptText_ReferencesGraphTools()
    {
        // The prompt instructs the LLM to call add_node / add_relation
        Assert.That(RepositoryExplorerPrompt.PromptText, Does.Contain("add_node"));
        Assert.That(RepositoryExplorerPrompt.PromptText, Does.Contain("add_relation"));
    }

    [Test]
    public void PromptText_ContainsRepositoryDiscoverySteps()
    {
        // Phase 0 steps must be present
        Assert.That(RepositoryExplorerPrompt.PromptText, Does.Contain("Root Inventory"));
        Assert.That(RepositoryExplorerPrompt.PromptText, Does.Contain("Technology Fingerprint"));
        Assert.That(RepositoryExplorerPrompt.PromptText, Does.Contain("Entry Point Tracing"));
        Assert.That(RepositoryExplorerPrompt.PromptText, Does.Contain("Data Model Scan"));
        Assert.That(RepositoryExplorerPrompt.PromptText, Does.Contain("Cross-Service Call Graph"));
        Assert.That(RepositoryExplorerPrompt.PromptText, Does.Contain("Ambiguity Log"));
    }

    [Test]
    public void PromptText_ContainsMermaidDiagramInstructions()
    {
        Assert.That(RepositoryExplorerPrompt.PromptText, Does.Contain("Mermaid"));
    }

    [Test]
    public void PromptText_ContainsSrsDocumentInstructions()
    {
        Assert.That(RepositoryExplorerPrompt.PromptText, Does.Contain("SRS"));
    }

    [Test]
    public void PromptText_ContainsRoleDefinition()
    {
        Assert.That(RepositoryExplorerPrompt.PromptText, Does.Contain("# ROLE"));
    }

    [Test]
    public void PromptText_IsSubstantialLength()
    {
        // Sanity-check that the full prompt was not accidentally truncated
        Assert.That(RepositoryExplorerPrompt.PromptText.Length, Is.GreaterThan(2000));
    }
}
