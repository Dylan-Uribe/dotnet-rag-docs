using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.ML.Tokenizers;
using Rag.API.Data;
using Rag.API.Options;
using Rag.API.Query;
using Rag.API.Retrieval;
using Rag.Evals;

CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;

string eval = ArgValue(args, "--eval") ?? "retrieval";

// The sweep builds one host per configuration, so it runs before the shared one.
if (eval == "sweep")
{
    IReadOnlyList<SweepResult> sweep = await SweepRunner.RunAsync(args);

    SweepReport.Print(sweep);
    await SweepReport.WriteJsonAsync(sweep, OutputPath("sweep-results.json"));

    return;
}

using IHost host = EvalHost.Build(args);
await using AsyncServiceScope scope = host.Services.CreateAsyncScope();

await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.MigrateAsync();

await CorpusLoader.EnsureIngestedAsync(scope.ServiceProvider, reingest: args.Contains("--reingest"));

switch (eval)
{
    case "retrieval":
        await RunRetrievalAsync();
        break;

    case "abstention":
        await RunAbstentionAsync();
        break;

    default:
        throw new ArgumentException($"Unknown eval '{eval}'. Use 'retrieval', 'abstention' or 'sweep'.");
}

async Task RunRetrievalAsync()
{
    EvalQuestion[] questions = await GoldenSet.LoadAsync<EvalQuestion>("retrieval-questions.json");
    int topK = scope.ServiceProvider.GetRequiredService<IOptions<RagOptions>>().Value.TopK;

    Console.WriteLine($"Retrieval eval: {questions.Length} questions, TopK {topK}. No chat model is called.");

    var runner = new EvalRunner(
        scope.ServiceProvider.GetRequiredService<IRetriever>(),
        scope.ServiceProvider.GetRequiredService<Tokenizer>());

    IReadOnlyList<QuestionOutcome> outcomes = await runner.RunAsync(questions);

    EvalReport.Print(outcomes, topK);
    await EvalReport.WriteJsonAsync(outcomes, topK, OutputPath("eval-results.json"));
}

async Task RunAbstentionAsync()
{
    AbstentionQuestion[] questions = await GoldenSet.LoadAsync<AbstentionQuestion>("abstention-questions.json");

    Console.WriteLine(
        $"Abstention eval: {questions.Length} questions " +
        $"({questions.Count(question => !question.Answerable)} unanswerable, " +
        $"{questions.Count(question => question.Answerable)} controls). This one calls the chat model.");

    IReadOnlyList<AbstentionOutcome> outcomes =
        await new AbstentionRunner(scope.ServiceProvider.GetRequiredService<IQueryService>()).RunAsync(questions);

    AbstentionReport.Print(outcomes);
    await AbstentionReport.WriteJsonAsync(outcomes, OutputPath("abstention-results.json"));
}

string OutputPath(string defaultFileName) =>
    ArgValue(args, "--out") ?? Path.Combine(AppContext.BaseDirectory, defaultFileName);

static string? ArgValue(string[] arguments, string name)
{
    int index = Array.IndexOf(arguments, name);

    return index >= 0 && index + 1 < arguments.Length ? arguments[index + 1] : null;
}
