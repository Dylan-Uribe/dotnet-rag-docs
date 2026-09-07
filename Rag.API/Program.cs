using Microsoft.EntityFrameworkCore;
using Microsoft.ML.Tokenizers;
using Rag.API.Data;
using Rag.API.Ingestion;
using Rag.API.Options;

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

builder.Services.AddScoped<IChunker, RecursiveChunker>();
builder.Services.AddSingleton<IDocumentParser, PdfParser>();

builder.Services.Configure<RagOptions>(builder.Configuration.GetSection("Rag"));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.Run();