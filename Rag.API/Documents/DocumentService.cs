using Microsoft.EntityFrameworkCore;
using Rag.API.Contracts;
using Rag.API.Data;

namespace Rag.API.Documents;

public sealed class DocumentService : IDocumentService
{
    private readonly ApplicationDbContext _dbContext;

    public DocumentService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<DocumentResponse>> GetAllAsync()
    {
        return await _dbContext.Documents
            .OrderByDescending(document => document.IngestedAt)
            .Select(document => new DocumentResponse(
                document.Id,
                document.Name,
                document.IngestedAt,
                document.DocumentChunks.Count))
            .ToListAsync();
    }

    public async Task<DocumentResponse?> GetByIdAsync(Guid id)
    {
        return await _dbContext.Documents
            .Where(document => document.Id == id)
            .Select(document => new DocumentResponse(
                document.Id,
                document.Name,
                document.IngestedAt,
                document.DocumentChunks.Count))
            .FirstOrDefaultAsync();
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var affected = await _dbContext.Documents
            .Where(document => document.Id == id)
            .ExecuteDeleteAsync();

        return affected > 0;
    }
}
