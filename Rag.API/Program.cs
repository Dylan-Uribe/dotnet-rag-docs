using Rag.API.Endpoints;
using Rag.API.Extensions;
using Scalar.AspNetCore;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddApiServices()
    .AddPersistence(builder.Configuration)
    .AddConfiguredOptions()
    .AddAiProviders()
    .AddApplicationServices();

WebApplication app = builder.Build();

app.ApplyMigrations();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();

app.MapDocumentsEndpoints();
app.MapQueryEndpoints();

app.Run();
