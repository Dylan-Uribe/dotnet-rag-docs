using Rag.API.Contracts;
using Rag.API.Generation;
using Rag.API.Retrieval;

namespace Rag.API.Query;

public sealed class QueryService : IQueryService
{
    private readonly IRetriever _retriever;
    private readonly IAnswerGenerator _generator;

    public QueryService(IRetriever retriever, IAnswerGenerator generator)
    {
        _retriever = retriever;
        _generator = generator;
    }

    public async Task<AnswerResponse> AskAsync(string question)
    {
        var chunks = await _retriever.RetrieveAsync(question);
        return await _generator.GenerateAsync(question, chunks);
    }
}
