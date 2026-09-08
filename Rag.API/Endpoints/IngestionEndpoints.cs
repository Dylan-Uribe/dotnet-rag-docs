using Rag.API.Contracts;
using Rag.API.Ingestion;

namespace Rag.API.Endpoints;

public static class IngestionEndpoints
{
    public static void MapIngestionEndpoints(this WebApplication app)
    {
        app.MapPost("/documents", async (
            IFormFile file,
            IIngestionService ingestion) =>
        {
            if (file.Length == 0)
            {
                return Results.BadRequest("The uploaded file is empty.");
            }

            if (!file.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                return Results.BadRequest("Only PDF files are supported.");
            }

            await using var stream = file.OpenReadStream();

            var result = await ingestion.IngestAsync(stream, file.FileName);

            return Results.Ok(new IngestResponse(
                result.DocumentId,
                result.PageCount,
                result.ChunkCount));
        })
        .WithName("IngestDocument")
        .DisableAntiforgery();
    }
}