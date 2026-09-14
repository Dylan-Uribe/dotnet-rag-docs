namespace Rag.API.Contracts;

public sealed record DocumentResponse(
    Guid Id,
    string Name,
    DateTime IngestedAt,
    int ChunkCount);
