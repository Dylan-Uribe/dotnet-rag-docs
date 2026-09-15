using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Pgvector;
using Pgvector.EntityFrameworkCore;
using Rag.API.Data;
using Rag.API.Embeddings;
using Rag.API.Options;

namespace Rag.API.Retrieval;

public sealed class VectorRetriever(
    ApplicationDbContext context,
    IEmbeddingService embeddings,
    IOptions<RagOptions> options) : IRetriever
{
    private readonly RagOptions _options = options.Value;

    public async Task<IReadOnlyList<RetrievedChunk>> RetrieveAsync(string question)
    {
        IReadOnlyList<float[]> vectors = await embeddings.EmbedAsync([question]);
        var queryVector = new Vector(vectors[0]);

        List<RetrievedChunk> candidates = await context.DocumentChunks
            .OrderBy(chunk => chunk.Embedding.CosineDistance(queryVector))
            .Take(_options.TopK)
            .Select(chunk => new RetrievedChunk(
                chunk.Document.Name,
                chunk.PageNumber,
                chunk.TextContent,
                chunk.Embedding.CosineDistance(queryVector)))
            .ToListAsync();

        if (_options.MaxDistance.HasValue) 
        {
            double max = _options.MaxDistance.Value;
            return candidates.Where(chunk => chunk.Distance <= max).ToList();
        }

        return candidates;
    }
}
