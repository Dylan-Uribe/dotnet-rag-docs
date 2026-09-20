using Microsoft.Extensions.AI;

namespace Rag.Tests.TestSupport;

/// <summary>
/// Stands in for the provider behind <see cref="IEmbeddingGenerator{TInput,TEmbedding}"/>.
/// Records every batch it receives so tests can assert how the service called it.
/// </summary>
internal sealed class FakeEmbeddingGenerator(int dimensions = TestVectors.Dimensions)
    : IEmbeddingGenerator<string, Embedding<float>>
{
    public List<string[]> ReceivedBatches { get; } = [];
    public List<EmbeddingGenerationOptions?> ReceivedOptions { get; } = [];

    public int CallCount => ReceivedBatches.Count;

    public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
        IEnumerable<string> values,
        EmbeddingGenerationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        string[] batch = values.ToArray();

        ReceivedBatches.Add(batch);
        ReceivedOptions.Add(options);

        var embeddings = new GeneratedEmbeddings<Embedding<float>>(
            batch.Select(text => new Embedding<float>(TestVectors.For(text, dimensions))));

        return Task.FromResult(embeddings);
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose() { }
}
