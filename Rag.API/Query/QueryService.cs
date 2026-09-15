using Rag.API.Contracts;
using Rag.API.Generation;
using Rag.API.Retrieval;

namespace Rag.API.Query;

public sealed class QueryService(
    IRetriever retriever,
    IAnswerGenerator generator) : IQueryService
{
    public async Task<AnswerResponse> AskAsync(string question)
    {
        IReadOnlyList<RetrievedChunk> chunks = await retriever.RetrieveAsync(question);
        return await generator.GenerateAsync(question, chunks);
    }
}
