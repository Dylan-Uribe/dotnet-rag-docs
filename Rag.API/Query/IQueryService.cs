using Rag.API.Contracts;

namespace Rag.API.Query;

public interface IQueryService
{
    Task<AnswerResponse> AskAsync(string question);
}
