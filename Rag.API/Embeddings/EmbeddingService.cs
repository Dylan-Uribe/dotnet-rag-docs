using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Rag.API.Options;

namespace Rag.API.Embeddings;

public sealed class EmbeddingService : IEmbeddingService
{
    private readonly IEmbeddingGenerator<string, Embedding<float>> _generator;
    private readonly int _dimensions;
    private readonly int _batchSize;

    public EmbeddingService(
        IEmbeddingGenerator<string, Embedding<float>> generator,
        IOptions<OpenAIOptions> options)
    {
        _generator = generator;
        _dimensions = options.Value.EmbeddingDimensions;
        _batchSize = options.Value.EmbeddingBatchSize;
    }

    public async Task<IReadOnlyList<float[]>> EmbedAsync(IReadOnlyList<string> texts)
    {
        if (texts.Count == 0) return [];

        var options = new EmbeddingGenerationOptions { Dimensions = _dimensions };
        var vectors = new List<float[]>(texts.Count);

        foreach (string[] batch in texts.Chunk(_batchSize))
        {
            GeneratedEmbeddings<Embedding<float>> response =
                await _generator.GenerateAsync(batch, options);

            foreach (var embedding in response)
            {
                var vector = embedding.Vector.ToArray();

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
