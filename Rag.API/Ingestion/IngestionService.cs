using Pgvector;
using Rag.API.Data;
using Rag.API.Domain;
using Rag.API.Embeddings;

namespace Rag.API.Ingestion;

public class IngestionService : IIngestionService
{
    private readonly IDocumentParser _parser;
    private readonly IChunker _chunker;
    private readonly IEmbeddingService _embeddings;
    private readonly ApplicationDbContext _dbContext;

    public IngestionService(
        IDocumentParser parser, 
        IChunker chunker, 
        IEmbeddingService embeddings,
        ApplicationDbContext db) 
    {
        _parser = parser;
        _chunker = chunker;
        _embeddings = embeddings;
        _dbContext = db;
    }

    public async Task<IngestionResult> IngestAsync(Stream documentStream, string fileName)
    {
        var pages = _parser.Parse(documentStream);

        var chunks = new List<TextChunk>();

        foreach (var page in pages) 
        {
            chunks.AddRange(_chunker.Chunk(page.Text, page.PageNumber));
        }

        if (chunks.Count == 0)
        {
            throw new InvalidOperationException(
                $"'{fileName}' produced no chunks.");
        }

        var texts = chunks.Select(chunk => chunk.Text).ToList();
        var vectors = await _embeddings.EmbedAsync(texts);

        var document = new Document
        {
            Id = Guid.NewGuid(),
            Name = fileName,
            IngestedAt = DateTime.UtcNow
        };

        for (int i = 0; i < chunks.Count; i++)
        {
            document.DocumentChunks.Add(new DocumentChunk
            {
                Id = Guid.NewGuid(),
                DocumentId = document.Id,
                TextContent = chunks[i].Text,
                PageNumber = chunks[i].PageNumber,
                Embedding = new Vector(vectors[i])
            });
        }

        _dbContext.Documents.Add(document);
        await _dbContext.SaveChangesAsync();

        return new IngestionResult(document.Id, pages.Count, chunks.Count);

    }
}
