using Pgvector;
using Rag.API.Common;
using Rag.API.Contracts;
using Rag.API.Data;
using Rag.API.Domain;
using Rag.API.Embeddings;

namespace Rag.API.Ingestion;

public sealed class IngestionService(
    IEnumerable<IDocumentParser> parsers,
    IChunker chunker,
    IEmbeddingService embeddings,
    ApplicationDbContext context) : IIngestionService
{
    private readonly IReadOnlyList<IDocumentParser> _parsers = parsers.ToList();

    public async Task<Result<IngestResponse>> IngestAsync(Stream documentStream, string fileName)
    {
        string extension = Path.GetExtension(fileName).ToLowerInvariant();

        IDocumentParser? parser = _parsers.FirstOrDefault(
            p => p.SupportedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase));

        if (parser is null)
        {
            return Result<IngestResponse>.Failure(new Error(
                ErrorType.UnsupportedType,
                $"No parser is registered for '{extension}' files."));
        }

        IReadOnlyList<PageText> pages = parser.Parse(documentStream);

        if (pages.Count == 0)
        {
            return Result<IngestResponse>.Failure(new Error(
                ErrorType.Unprocessable,
                "No extractable text was found in the document. " +
                "It is likely a scanned PDF with no text layer and would require OCR."));
        }

        var chunks = pages
            .SelectMany(page => chunker.Chunk(page.Text, page.PageNumber))
            .ToList();

        if (chunks.Count == 0)
        {
            return Result<IngestResponse>.Failure(new Error(
                ErrorType.Unprocessable,
                $"'{fileName}' produced no chunks."));
        }

        var texts = chunks.Select(chunk => chunk.Text).ToList();
        IReadOnlyList<float[]> vectors = await embeddings.EmbedAsync(texts);

        var document = new Document
        {
            Id = Guid.CreateVersion7(),
            Name = fileName,
            IngestedAt = DateTime.UtcNow
        };

        for (int i = 0; i < chunks.Count; i++)
        {
            document.DocumentChunks.Add(new DocumentChunk
            {
                Id = Guid.CreateVersion7(),
                TextContent = chunks[i].Text,
                PageNumber = chunks[i].PageNumber,
                Embedding = new Vector(vectors[i])
            });
        }

        context.Documents.Add(document);
        await context.SaveChangesAsync();

        return Result<IngestResponse>.Success(
            new IngestResponse(document.Id, pages.Count, chunks.Count));
    }
}
