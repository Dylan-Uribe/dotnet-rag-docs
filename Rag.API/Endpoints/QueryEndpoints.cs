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
                return Results.Problem(
                    detail: "Question must not be empty.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            var answer = await queryService.AskAsync(request.Question);

            return Results.Ok(answer);
        })
        .WithName("Query")
        .WithTags("Query")
        .Produces<AnswerResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest);

        return app;
    }
}
