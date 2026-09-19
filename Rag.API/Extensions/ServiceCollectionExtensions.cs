using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Microsoft.ML.Tokenizers;
using OpenAI;
using Rag.API.Common;
using Rag.API.Data;
using Rag.API.Documents;
using Rag.API.Embeddings;
using Rag.API.Generation;
using Rag.API.Ingestion;
using Rag.API.Options;
using Rag.API.Query;
using Rag.API.Retrieval;

namespace Rag.API.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApiServices(this IServiceCollection services)
    {
        services.AddOpenApi();
        services.AddProblemDetails();
        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.AddValidation();

        return services;
    }

    public static IServiceCollection AddPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("Postgres"),
                o => o.UseVector()));

        return services;
    }

    public static IServiceCollection AddConfiguredOptions(this IServiceCollection services)
    {
        services.AddOptions<RagOptions>()
            .BindConfiguration(RagOptions.SectionName)
            .ValidateDataAnnotations()
            .Validate(
                o => o.ChunkOverlapTokens < o.ChunkSizeTokens,
                "Rag:ChunkOverlapTokens must be less than Rag:ChunkSizeTokens.")
            .ValidateOnStart();

        services.AddOptions<OpenAIOptions>()
            .BindConfiguration(OpenAIOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        return services;
    }

    public static IServiceCollection AddAiProviders(this IServiceCollection services)
    {
        services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(sp =>
        {
            OpenAIOptions options = sp.GetRequiredService<IOptions<OpenAIOptions>>().Value;

            return new OpenAIClient(options.ApiKey)
                .GetEmbeddingClient(options.EmbeddingModel)
                .AsIEmbeddingGenerator();
        });

        services.AddSingleton<IChatClient>(sp =>
        {
            OpenAIOptions options = sp.GetRequiredService<IOptions<OpenAIOptions>>().Value;

            return new OpenAIClient(options.ApiKey)
                .GetChatClient(options.ChatModel)
                .AsIChatClient();
        });

        return services;
    }

    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddSingleton<Tokenizer>(
            TiktokenTokenizer.CreateForEncoding("cl100k_base"));

        services.AddSingleton<IChunker, RecursiveChunker>();
        services.AddSingleton<IDocumentParser, PdfParser>();

        services.AddSingleton<IEmbeddingService, EmbeddingService>();
        services.AddScoped<IIngestionService, IngestionService>();
        services.AddScoped<IDocumentService, DocumentService>();
        services.AddScoped<IRetriever, VectorRetriever>();
        services.AddSingleton<IAnswerGenerator, AnswerGenerator>();
        services.AddScoped<IQueryService, QueryService>();

        return services;
    }
}
