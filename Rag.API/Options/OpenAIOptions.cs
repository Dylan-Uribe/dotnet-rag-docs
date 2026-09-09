namespace Rag.API.Options;

public sealed class OpenAIOptions
{
    public string ApiKey { get; set; } = string.Empty;
    public string EmbeddingModel { get; set; } = "text-embedding-3-small";
    public int EmbeddingDimensions { get; set; } = 1536;
    public int EmbeddingBatchSize { get; set; } = 64;
    public string ChatModel { get; set; } = "gpt-4o-mini";
}