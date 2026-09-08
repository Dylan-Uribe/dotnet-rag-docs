using Rag.API.Contracts;
using Rag.API.Retrieval;

namespace Rag.API.Endpoints;

public static class QueryEndpoints
{
    public static IEndpointRouteBuilder MapQueryEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/query", async (QueryRequest request, IRetriever retriever) =>
        {
            if (string.IsNullOrWhiteSpace(request.Question))
            {
                return Results.BadRequest(new { error = "Question must not be empty." });
            }

            var chunks = await retriever.RetrieveAsync(request.Question);

            return Results.Ok(chunks);
        })
        .WithName("Query");

        return app;
    }
}