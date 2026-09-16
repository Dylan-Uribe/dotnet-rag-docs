using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Rag.API.Options;

namespace Rag.API.Embeddings;

public sealed class EmbeddingService(
    IEmbeddingGenerator<string, Embedding<float>> generator,
    IOptions<OpenAIOptions> options) : IEmbeddingService
{
    private readonly int _dimensions = options.Value.EmbeddingDimensions;
    private readonly int _batchSize = options.Value.EmbeddingBatchSize;

    public async Task<IReadOnlyList<float[]>> EmbedAsync(IReadOnlyList<string> texts)
    {
        if (texts.Count == 0) return [];

        var generationOptions = new EmbeddingGenerationOptions { Dimensions = _dimensions };
        var vectors = new List<float[]>(texts.Count);

        foreach (string[] batch in texts.Chunk(_batchSize))
        {
            GeneratedEmbeddings<Embedding<float>> response =
                await generator.GenerateAsync(batch, generationOptions);

            foreach (Embedding<float> embedding in response)
            {
                float[] vector = embedding.Vector.ToArray();

                if (vector.Length != _dimensions)
                {
                    throw new InvalidOperationException(
                        $"The embedding model returned {vector.Length} dimensions but " +
                        $"{_dimensions} were configured (OpenAI:EmbeddingDimensions). The database " +
                        "vector column and the configured dimension must match; to change the model, " +
                        "update the dimension and add a matching migration.");
                }

                vectors.Add(vector);
            }
        }

        return vectors;
    }
}
