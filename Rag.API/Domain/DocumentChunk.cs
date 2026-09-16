using Pgvector;

namespace Rag.API.Domain;

public sealed class DocumentChunk
{
    public Guid Id { get; set; }
    public string TextContent { get; set; } = string.Empty;
    public required Vector Embedding { get; set; }
    public int PageNumber { get; set; }
    public Guid DocumentId { get; set; }
    public Document Document { get; set; } = null!;
}
