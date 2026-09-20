using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.ML.Tokenizers;
using OpenAI;
using Rag.API.Data;
using Rag.API.Generation;
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

// --chunk/--overlap run any eval under a chunking other than the configured one.
// They force a re-ingest: whatever is stored was cut to different boundaries.
Dictionary<string, string?>? chunking = ChunkingOverrides(args);
Dictionary<string, string?> overrides = new(chunking ?? []);

if (ArgValue(args, "--judge") is string judgeOverride)
{
    overrides["Judge:Model"] = judgeOverride;
}

using IHost host = EvalHost.Build(args, overrides.Count > 0 ? overrides : null);
await using AsyncServiceScope scope = host.Services.CreateAsyncScope();

await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.MigrateAsync();

if (chunking is not null)
{
    RagOptions applied = scope.ServiceProvider.GetRequiredService<IOptions<RagOptions>>().Value;
    Console.WriteLine(
        $"Chunking override: {applied.ChunkSizeTokens} tokens / {applied.ChunkOverlapTokens} overlap.");
}

await CorpusLoader.EnsureIngestedAsync(
    scope.ServiceProvider,
    reingest: args.Contains("--reingest") || chunking is not null);

switch (eval)
{
    case "retrieval":
        await RunRetrievalAsync();
        break;

    case "abstention":
        await RunAbstentionAsync();
        break;

    case "faithfulness":
        await RunFaithfulnessAsync();
        break;

    case "injection":
        await RunInjectionAsync();
        break;

    default:
        throw new ArgumentException(
            $"Unknown eval '{eval}'. Use 'retrieval', 'abstention', 'faithfulness', 'injection' or 'sweep'.");
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

async Task RunFaithfulnessAsync()
{
    OpenAIOptions ai = scope.ServiceProvider.GetRequiredService<IOptions<OpenAIOptions>>().Value;

    string judgeModel =
        host.Services.GetRequiredService<IConfiguration>()["Judge:Model"] ?? ai.ChatModel;

    using IChatClient judgeClient = new OpenAIClient(ai.ApiKey)
        .GetChatClient(judgeModel)
        .AsIChatClient();

    var runner = new FaithfulnessRunner(
        scope.ServiceProvider.GetRequiredService<IRetriever>(),
        scope.ServiceProvider.GetRequiredService<IAnswerGenerator>(),
        new Judge(judgeClient));

    Console.WriteLine($"Judge model: {judgeModel}");

    if (judgeModel == ai.ChatModel)
    {
        Console.WriteLine("Warning: the judge is the same model that wrote the answers. See the README.");
    }

    Console.WriteLine();
    Console.WriteLine("Calibrating the judge before trusting it ...");

    CalibrationCase[] cases = await GoldenSet.LoadAsync<CalibrationCase>("judge-calibration.json");
    IReadOnlyList<CalibrationOutcome> calibration = await runner.CalibrateAsync(cases);

    FaithfulnessReport.PrintCalibration(calibration);

    double agreement = calibration.Count(outcome => outcome.FaithfulAgrees) / (double)calibration.Count;

    if (agreement < 0.75)
    {
        Console.WriteLine();
        Console.WriteLine(
            $"Judge agreement is {agreement:0.00}. The numbers below measure the judge, not the system.");
    }

    if (args.Contains("--calibrate-only")) return;

    EvalQuestion[] questions = await GoldenSet.LoadAsync<EvalQuestion>("retrieval-questions.json");

    Console.WriteLine();
    Console.WriteLine($"Grading {questions.Length} answers ...");

    IReadOnlyList<FaithfulnessOutcome> outcomes = await runner.RunAsync(questions);

    FaithfulnessReport.Print(outcomes);
    await FaithfulnessReport.WriteJsonAsync(
        calibration, outcomes, judgeModel, OutputPath("faithfulness-results.json"));
}

async Task RunInjectionAsync()
{
    InjectionAttack[] attacks = await GoldenSet.LoadAsync<InjectionAttack>("injection-attacks.json");

    Console.WriteLine(
        $"Injection eval: {attacks.Length} payloads at two positions each. " +
        "Each payload is spliced into the chunks a real retrieval returned.");

    var runner = new InjectionRunner(
        scope.ServiceProvider.GetRequiredService<IRetriever>(),
        scope.ServiceProvider.GetRequiredService<IAnswerGenerator>());

    IReadOnlyList<InjectionOutcome> outcomes = await runner.RunAsync(attacks);

    InjectionReport.Print(outcomes);
    await InjectionReport.WriteJsonAsync(outcomes, OutputPath("injection-results.json"));
}

string OutputPath(string defaultFileName) =>
    ArgValue(args, "--out") ?? Path.Combine(AppContext.BaseDirectory, defaultFileName);

static Dictionary<string, string?>? ChunkingOverrides(string[] arguments)
{
    string? size = ArgValue(arguments, "--chunk");
    string? overlap = ArgValue(arguments, "--overlap");

    if (size is null && overlap is null) return null;

    var overrides = new Dictionary<string, string?>();

    if (size is not null) overrides["Rag:ChunkSizeTokens"] = size;
    if (overlap is not null) overrides["Rag:ChunkOverlapTokens"] = overlap;

    return overrides;
}

static string? ArgValue(string[] arguments, string name)
{
    int index = Array.IndexOf(arguments, name);

    return index >= 0 && index + 1 < arguments.Length ? arguments[index + 1] : null;
}
