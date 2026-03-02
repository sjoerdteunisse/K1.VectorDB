using K1.VectorDB.Engine.EmbeddingProviders;

namespace K1.VectorDB.Engine;

public class VectorDb
{
    public delegate string? DocumentPostprocessor(string line, string? path = null, int? originatedLineNumber = null);

    public delegate string? DocumentPreprocessor(string line, string? path = null, int? originatedLineNumber = null);

    private readonly IEmbedder _embedder;
    private readonly Dictionary<string, VectorDbIndex> _indexes;

    public readonly string DatabasePath;

    private readonly ulong _autoIndexCount;

    public VectorDb(IEmbedder embedder, string path, int autoIndexCount = 0)
    {
        _embedder = embedder;
        _indexes = new Dictionary<string, VectorDbIndex>();
        DatabasePath = path;

        if (autoIndexCount > 0)
        {
            _autoIndexCount = (ulong)autoIndexCount;
            for (ulong i = 0; i < _autoIndexCount; i++) CreateIndex($"IX{i}");
        }
        else
        {
            CreateIndex("Default");
        }
    }

    public List<string> IndexNames => _indexes.Keys.ToList();

    public bool CreateIndex(string name)
    {
        if (_indexes.ContainsKey(name)) return false;

        var index = new VectorDbIndex(name);
        _indexes.Add(name, index);

        return true;
    }

    public bool DeleteIndex(string name)
    {
        return _indexes.Remove(name);
    }

    public bool IndexDocument(string indexName, string document, DocumentPreprocessor? preprocessor = null,
        DocumentPostprocessor? postprocessor = null)
    {
        return IndexDocument(document, preprocessor, postprocessor, indexName);
    }

    public bool IndexDocument(string document, DocumentPreprocessor? preprocessor = null,
        DocumentPostprocessor? postprocessor = null, string? indexName = null)
    {
        string IndexName;

        if (indexName != null)
        {
            if (!_indexes.ContainsKey(indexName)) return false;
            IndexName = indexName;
        }
        else if (indexName == null && _autoIndexCount == 0)
        {
            IndexName = _indexes.First().Key;
        }
        else
        {
            var hash = StringHash(document);
            var bucket = hash % _autoIndexCount;
            IndexName = $"IX_{bucket}";
        }

        var index = _indexes[IndexName];


        var line = document;
        if (preprocessor != null)
        {
            line = preprocessor(document);
            if (line == null) return false;
        }

        if (postprocessor != null)
        {
            var postDoc = postprocessor(document);
            if (postDoc == null) return false;
            var doc = new VectorDbDocument(postDoc);
            var vector = _embedder.GetVector(line);
            index.Add(vector, doc);
            return true;
        }
        else
        {
            var doc = new VectorDbDocument(line);
            var vector = _embedder.GetVector(line);
            index.Add(vector, doc);
            return true;
        }
    }


    public bool IndexDocumentFile(
        string indexName,
        string documentPath,
        DocumentPreprocessor? preprocessor = null,
        DocumentPostprocessor? postprocessor = null)
    {
        return IndexDocumentFile(documentPath, preprocessor, postprocessor, indexName);
    }

    private bool IndexDocumentFile(
        string documentPath,
        DocumentPreprocessor? preprocessor = null,
        DocumentPostprocessor? postprocessor = null,
        string? indexName = null)
    {
        if (!File.Exists(documentPath)) return false;

        var lines = File.ReadAllLines(documentPath);

        for (var i = 0; i < lines.Length; i++)
        {
            string ixName;
            if (indexName != null)
            {
                if (!_indexes.ContainsKey(indexName)) return false;
                ixName = indexName;
            }
            else if (indexName == null && _autoIndexCount == 0)
            {
                ixName = _indexes.First().Key;
            }
            else
            {
                var hash = StringHash(lines[i]);
                var bucket = hash % _autoIndexCount;
                ixName = $"IX_{bucket}";
            }

            var index = _indexes[ixName];

            var line = lines[i];

            if (preprocessor != null)
            {
                line = preprocessor(lines[i], documentPath, i);
                if (line == null) continue;
            }

            if (postprocessor != null)
            {
                var postDoc = postprocessor(lines[i], documentPath, i);
                if (postDoc == null) return false;
                var doc = new VectorDbDocument(postDoc);
                var vector = _embedder.GetVector(line);
                index.Add(vector, doc);
            }
            else
            {
                var doc = new VectorDbDocument(line);
                var vector = _embedder.GetVector(line);
                index.Add(vector, doc);
            }
        }

        return true;
    }

