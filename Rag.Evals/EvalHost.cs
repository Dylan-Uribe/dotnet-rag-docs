using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Rag.API.Extensions;

namespace Rag.Evals;

internal static class EvalHost
{
    /// <summary>
    /// Builds a host wired exactly like the API. <paramref name="overrides"/> exists for
    /// the sweep: RecursiveChunker reads its options in the constructor and is registered
    /// as a singleton, so a different chunk size means a different container.
    /// </summary>
    public static IHost Build(string[] args, IReadOnlyDictionary<string, string?>? overrides = null)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

        // The host resolves its content root from the working directory, so the eval's own
        // settings are loaded by absolute path instead: the run must not depend on where it
        // was launched from.
        builder.Configuration.AddJsonFile(
            Path.Combine(AppContext.BaseDirectory, "appsettings.json"),
            optional: false);

        builder.Configuration.AddUserSecrets(typeof(EvalHost).Assembly, optional: true);

        if (overrides is not null)
        {
            builder.Configuration.AddInMemoryCollection(overrides);
        }

        builder.Services
            .AddPersistence(builder.Configuration)
            .AddConfiguredOptions()
            .AddAiProviders()
            .AddApplicationServices();

        return builder.Build();
    }
}

internal static class GoldenSet
{
    public static async Task<T[]> LoadAsync<T>(string fileName)
    {
        string path = Path.Combine(AppContext.BaseDirectory, "GoldenSet", fileName);

        return JsonSerializer.Deserialize<T[]>(
            await File.ReadAllTextAsync(path),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException($"No questions found in {path}.");
    }
}
