namespace Rag.API.Contracts;

public sealed record IngestResponse(
    Guid DocumentId,
    int PageCount,
    int ChunkCount
);
