using Microsoft.Extensions.Options;
using Rag.API.Options;
using Microsoft.ML.Tokenizers;
using System.Text;

namespace Rag.API.Ingestion;

public class RecursiveChunker : IChunker
{
    private readonly int _chunkSize;
    private readonly int _chunkOverlap;
    private readonly Tokenizer _tokenizer;
    private readonly string[] _separators = ["\n\n", "\n", ". ", " "];
    public RecursiveChunker(IOptions<RagOptions> options, Tokenizer tokenizer)
    {
        _chunkSize = options.Value.ChunkSizeTokens;
        _chunkOverlap = options.Value.ChunkOverlapTokens;
        _tokenizer = tokenizer;
    }
    public IReadOnlyList<TextChunk> Chunk(string pageText, int pageNumber)
    {
        var chunks = new List<TextChunk>();

        if (string.IsNullOrWhiteSpace(pageText)) 
        {
            return chunks;
        }

        SplitRecursively(pageText, pageNumber, 0, chunks);

        return chunks;
    }

    private void SplitRecursively(string text, int pageNumber, int separatorIndex, List<TextChunk> chunks)
    {
        if (_tokenizer.CountTokens(text) <= _chunkSize)
        {
            chunks.Add(new TextChunk(text.Trim(), pageNumber));
            return;
        }

        if (separatorIndex >= _separators.Length)
        {
            chunks.Add(new TextChunk(text.Trim(), pageNumber));
            return;
        }

        string separator = _separators[separatorIndex];
        string[] parts = text.Split(separator, StringSplitOptions.RemoveEmptyEntries);

        var builder = new StringBuilder();
        int builderTokens = 0;

        void SealChunk()
        {
            if (builder.Length.Equals(0)) return;
            chunks.Add(new TextChunk(builder.ToString().Trim(), pageNumber));
            builder.Clear();
            builderTokens = 0;
        }

        foreach (string part in parts)
        {
            if (string.IsNullOrWhiteSpace(part)) continue;

            int partTokens = _tokenizer.CountTokens(part);

            if (partTokens > _chunkSize)
            {
                SealChunk();
                SplitRecursively(part, pageNumber, separatorIndex + 1, chunks);
                continue;
            }

            if (builderTokens + partTokens > _chunkSize) 
            {
                SealChunk();
            }

            builder.Append(part).Append(separator);
            builderTokens += partTokens;
        }

        SealChunk();
    }
}
