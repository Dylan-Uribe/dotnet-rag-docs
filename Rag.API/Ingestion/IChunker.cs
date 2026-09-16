namespace Rag.API.Ingestion;

public interface IChunker
{
    IReadOnlyList<TextChunk> Chunk(string pageText, int pageNumber);
}

public sealed record TextChunk(string Text, int PageNumber);
