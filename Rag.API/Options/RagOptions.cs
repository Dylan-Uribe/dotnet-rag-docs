using System.ComponentModel.DataAnnotations;

namespace Rag.API.Options;

public sealed class RagOptions
{
    public const string SectionName = "Rag";

    [Range(1, 8191)]
    public int ChunkSizeTokens { get; set; } = 350;

    [Range(0, 8191)]
    public int ChunkOverlapTokens { get; set; } = 70;

    [Range(1, 100)]
    public int TopK { get; set; } = 8;

    [Range(0.0, 2.0)]
    public double? MaxDistance { get; set; }
}
