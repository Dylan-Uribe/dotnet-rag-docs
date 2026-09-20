using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Rag.API.Data;
using Rag.API.Extensions;
using Rag.API.Options;
using Rag.API.Query;
using Rag.API.Retrieval;
using Rag.Evals;

CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

// The host resolves its content root from the working directory, so the eval's own
// settings are loaded by absolute path instead: the run must not depend on where it
// was launched from.
builder.Configuration.AddJsonFile(
    Path.Combine(AppContext.BaseDirectory, "appsettings.json"),
    optional: false);

builder.Configuration.AddUserSecrets(typeof(EvalRunner).Assembly, optional: true);

builder.Services
    .AddPersistence(builder.Configuration)
    .AddConfiguredOptions()
    .AddAiProviders()
    .AddApplicationServices();

using IHost host = builder.Build();
await using AsyncServiceScope scope = host.Services.CreateAsyncScope();

await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.MigrateAsync();

await CorpusLoader.EnsureIngestedAsync(scope.ServiceProvider, reingest: args.Contains("--reingest"));

string eval = ArgValue(args, "--eval") ?? "retrieval";

switch (eval)
{
    case "retrieval":
        await RunRetrievalAsync();
        break;

    case "abstention":
        await RunAbstentionAsync();
        break;

    default:
        throw new ArgumentException($"Unknown eval '{eval}'. Use 'retrieval' or 'abstention'.");
}

async Task RunRetrievalAsync()
{
    EvalQuestion[] questions = await LoadAsync<EvalQuestion>("retrieval-questions.json");
    int topK = scope.ServiceProvider.GetRequiredService<IOptions<RagOptions>>().Value.TopK;

    Console.WriteLine($"Retrieval eval: {questions.Length} questions, TopK {topK}. No chat model is called.");

    IReadOnlyList<QuestionOutcome> outcomes =
        await new EvalRunner(scope.ServiceProvider.GetRequiredService<IRetriever>()).RunAsync(questions);

    EvalReport.Print(outcomes, topK);
    await EvalReport.WriteJsonAsync(outcomes, topK, OutputPath("eval-results.json"));
}

async Task RunAbstentionAsync()
{
    AbstentionQuestion[] questions = await LoadAsync<AbstentionQuestion>("abstention-questions.json");

    Console.WriteLine(
        $"Abstention eval: {questions.Length} questions " +
        $"({questions.Count(question => !question.Answerable)} unanswerable, " +
        $"{questions.Count(question => question.Answerable)} controls). This one calls the chat model.");

    IReadOnlyList<AbstentionOutcome> outcomes =
        await new AbstentionRunner(scope.ServiceProvider.GetRequiredService<IQueryService>()).RunAsync(questions);

    AbstentionReport.Print(outcomes);
    await AbstentionReport.WriteJsonAsync(outcomes, OutputPath("abstention-results.json"));
}

async Task<T[]> LoadAsync<T>(string fileName)
{
    string path = Path.Combine(AppContext.BaseDirectory, "GoldenSet", fileName);

    return JsonSerializer.Deserialize<T[]>(
        await File.ReadAllTextAsync(path),
        new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
        ?? throw new InvalidOperationException($"No questions found in {path}.");
}

string OutputPath(string defaultFileName) =>
    ArgValue(args, "--out") ?? Path.Combine(AppContext.BaseDirectory, defaultFileName);

static string? ArgValue(string[] arguments, string name)
{
    int index = Array.IndexOf(arguments, name);

    return index >= 0 && index + 1 < arguments.Length ? arguments[index + 1] : null;
}
