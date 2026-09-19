using Microsoft.EntityFrameworkCore;
using Rag.API.Contracts;
using Rag.API.Data;

namespace Rag.API.Documents;

public sealed class DocumentService(
    ApplicationDbContext context) : IDocumentService
{
    public async Task<IReadOnlyList<DocumentResponse>> GetAllAsync()
    {
        return await context.Documents
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
        return await context.Documents
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
        int affected = await context.Documents
            .Where(document => document.Id == id)
            .ExecuteDeleteAsync();

        return affected > 0;
    }
}
