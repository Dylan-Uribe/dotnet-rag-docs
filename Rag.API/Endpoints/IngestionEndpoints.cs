using Rag.API.Contracts;
using Rag.API.Ingestion;

namespace Rag.API.Endpoints;

public static class IngestionEndpoints
{
    public static IEndpointRouteBuilder MapIngestionEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/documents", async (
            IFormFile file,
            IIngestionService ingestion) =>
        {
            if (file.Length == 0)
            {
                return Results.Problem(
                    detail: "The uploaded file is empty.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            await using var stream = file.OpenReadStream();

            var result = await ingestion.IngestAsync(stream, file.FileName);

            if (!result.IsSuccess)
            {
                return result.Error!.ToProblem();
            }

            var value = result.Value!;
            var response = new IngestResponse(value.DocumentId, value.PageCount, value.ChunkCount);

            return Results.Created($"/documents/{value.DocumentId}", response);
        })
        .WithName("IngestDocument")
        .WithTags("Documents")
        .Produces<IngestResponse>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status415UnsupportedMediaType)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
        .DisableAntiforgery();

        return app;
    }
}
