using Rag.API.Contracts;

namespace Rag.API.Documents;

public interface IDocumentService
{
    Task<IReadOnlyList<DocumentResponse>> GetAllAsync();
    Task<DocumentResponse?> GetByIdAsync(Guid id);
    Task<bool> DeleteAsync(Guid id);
}
