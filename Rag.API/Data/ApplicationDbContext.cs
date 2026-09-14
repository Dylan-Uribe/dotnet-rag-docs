using Microsoft.EntityFrameworkCore;
using Rag.API.Domain;
namespace Rag.API.Data;

public sealed class ApplicationDbContext
    (DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<DocumentChunk> DocumentChunks => Set<DocumentChunk>();
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
        modelBuilder.HasPostgresExtension("vector");
    }
}
