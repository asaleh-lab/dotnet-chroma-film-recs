using OpenAI.Embeddings;

namespace Lantern.Core;

public sealed class Embedder
{
    public const string Model = "text-embedding-3-small";

    private readonly EmbeddingClient _client;

    public Embedder(string apiKey)
    {
        _client = new EmbeddingClient(Model, apiKey);
    }

    public async Task<float[][]> EmbedAsync(IReadOnlyList<string> texts)
    {
        if (texts.Count == 0)
        {
            return [];
        }

        OpenAIEmbeddingCollection result = await _client.GenerateEmbeddingsAsync(texts);
        var byIndex = result.ToDictionary(item => item.Index, item => item.ToFloats().ToArray());
        return Enumerable.Range(0, texts.Count).Select(index =>
        {
            if (!byIndex.TryGetValue(index, out var embedding))
            {
                throw new InvalidOperationException($"Embedding missing for index {index}.");
            }

            return embedding;
        }).ToArray();
    }
}
