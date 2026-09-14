using Pgvector;
using Rag.API.Common;
using Rag.API.Data;
using Rag.API.Domain;
using Rag.API.Embeddings;

namespace Rag.API.Ingestion;

public class IngestionService : IIngestionService
{
    private readonly IReadOnlyList<IDocumentParser> _parsers;
    private readonly IChunker _chunker;
    private readonly IEmbeddingService _embeddings;
    private readonly ApplicationDbContext _dbContext;

    public IngestionService(
        IEnumerable<IDocumentParser> parsers,
        IChunker chunker,
        IEmbeddingService embeddings,
        ApplicationDbContext db)
    {
        _parsers = parsers.ToList();
        _chunker = chunker;
        _embeddings = embeddings;
        _dbContext = db;
    }

    public async Task<Result<IngestionResult>> IngestAsync(Stream documentStream, string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();

        var parser = _parsers.FirstOrDefault(
            p => p.SupportedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase));

        if (parser is null)
        {
            return Result<IngestionResult>.Failure(new Error(
                ErrorType.UnsupportedType,
                $"No parser is registered for '{extension}' files."));
        }

        var pages = parser.Parse(documentStream);

        if (pages.Count == 0)
        {
            return Result<IngestionResult>.Failure(new Error(
                ErrorType.Unprocessable,
                "No extractable text was found in the document. " +
                "It is likely a scanned PDF with no text layer and would require OCR."));
        }

        var chunks = pages
            .SelectMany(page => _chunker.Chunk(page.Text, page.PageNumber))
            .ToList();

        if (chunks.Count == 0)
        {
            return Result<IngestionResult>.Failure(new Error(
                ErrorType.Unprocessable,
                $"'{fileName}' produced no chunks."));
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

        return Result<IngestionResult>.Success(
            new IngestionResult(document.Id, pages.Count, chunks.Count));
    }
}
