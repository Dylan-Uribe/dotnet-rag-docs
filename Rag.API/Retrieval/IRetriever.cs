namespace Rag.API.Retrieval;

public interface IRetriever
{
    Task<IReadOnlyList<RetrievedChunk>> RetrieveAsync(string question);
}