    public bool IndexDocument(string indexName, string content, string id)
    {
        if (!_indexes.TryGetValue(indexName, out var index)) return false;
        var doc = new VectorDbDocument(id, content);
        var vector = _embedder.GetVector(content);
        index.Add(vector, doc);
        return true;
    }

    public bool DeleteDocument(string id)
    {
        var deleted = false;
        foreach (var index in _indexes.Values)
            if (index.Remove(id)) deleted = true;
        return deleted;
    }

    public VectorDbQueryResult QueryCosineSimilarity(string query, string indexName, int topK = 5)
    {
        if (!_indexes.TryGetValue(indexName, out var index))
            return new VectorDbQueryResult([], []);
        var vector = _embedder.GetVector(query);
        return index.QueryCosineSimilarity(vector, topK);
    }

    public bool DeleteDocument(double[] vector)
    {
        var success = false;

        foreach (var index in _indexes.Values)
            try
            {
                index.Remove(vector);
                success = true;
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($"Error removing vector from index {index}: {e.Message}");
            }

        return success;
    }

    public void Save()
    {
        if (!Directory.Exists(DatabasePath)) Directory.CreateDirectory(DatabasePath);
        var indexfile = Path.Combine(DatabasePath, "indexs.txt");
        var sw = new StreamWriter(indexfile, false);
        foreach (var index in _indexes)
        {
            sw.WriteLine(index.Value.Name);
            index.Value.Save(DatabasePath);
        }

        sw.Close();

        Console.WriteLine("Index Usage:");
        foreach (var key in _indexes.Keys) Console.Write($"{_indexes[key].Count,4}");
        Console.WriteLine();
    }

    public void Load()
    {
        var indexfile = Path.Combine(DatabasePath, "indexs.txt");
        var sr = new StreamReader(indexfile);
        while (!sr.EndOfStream)
        {
            var line = sr.ReadLine();
            if (line is null) continue;
            var index = new VectorDbIndex(line);
            index.Load(DatabasePath);
            if (!_indexes.ContainsKey(index.Name))
                _indexes.Add(index.Name, index);
            else
                _indexes[index.Name] = index;
        }

        sr.Close();
    }

    public VectorDbQueryResult QueryCosineSimilarity(string query, int topK = 5)
    {
        var vector = _embedder.GetVector(query);
        var results = new List<VectorDbQueryResult>();

        Parallel.ForEach(_indexes, index =>
        {
            var result = index.Value.QueryCosineSimilarity(vector, topK);
            results.Add(result);
        });
        
        var docs = new List<VectorDbDocument>();
        var distances = new List<double>();
        
        foreach (var result in results)
        {
            docs.AddRange(result.Documents);
            distances.AddRange(result.Distances);
        }

        var sorted =
            distances.Select((x, i) => new KeyValuePair<double, VectorDbDocument>(x, docs[i]))
                .OrderByDescending(x => x.Key).ToList().Take(topK);

        var newresult =
            new VectorDbQueryResult(sorted.Select(x => x.Value).ToList(), sorted.Select(x => x.Key).ToList());
        return newresult;
    }

    private static ulong StringHash(string text)
    {
        var hashedValue = 3074457345618258791ul;
        for (var i = 0; i < text.Length; i++) hashedValue = (hashedValue + text[i]) * 3074457345618258799ul;
        return hashedValue;
    }
}