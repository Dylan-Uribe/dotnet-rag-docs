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

string goldenSetPath = Path.Combine(AppContext.BaseDirectory, "GoldenSet", "retrieval-questions.json");

EvalQuestion[] questions =
    JsonSerializer.Deserialize<EvalQuestion[]>(
        await File.ReadAllTextAsync(goldenSetPath),
        new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
    ?? throw new InvalidOperationException($"No questions found in {goldenSetPath}.");

int topK = scope.ServiceProvider.GetRequiredService<IOptions<RagOptions>>().Value.TopK;

Console.WriteLine($"Running {questions.Length} questions against the real retriever ...");

IReadOnlyList<QuestionOutcome> outcomes =
    await new EvalRunner(scope.ServiceProvider.GetRequiredService<IRetriever>()).RunAsync(questions);

EvalReport.Print(outcomes, topK);

await EvalReport.WriteJsonAsync(outcomes, topK, OutputPath(args));

static string OutputPath(string[] args)
{
    int index = Array.IndexOf(args, "--out");

    return index >= 0 && index + 1 < args.Length
        ? args[index + 1]
        : Path.Combine(AppContext.BaseDirectory, "eval-results.json");
}
