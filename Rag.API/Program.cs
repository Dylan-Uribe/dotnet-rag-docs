using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Microsoft.ML.Tokenizers;
using OpenAI;
using Rag.API.Common;
using Rag.API.Data;
using Rag.API.Embeddings;
using Rag.API.Endpoints;
using Rag.API.Generation;
using Rag.API.Ingestion;
using Rag.API.Options;
using Rag.API.Query;
using Rag.API.Retrieval;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddValidation();

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("Postgres"),
        o => o.UseVector())
);

builder.Services.AddSingleton<Tokenizer>(
    TiktokenTokenizer.CreateForEncoding("cl100k_base"));

builder.Services.AddSingleton<IChunker, RecursiveChunker>();
builder.Services.AddSingleton<IDocumentParser, PdfParser>();

builder.Services.AddOptions<RagOptions>()
    .Bind(builder.Configuration.GetSection(RagOptions.SectionName))
    .ValidateDataAnnotations()
    .Validate(
        o => o.ChunkOverlapTokens < o.ChunkSizeTokens,
        "Rag:ChunkOverlapTokens must be less than Rag:ChunkSizeTokens.")
    .ValidateOnStart();

builder.Services.AddOptions<OpenAIOptions>()
    .Bind(builder.Configuration.GetSection(OpenAIOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(sp =>
{
    var options = sp.GetRequiredService<IOptions<OpenAIOptions>>().Value;

    return new OpenAIClient(options.ApiKey)
        .GetEmbeddingClient(options.EmbeddingModel)
        .AsIEmbeddingGenerator();
});

builder.Services.AddSingleton<IChatClient>(sp =>
{
    var options = sp.GetRequiredService<IOptions<OpenAIOptions>>().Value;

    return new OpenAIClient(options.ApiKey)
        .GetChatClient(options.ChatModel)
        .AsIChatClient();
});

builder.Services.AddSingleton<IEmbeddingService, EmbeddingService>();
builder.Services.AddScoped<IIngestionService, IngestionService>();
builder.Services.AddScoped<IRetriever, VectorRetriever>();
builder.Services.AddSingleton<IAnswerGenerator, AnswerGenerator>();
builder.Services.AddScoped<IQueryService, QueryService>();

var app = builder.Build();

app.UseExceptionHandler();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();

app.MapIngestionEndpoints();
app.MapQueryEndpoints();
app.Run();
