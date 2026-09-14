using Rag.API.Contracts;
using Rag.API.Generation;
using Rag.API.Retrieval;

namespace Rag.API.Endpoints;

public static class QueryEndpoints
{
    public static IEndpointRouteBuilder MapQueryEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/query", async (
            QueryRequest request,
            IRetriever retriever,
            IAnswerGenerator generator) =>
        {
            if (string.IsNullOrWhiteSpace(request.Question))
            {
                return Results.BadRequest(new { error = "Question must not be empty." });
            }

            var chunks = await retriever.RetrieveAsync(request.Question);
            var answer = await generator.GenerateAsync(request.Question, chunks);

            return Results.Ok(answer);
        })
        .WithName("Query");

        return app;
    }
}