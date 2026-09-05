namespace Rag.API.Options;

public sealed class RagOptions
{
    public int ChunkSizeTokens { get; set; } = 500;
    public int ChunkOverlapTokens { get; set; } = 75;
}
