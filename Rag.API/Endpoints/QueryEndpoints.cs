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
            AnswerResponse answer = await queryService.AskAsync(request.Question);
            return Results.Ok(answer);
        })
        .WithName("Query")
        .WithTags("Query")
        .Produces<AnswerResponse>(StatusCodes.Status200OK)
        .ProducesValidationProblem();

        return app;
    }
}
