namespace Rag.API.Options;

public sealed class RagOptions
{
    public int ChunkSizeTokens { get; set; } = 500;
    public int ChunkOverlapTokens { get; set; } = 75;
    public int EmbeddingBatchSize { get; set; } = 64;
    public int TopK { get; set; } = 5;
    public double? MaxDistance { get; set; }
}
