using Rag.API.Common;

namespace Rag.API.Ingestion;

public interface IIngestionService
{
    Task<Result<IngestionResult>> IngestAsync(Stream documentStream, string fileName);
}

public sealed record IngestionResult(Guid DocumentId, int PageCount, int ChunkCount);
