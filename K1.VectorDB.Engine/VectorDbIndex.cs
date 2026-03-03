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

        try
        {
            if (!Directory.Exists(savepath)) Directory.CreateDirectory(savepath);

            var vectorsBytes = MessagePackSerializer.Serialize(vectors, options);
            DataFileHelper.WriteWithChecksum(Path.Combine(savepath, "vectors.bin"), vectorsBytes);

            var documentsBytes = MessagePackSerializer.Serialize(_documents, options);
            DataFileHelper.WriteWithChecksum(Path.Combine(savepath, "documents.bin"), documentsBytes);
        }
        catch (Exception ex) when (ex is not IOException)
        {
            throw new IOException($"Failed to save index '{Name}' to '{savepath}': {ex.Message}", ex);
        }

        _fileValid = true;
    }

    public void Load(string path)
    {
        var loadpath = Path.Combine(path, Name);

        if (!Directory.Exists(loadpath))
            throw new DirectoryNotFoundException($"Index directory not found: '{loadpath}'.");

        List<double[]> loadedVectors;
        List<VectorDbDocument> loadedDocuments;

        try
        {
            var vectorsBytes = DataFileHelper.ReadAndVerifyChecksum(Path.Combine(loadpath, "vectors.bin"));
            loadedVectors = MessagePackSerializer.Deserialize<List<double[]>>(vectorsBytes, options);
        }
        catch (InvalidDataException)
        {
            throw; // Propagate checksum / corruption errors as-is so callers can act on them.
        }
        catch (Exception ex)
        {
            throw new IOException($"Failed to load vectors for index '{Name}' from '{loadpath}': {ex.Message}", ex);
        }

        try
        {
            var docsBytes = DataFileHelper.ReadAndVerifyChecksum(Path.Combine(loadpath, "documents.bin"));
            loadedDocuments = MessagePackSerializer.Deserialize<List<VectorDbDocument>>(docsBytes, options);
        }
        catch (InvalidDataException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new IOException($"Failed to load documents for index '{Name}' from '{loadpath}': {ex.Message}", ex);
        }

        if (loadedVectors.Count != loadedDocuments.Count)
            throw new InvalidDataException(
                $"Index '{Name}' is inconsistent: {loadedVectors.Count} vectors but {loadedDocuments.Count} documents.");

        vectors = loadedVectors;
        _documents = loadedDocuments;
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

    public bool Remove(string documentId)
    {
        var index = _documents.FindIndex(d => d.Id == documentId);
        if (index == -1) return false;

        vectors.RemoveAt(index);
        _documents.RemoveAt(index);
        ResetCaches();
        return true;
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