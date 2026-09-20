using Microsoft.Extensions.Options;
using Rag.API.Embeddings;
using Rag.API.Options;
using Rag.Tests.TestSupport;

namespace Rag.Tests.Embeddings;

public class EmbeddingServiceTests
{
    private static EmbeddingService CreateService(
        FakeEmbeddingGenerator generator,
        int dimensions = TestVectors.Dimensions,
        int batchSize = 64)
    {
        var options = Options.Create(new OpenAIOptions
        {
            EmbeddingDimensions = dimensions,
            EmbeddingBatchSize = batchSize
        });

        return new EmbeddingService(generator, options);
    }

    // ---------- cost ----------

    [Fact]
    public async Task EmbedAsync_CallsNoProvider_WhenThereAreNoTexts()
    {
        // Every provider call is billed, so an empty ingest must not reach the network.
        var generator = new FakeEmbeddingGenerator();

        IReadOnlyList<float[]> vectors = await CreateService(generator).EmbedAsync([]);

        Assert.Empty(vectors);
        Assert.Equal(0, generator.CallCount);
    }

    [Fact]
    public async Task EmbedAsync_SplitsTheTextsIntoBatchesOfTheConfiguredSize()
    {
        // Providers cap how many inputs one request may carry; the batch size is
        // the knob that keeps requests under that cap.
        var generator = new FakeEmbeddingGenerator();

        await CreateService(generator, batchSize: 2).EmbedAsync(["a", "b", "c", "d", "e"]);

        Assert.Equal(3, generator.CallCount);
        Assert.Equal([2, 2, 1], generator.ReceivedBatches.Select(batch => batch.Length));
    }

    // ---------- contract with the caller ----------

    [Fact]
    public async Task EmbedAsync_ReturnsOneVectorPerText()
    {
        var generator = new FakeEmbeddingGenerator();

        IReadOnlyList<float[]> vectors = await CreateService(generator, batchSize: 2)
            .EmbedAsync(["a", "b", "c"]);

        Assert.Equal(3, vectors.Count);
    }

    [Fact]
    public async Task EmbedAsync_PreservesTheOrderOfTheTextsAcrossBatches()
    {
        // The ingestion service pairs chunks to vectors by position. If batching
        // reordered them, every chunk would be stored under another chunk's vector.
        string[] texts = ["alpha", "bravo", "charlie", "delta", "echo"];
        var generator = new FakeEmbeddingGenerator();

        IReadOnlyList<float[]> vectors = await CreateService(generator, batchSize: 2).EmbedAsync(texts);

        for (int i = 0; i < texts.Length; i++)
        {
            Assert.Equal(TestVectors.For(texts[i]), vectors[i]);
        }
    }

    [Fact]
    public async Task EmbedAsync_AsksTheProviderForTheConfiguredDimensions()
    {
        var generator = new FakeEmbeddingGenerator();

        await CreateService(generator, batchSize: 2).EmbedAsync(["a", "b", "c"]);

        Assert.All(
            generator.ReceivedOptions,
            options => Assert.Equal(TestVectors.Dimensions, options?.Dimensions));
    }

    // ---------- the schema guard ----------

    [Fact]
    public async Task EmbedAsync_ThrowsWhenTheProviderReturnsAnotherDimension()
    {
        // The vector column is fixed at vector(1536). Storing anything else is a
        // schema error, and silently keeping it would poison the whole index.
        var generator = new FakeEmbeddingGenerator(dimensions: 512);

        EmbeddingService service = CreateService(generator, dimensions: 1536);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.EmbedAsync(["a"]));

        Assert.Contains("512", exception.Message);
        Assert.Contains("OpenAI:EmbeddingDimensions", exception.Message);
    }
}
