using Rag.API.Contracts;
using Rag.API.Retrieval;

namespace Rag.API.Generation;

public interface IAnswerGenerator
{
    Task<AnswerResponse> GenerateAsync(string question, IReadOnlyList<RetrievedChunk> chunks);
}
