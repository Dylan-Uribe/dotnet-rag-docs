namespace Rag.API.Ingestion;

public interface IIngestionService
{
    Task<IngestionResult> IngestAsync(Stream documentStream, string fileName);
}

public sealed record IngestionResult(Guid DocumentId, int PageCount, int ChunkCount);