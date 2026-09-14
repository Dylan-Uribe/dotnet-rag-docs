using Rag.API.Contracts;
using Rag.API.Query;

namespace Rag.API.Endpoints;

public static class QueryEndpoints
{
    public static IEndpointRouteBuilder MapQueryEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/query", async (
            QueryRequest request,
            IQueryService queryService) =>
        {
            if (string.IsNullOrWhiteSpace(request.Question))
            {
                return Results.BadRequest(new { error = "Question must not be empty." });
            }

            var answer = await queryService.AskAsync(request.Question);

            return Results.Ok(answer);
        })
        .WithName("Query");

        return app;
    }
}
