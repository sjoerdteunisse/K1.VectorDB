using System.Net.Http.Headers;
using System.Text.Json;

namespace K1.VectorDB.Engine.EmbeddingProviders.LMStudio;

public class LMStudioEmbedder : IEmbedder
{
    public string URL { get; set; } = "http://localhost:1234/v1/embeddings";
    public string Model { get; set; } = "text-embedding-qwen3-embedding-0.6b";


    public double[] GetVector(string document)
    {
        EmbeddingRequest er = new()
        {
            input = document,
            model = Model
        };

        try
        {
            using var client = new HttpClient();
            var request = new HttpRequestMessage(HttpMethod.Post, URL);
            request.Content = new StringContent(JsonSerializer.Serialize(er));
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

            var httpResponse = client.Send(request);
            httpResponse.EnsureSuccessStatusCode();
            var responseBody = httpResponse.Content.ReadAsStringAsync();
            responseBody.Wait();
            var responseJson = responseBody.Result;

            var response = JsonSerializer.Deserialize<EmbeddingResponse>(responseJson)!;

            var vector = response.data[0].embedding.ToArray();
            return vector;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }

    public double[][] GetVectors(string[] documents)
    {
        List<double[]> vectors = [];

        foreach (var document in documents) vectors.Add(GetVector(document));

        return vectors.ToArray();
    }
}