using Rag.API.Common;
using Rag.API.Contracts;
using Rag.API.Documents;
using Rag.API.Ingestion;

namespace Rag.API.Endpoints;

public static class DocumentsEndpoints
{
    public static IEndpointRouteBuilder MapDocumentsEndpoints(this IEndpointRouteBuilder app)
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

            await using Stream stream = file.OpenReadStream();

            Result<IngestResponse> result = await ingestion.IngestAsync(stream, file.FileName);

            if (!result.IsSuccess)
            {
                return result.Error!.ToProblem();
            }

            IngestResponse response = result.Value!;

            return Results.CreatedAtRoute(
                routeName: "GetDocumentById",
                routeValues: new { id = response.DocumentId },
                value: response);
        })
        .WithName("IngestDocument")
        .WithTags("Documents")
        .Produces<IngestResponse>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status415UnsupportedMediaType)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
        .DisableAntiforgery();

        app.MapGet("/documents", async (IDocumentService documents) =>
        {
            IReadOnlyList<DocumentResponse> result = await documents.GetAllAsync();
            return Results.Ok(result);
        })
        .WithName("GetDocuments")
        .WithTags("Documents")
        .Produces<IReadOnlyList<DocumentResponse>>(StatusCodes.Status200OK);

        app.MapGet("/documents/{id:guid}", async (Guid id, IDocumentService documents) =>
        {
            DocumentResponse? document = await documents.GetByIdAsync(id);

            return document is null
                ? Results.Problem(
                    detail: $"Document '{id}' was not found.",
                    statusCode: StatusCodes.Status404NotFound)
                : Results.Ok(document);
        })
        .WithName("GetDocumentById")
        .WithTags("Documents")
        .Produces<DocumentResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound);

        app.MapDelete("/documents/{id:guid}", async (Guid id, IDocumentService documents) =>
        {
            bool deleted = await documents.DeleteAsync(id);

            return deleted
                ? Results.NoContent()
                : Results.Problem(
                    detail: $"Document '{id}' was not found.",
                    statusCode: StatusCodes.Status404NotFound);
        })
        .WithName("DeleteDocument")
        .WithTags("Documents")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}
