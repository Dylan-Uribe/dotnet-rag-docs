namespace Rag.API.Domain;

public sealed class Document
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime IngestedAt { get; set; }
    public List<DocumentChunk> DocumentChunks { get; set; } = [];
}
