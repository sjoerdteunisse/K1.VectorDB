using K1.VectorDB.Engine.EmbeddingProviders;

namespace K1.VectorDB.MCP.Tests;

/// <summary>
/// Deterministic embedder for tests. Maps known content strings to fixed unit vectors so
/// that semantic-search assertions are predictable without a live LM Studio instance.
/// Unknown strings fall back to a uniform default vector.
/// </summary>
internal sealed class FakeEmbedder : IEmbedder
{
    // ── Known content vectors (orthogonal / near-orthogonal) ──────────────────
    private static readonly Dictionary<string, double[]> Vectors = new()
    {
        // Node content
        ["auth-service"]  = [1.0, 0.0, 0.0, 0.0],
        ["user-table"]    = [0.0, 1.0, 0.0, 0.0],
        ["api-gateway"]   = [0.0, 0.0, 1.0, 0.0],
        ["deploy-node"]   = [0.0, 0.0, 0.0, 1.0],
        ["node-a"]        = [0.9, 0.1, 0.0, 0.0],
        ["node-b"]        = [0.1, 0.9, 0.0, 0.0],
        ["node-c"]        = [0.0, 0.1, 0.9, 0.0],
        // Query vectors
        ["query:auth"]    = [0.95, 0.05, 0.0, 0.0],
        ["query:user"]    = [0.05, 0.95, 0.0, 0.0],
        ["query:api"]     = [0.0, 0.05, 0.95, 0.0],
    };

    private static readonly double[] Default = [0.25, 0.25, 0.25, 0.25];

    public double[] GetVector(string text) =>
        Vectors.TryGetValue(text, out var v) ? v : Default;

    public double[][] GetVectors(string[] texts) =>
        texts.Select(GetVector).ToArray();
}
