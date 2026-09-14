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
        var currentParts = new List<(string Text, int Tokens)>();

        void SealChunk(bool carryOverlap = false)
        {
            string sealed_ = builder.ToString().Trim();

            if (sealed_.Length == 0)
            {
                builder.Clear();
                builderTokens = 0;
                currentParts.Clear();
                return;
            }

            chunks.Add(new TextChunk(sealed_, pageNumber));

            var carried = new List<(string Text, int Tokens)>();

            if (carryOverlap && _chunkOverlap > 0 && currentParts.Count > 1)
            {
                int collected = 0;
                int maxOverlap = Math.Min(_chunkOverlap, _chunkSize / 2);

                for (int i = currentParts.Count - 1; i >= 1; i--)
                {
                    if (collected + currentParts[i].Tokens > maxOverlap) break;

                    carried.Add(currentParts[i]);
                    collected += currentParts[i].Tokens;
                }

                carried.Reverse();
            }

            builder.Clear();
            builderTokens = 0;
            currentParts.Clear();

            foreach (var part in carried)
            {
                builder.Append(part.Text).Append(separator);
                builderTokens += part.Tokens;
                currentParts.Add(part);
            }
        }

        foreach (string part in parts)
        {
            if (string.IsNullOrWhiteSpace(part)) continue;

            int partTokens = _tokenizer.CountTokens(part);

            if (partTokens > _chunkSize)
            {
                SealChunk(carryOverlap: false);
                SplitRecursively(part, pageNumber, separatorIndex + 1, chunks);
                continue;
            }

            if (builderTokens + partTokens > _chunkSize) 
            {
                SealChunk(carryOverlap: true);
            }

            builder.Append(part).Append(separator);
            currentParts.Add((part,partTokens));
            builderTokens += partTokens;
        }

        SealChunk(carryOverlap: false);
    }
}
