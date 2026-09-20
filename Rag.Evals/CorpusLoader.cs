using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Rag.API.Common;
using Rag.API.Contracts;
using Rag.API.Data;
using Rag.API.Domain;
using Rag.API.Ingestion;

namespace Rag.Evals;

internal static class CorpusLoader
{
    private const string CorpusFileName = "football.pdf";

    /// <summary>
    /// Makes sure the corpus is in the database. Re-ingesting is opt-in because the
    /// embeddings are already paid for; pass --reingest after changing chunking.
    /// </summary>
    public static async Task EnsureIngestedAsync(IServiceProvider services, bool reingest)
    {
        ApplicationDbContext context = services.GetRequiredService<ApplicationDbContext>();

        List<Document> existing = await context.Documents
            .Where(document => document.Name.ToLower() == CorpusFileName)
            .Include(document => document.DocumentChunks)
            .ToListAsync();

        if (existing.Count > 0 && !reingest)
        {
            int chunkCount = existing.Sum(document => document.DocumentChunks.Count);
            Console.WriteLine($"Corpus: {existing[0].Name} ({chunkCount} chunks, already ingested)");
            return;
        }

        if (existing.Count > 0)
        {
            context.Documents.RemoveRange(existing);
            await context.SaveChangesAsync();
            Console.WriteLine($"Removed {existing.Count} previously ingested copy/copies.");
        }

        string path = Path.Combine(AppContext.BaseDirectory, "Corpus", CorpusFileName);

        Console.WriteLine($"Ingesting {path} ...");

        await using FileStream stream = File.OpenRead(path);

        Result<IngestResponse> result = await services
            .GetRequiredService<IIngestionService>()
            .IngestAsync(stream, CorpusFileName);

        if (!result.IsSuccess)
        {
            throw new InvalidOperationException($"Ingestion failed: {result.Error!.Message}");
        }

        Console.WriteLine($"Corpus: {CorpusFileName} ({result.Value!.ChunkCount} chunks from {result.Value.PageCount} pages)");
    }
}
