using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Microsoft.ML.Tokenizers;
using OpenAI;
using Rag.API.Data;
using Rag.API.Embeddings;
using Rag.API.Endpoints;
using Rag.API.Ingestion;
using Rag.API.Options;
using Rag.API.Retrieval;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("Postgres"),
        o => o.UseVector())
);

builder.Services.AddSingleton<Tokenizer>(
    TiktokenTokenizer.CreateForEncoding("cl100k_base"));

builder.Services.AddSingleton<IChunker, RecursiveChunker>();
builder.Services.AddSingleton<IDocumentParser, PdfParser>();

builder.Services.Configure<RagOptions>(builder.Configuration.GetSection("Rag"));
builder.Services.Configure<OpenAIOptions>(builder.Configuration.GetSection("OpenAI"));

builder.Services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(sp =>
{
    var options = sp.GetRequiredService<IOptions<OpenAIOptions>>().Value;

    if (string.IsNullOrWhiteSpace(options.ApiKey))
    {
        throw new InvalidOperationException(
            "OpenAI:ApiKey is not configured. Run: dotnet user-secrets set \"OpenAI:ApiKey\" \"sk-...\"");
    }

    return new OpenAIClient(options.ApiKey)
        .GetEmbeddingClient(options.EmbeddingModel)
        .AsIEmbeddingGenerator();
});

builder.Services.AddSingleton<IEmbeddingService, OpenAIEmbeddingService>();
builder.Services.AddScoped<IIngestionService, IngestionService>();
builder.Services.AddScoped<IRetriever, VectorRetriever>();

var app = builder.Build();

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