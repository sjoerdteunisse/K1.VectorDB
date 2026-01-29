using K1.VectorDB.Engine;
using K1.VectorDB.Engine.EmbeddingProviders.LMStudio;

var lmStudioEmbedder = new LMStudioEmbedder
{
    Model = "Qwen/Qwen3-Embedding-0.6B-GGUF"
};

var db = new VectorDb(lmStudioEmbedder, "data/vectors");

db.IndexDocument("document about dogs");
db.IndexDocument("document test");
db.IndexDocument("document dogs");

db.Save();

db.Load();

var res = db.QueryCosineSimilarity("document test123");

for (var i = 0; i < res.Documents.Count; i++)
    Console.WriteLine($"{res.Documents[i].DocumentContent} - Score: {res.Distances[i]}");

Console.WriteLine("Hello, World!");