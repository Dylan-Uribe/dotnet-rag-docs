using System.ComponentModel.DataAnnotations;

namespace Rag.API.Options;

public sealed class OpenAIOptions
{
    public const string SectionName = "OpenAI";

    [Required]
    public string ApiKey { get; set; } = string.Empty;

    [Required]
    public string EmbeddingModel { get; set; } = "text-embedding-3-small";

    [Range(1, 3072)]
    public int EmbeddingDimensions { get; set; } = 1536;

    [Range(1, 2048)]
    public int EmbeddingBatchSize { get; set; } = 64;

    [Required]
    public string ChatModel { get; set; } = "gpt-4o-mini";
}
