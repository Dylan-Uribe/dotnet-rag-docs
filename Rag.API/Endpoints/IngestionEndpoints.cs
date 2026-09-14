using Rag.API.Common;
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

            await using var stream = file.OpenReadStream();

            var result = await ingestion.IngestAsync(stream, file.FileName);

            if (!result.IsSuccess)
            {
                var error = result.Error!;
                var status = error.Type switch
                {
                    ErrorType.UnsupportedType => StatusCodes.Status415UnsupportedMediaType,
                    _ => StatusCodes.Status422UnprocessableEntity
                };

                return Results.Problem(detail: error.Message, statusCode: status);
            }

            var value = result.Value!;

            return Results.Ok(new IngestResponse(
                value.DocumentId,
                value.PageCount,
                value.ChunkCount));
        })
        .WithName("IngestDocument")
        .DisableAntiforgery();
    }
}