using System.Collections.Concurrent;
using K1.VectorDB.Engine.Helpers;
using MessagePack;

namespace K1.VectorDB.Engine;

internal class VectorDbIndex(string name)
{
    public readonly string Name = name;
    public int Count => _documents.Count;

    private bool _fileValid;
    private List<VectorDbDocument> _documents = new();
    private readonly Dictionary<double[], VectorDbQueryResult> _queryCacheCosineSimilarity = new();

    private readonly MessagePackSerializerOptions options = MessagePackSerializerOptions.Standard
        .WithSecurity(MessagePackSecurity.UntrustedData)
        .WithCompression(MessagePackCompression.Lz4BlockArray);

    private List<double[]> vectors = new();

    public void Save(string path)
    {
        if (_fileValid) return;
        var savepath = Path.Combine(path, Name);
        if (!Directory.Exists(savepath)) Directory.CreateDirectory(savepath);

        var vectorsBytes = MessagePackSerializer.Serialize(vectors, options);
        File.WriteAllBytes(Path.Combine(savepath, "vectors.bin"), vectorsBytes);

        var documentsBytes = MessagePackSerializer.Serialize(_documents, options);
        File.WriteAllBytes(Path.Combine(savepath, "documents.bin"), documentsBytes);

        _fileValid = true;
    }

    public void Load(string path)
    {
        var loadpath = Path.Combine(path, Name);

        if (!Directory.Exists(loadpath)) throw new DirectoryNotFoundException($"Directory {loadpath} not found.");

        var vectorsBytes = File.ReadAllBytes(Path.Combine(loadpath, "vectors.bin"));
        vectors = MessagePackSerializer.Deserialize<List<double[]>>(vectorsBytes, options);

        var docsBytes = File.ReadAllBytes(Path.Combine(loadpath, "documents.bin"));
        _documents = MessagePackSerializer.Deserialize<List<VectorDbDocument>>(docsBytes, options);

        _fileValid = true;
    }

    public void Add(double[] vector, VectorDbDocument doc)
    {
        ArgumentNullException.ThrowIfNull(vector);
        ArgumentNullException.ThrowIfNull(doc);

        if (vector.Length == 0) throw new ArgumentException("Vector length cannot be zero", nameof(vector));

        vectors.Add(vector);
        _documents.Add(doc);
        ResetCaches();
    }

    public void Remove(VectorDbDocument doc)
    {
        ArgumentNullException.ThrowIfNull(doc);

        var index = _documents.IndexOf(doc);

        if (index == -1) throw new ArgumentException("Document not found.", nameof(doc));

        vectors.RemoveAt(index);
        _documents.RemoveAt(index);
        ResetCaches();
    }

    public void Remove(double[] vector)
    {
        ArgumentNullException.ThrowIfNull(vector);
        var index = vectors.IndexOf(vector);

        if (index == -1) throw new ArgumentException("Vector not found.", nameof(vector));

        vectors.RemoveAt(index);
        _documents.RemoveAt(index);
        ResetCaches();
    }

    public void RemoveAt(int index)
    {
        if (index < 0 || index >= _documents.Count) throw new ArgumentOutOfRangeException(nameof(index));

        vectors.RemoveAt(index);
        _documents.RemoveAt(index);
        ResetCaches();
    }

    public void Clear()
    {
        vectors.Clear();
        _documents.Clear();

        ResetCaches();
    }

    private void ResetCaches()
    {
        _queryCacheCosineSimilarity.Clear();
        _fileValid = false;
    }

    private VectorDbQueryResult? TryHitCacheCosineSimilarity(double[] queryVector, int topK = 5)
    {
        if (_queryCacheCosineSimilarity.TryGetValue(queryVector, out var result) && result.Documents.Count >= topK)
        {
            if (result.Documents.Count > topK)
            {
                result.Documents = result.Documents.Take(topK).ToList();
                result.Distances = result.Distances.Take(topK).ToList();
            }

            return result;
        }

        return null;
    }

    public VectorDbQueryResult QueryCosineSimilarity(double[] queryVector, int topK = 5)
    {
        ArgumentNullException.ThrowIfNull(queryVector);

        if (topK <= 0)
            throw new ArgumentException("Number of results requested (k) must be greater than zero.", nameof(topK));

        var cachedResult = TryHitCacheCosineSimilarity(queryVector, topK);

        if (cachedResult != null) return cachedResult;

        var similarities = new ConcurrentBag<KeyValuePair<VectorDbDocument, double>>();

        Parallel.For(0, vectors.Count, i =>
        {
            var similarity = SimilarityMath.CosineSimilarity(queryVector, vectors[i]);
            similarities.Add(new KeyValuePair<VectorDbDocument, double>(_documents[i], similarity));
        });

        var orderedData = similarities
            .OrderByDescending(pair => pair.Value)
            .Take(topK)
            .ToList();

        return new VectorDbQueryResult(
            orderedData.Select(pair => pair.Key).ToList(),
            orderedData.Select(pair => pair.Value).ToList()
        );
    }

    public VectorDbQueryResult QueryEuclideanDistance(double[] queryVector, int topK = 5)
    {
        ArgumentNullException.ThrowIfNull(queryVector);

        if (topK <= 0)
            throw new ArgumentException("Number of results requested (k) must be greater than zero.", nameof(topK));

        var similarities = new ConcurrentBag<KeyValuePair<VectorDbDocument, double>>();

        Parallel.For(0, vectors.Count, i =>
        {
            var similarity = 1 - SimilarityMath.EuclideanDistance(queryVector, vectors[i]);
            similarities.Add(new KeyValuePair<VectorDbDocument, double>(_documents[i], similarity));
        });

        var orderedData = similarities
            .OrderByDescending(pair => pair.Value)
            .Take(topK)
            .ToList();

        return new VectorDbQueryResult(
            orderedData.Select(pair => pair.Key).ToList(),
            orderedData.Select(pair => pair.Value).ToList()
        );
    }
}