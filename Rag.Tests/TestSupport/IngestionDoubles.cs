using Rag.API.Embeddings;
using Rag.API.Ingestion;

namespace Rag.Tests.TestSupport;

/// <summary>Returns the pages it was handed, for the extensions it was told to support.</summary>
internal sealed class FakeDocumentParser(
    IReadOnlyList<PageText> pages,
    params string[] extensions) : IDocumentParser
{
    public IReadOnlyCollection<string> SupportedExtensions { get; } =
        extensions.Length == 0 ? [".pdf"] : extensions;

    public int CallCount { get; private set; }

    public IReadOnlyList<PageText> Parse(Stream documentStream)
    {
        CallCount++;
        return pages;
    }
}

/// <summary>One chunk per line, so a test can spell out the chunks it wants.</summary>
internal sealed class FakeChunker(bool returnsNothing = false) : IChunker
{
    public IReadOnlyList<TextChunk> Chunk(string pageText, int pageNumber)
    {
        if (returnsNothing) return [];

        return pageText
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => new TextChunk(line.Trim(), pageNumber))
            .ToList();
    }
}

/// <summary>Records the texts it embedded and returns a vector derived from each one.</summary>
internal sealed class FakeEmbeddingService : IEmbeddingService
{
    public List<string> EmbeddedTexts { get; } = [];

    public int CallCount { get; private set; }

    public Task<IReadOnlyList<float[]>> EmbedAsync(IReadOnlyList<string> texts)
    {
        CallCount++;
        EmbeddedTexts.AddRange(texts);

        IReadOnlyList<float[]> vectors = texts.Select(text => TestVectors.For(text)).ToList();

        return Task.FromResult(vectors);
    }
}
