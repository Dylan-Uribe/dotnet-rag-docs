using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.ML.Tokenizers;
using Rag.API.Data;
using Rag.API.Retrieval;

namespace Rag.Evals;

/// <summary>
/// Re-ingests the corpus under several chunking configurations and scores the same
/// golden set against each one. This only works because the ground truth is page
/// numbers: chunk ids would be invalidated by every re-chunking.
/// </summary>
public static class SweepRunner
{
    /// <summary>
    /// Explicit pairs rather than a full cross product: an overlap only makes sense
    /// relative to its chunk size, and the options validator rejects overlap >= size.
    /// The first five hold the overlap near 20% and vary the size; the 350 row is the
    /// current production setting and appears in both halves.
    /// </summary>
    private static readonly (int Size, int Overlap)[] Grid =
    [
        (150, 30),
        (250, 50),
        (350, 70),
        (500, 100),
        (800, 160),
        (350, 0),
        (350, 175)
    ];

    public static async Task<IReadOnlyList<SweepResult>> RunAsync(string[] args)
    {
        EvalQuestion[] questions = await GoldenSet.LoadAsync<EvalQuestion>("retrieval-questions.json");

        Console.WriteLine(
            $"Sweep: {Grid.Length} configurations x {questions.Length} questions. " +
            "Each configuration re-ingests the corpus.");
        Console.WriteLine();

        var results = new List<SweepResult>(Grid.Length);

        foreach ((int size, int overlap) in Grid)
        {
            Console.Write($"chunk {size,4} / overlap {overlap,3}  ");

            results.Add(await RunConfigurationAsync(args, questions, size, overlap));
        }

        await RestoreBaselineAsync(args);

        return results;
    }

    private static async Task<SweepResult> RunConfigurationAsync(
        string[] args,
        EvalQuestion[] questions,
        int size,
        int overlap)
    {
        using IHost host = EvalHost.Build(args, Overrides(size, overlap));
        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();

        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await context.Database.MigrateAsync();

        await CorpusLoader.EnsureIngestedAsync(scope.ServiceProvider, reingest: true, quiet: true);

        int chunkCount = await context.DocumentChunks.CountAsync();

        var runner = new EvalRunner(
            scope.ServiceProvider.GetRequiredService<IRetriever>(),
            scope.ServiceProvider.GetRequiredService<Tokenizer>());

        IReadOnlyList<QuestionOutcome> outcomes = await runner.RunAsync(questions);

        return new SweepResult(size, overlap, chunkCount, outcomes);
    }

    /// <summary>
    /// The sweep would otherwise leave the database holding whatever the last
    /// configuration produced, which is not what the API is configured to serve.
    /// </summary>
    private static async Task RestoreBaselineAsync(string[] args)
    {
        Console.WriteLine();
        Console.WriteLine("Restoring the corpus under the configured chunking ...");

        using IHost host = EvalHost.Build(args);
        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();

        await CorpusLoader.EnsureIngestedAsync(scope.ServiceProvider, reingest: true);
    }

    private static Dictionary<string, string?> Overrides(int size, int overlap) => new()
    {
        ["Rag:ChunkSizeTokens"] = size.ToString(),
        ["Rag:ChunkOverlapTokens"] = overlap.ToString()
    };
}
