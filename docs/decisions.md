# Design decisions & rationale

This document records the **why** behind the project's technical choices

## Guiding approach

**Portfolio demo:** The goal is to show a working RAG pipeline
**and** solid REST / DI / C# practices.

## AI providers behind Microsoft.Extensions.AI

- Both the chat model and the embedding model are consumed through the
  **`IChatClient`** / **`IEmbeddingGenerator`** abstractions. 
  This makes the services **testable**.
- **Swapping provider is a one-place change** in
  `Extensions/ServiceCollectionExtensions.cs` (`AddAiProviders`).
- OpenAI is the provider used here, so the config is named `OpenAIOptions`.

**Note:** In the future `OpenAIOptions` could become `AIOptions` if the project
supported swapping in any AI provider.

## Document parsing is extensible by file type

- `IngestionService` receives **`IEnumerable<IDocumentParser>`** and selects a
  parser by matching the file extension against each parser's
  `SupportedExtensions`.
- **Adding a new document type** (e.g. Excel) = a new `IDocumentParser` class +
  one registration line in `AddApplicationServices`.

## Chunking

- The recursive, token-aware chunker (`RecursiveChunker`) is a custom solution. Chunking strategy directly affects retrieval quality, so it's worth
  owning.
- Everything that is commodity infrastructure is delegated to libraries:
  pgvector (vector search), PdfPig (PDF extraction), ML.Tokenizers (tokenizing).

## Endpoints are HTTP-only

- Endpoints translate HTTP ↔ services and nothing more. Query orchestration
  (retrieve + generate) was extracted into `QueryService` so the `/query` endpoint
  stays thin. Each service has an interface and is registered in DI.

## DTOs are returned directly from services

- Services return the `Contracts/` DTOs directly (e.g. `IngestionService` returns
  `IngestResponse`)

## Options pattern with fail-fast validation

- Each options class has a `SectionName` const and DataAnnotations, and is bound
  with `.BindConfiguration(SectionName).ValidateDataAnnotations().ValidateOnStart()`.
  Invalid configuration fails **at startup**, not on the first request.
- Cross-field rules use `.Validate(...)` (e.g. chunk overlap must be smaller than
  chunk size).
- `BindConfiguration` (over `.Bind(config.GetSection(...))`) lets the registration
  method resolve `IConfiguration` from DI, so it needs no `IConfiguration`
  parameter.

## Composition root

- `Program.cs` is a thin composition root. Service registration is grouped by
  concern into extension methods in `Extensions/ServiceCollectionExtensions.cs`
  (`AddApiServices`, `AddPersistence`, `AddConfiguredOptions`, `AddAiProviders`,
  `AddApplicationServices`), and startup migration into
  `Extensions/WebApplicationExtensions.cs` (`ApplyMigrations`). This keeps
  `Program.cs` readable as a table of contents.

## Persistence

- **EF Core + Npgsql + pgvector**, with an HNSW index and cosine distance.
- **The vector dimension is fixed at the schema level (`vector(1536)`).** This is
  inherent to how pgvector indexes vectors — the column type carries the
  dimension. Changing the embedding model/dimension therefore requires a new
  migration **and** re-ingesting everything (the old and new vectors live in
  different vector spaces and aren't comparable). A fail-fast dimension guard in
  `EmbeddingService` catches a mismatch early.
- **Migrations run automatically on startup** so `docker compose up` needs no
  manual migration step.
- **IDs use `Guid.CreateVersion7()`** (time-ordered UUIDv7) instead of
  `Guid.NewGuid()`. Sequential-ish IDs reduce primary-key index fragmentation and
  improve write locality when inserting many chunks at once.

## Request validation without FluentValidation

- Query input is validated with **DataAnnotations + .NET 10's native minimal-API
  validation** (`AddValidation()`), which produces a 400 `problem+json` before the
  handler runs. FluentValidation was deliberately **not** adopted because the only rule
  is trivial (`Question` required, max length), so it wouldn't justify the extra
  dependency and wiring.

---

# Configuration defaults (the why)

These are the defaults in `Rag.API/appsettings.json`.

## Ingestion & retrieval (`Rag`)

| Setting | Default | Why this value |
| --- | --- | --- |
| `ChunkSizeTokens` | `350` | Large enough to keep a coherent idea in one chunk, small enough that a retrieved chunk is precise and not padded with unrelated text. Well within the embedding model's input limit. |
| `ChunkOverlapTokens` | `70` | ~20% of the chunk size. Overlap carries context across chunk boundaries so a sentence split between two chunks isn't lost to retrieval. Enough to help, not so much that it bloats storage with duplication. Must be smaller than `ChunkSizeTokens` (enforced at startup). |
| `TopK` | `8` | How many chunks are retrieved per question. Enough to give the chat model sufficient context to answer, without diluting the prompt with weak matches or making it needlessly long/expensive. |
| `MaxDistance` | `null` | No distance cutoff by default — always return the `TopK` nearest chunks. Set a value (cosine distance) to drop weak matches, at the risk of returning nothing for a genuinely unrelated question. Left off by default so the demo always shows retrieved context. |

## OpenAI (`OpenAI`)

| Setting | Default | Why this value |
| --- | --- | --- |
| `EmbeddingModel` | `text-embedding-3-small` | Strong quality-to-cost ratio for retrieval; inexpensive enough for a demo where anyone brings their own key. |
| `EmbeddingDimensions` | `1536` | The native dimension of `text-embedding-3-small`, and it **must** match the `vector(1536)` database column. Changing it requires a new migration and re-ingestion (see Persistence above). |
| `ChatModel` | `gpt-4o-mini` | Fast and cheap, and more than capable of answering strictly from provided context — the task here is grounded extraction/summarisation, not open-ended reasoning. |
| `EmbeddingBatchSize` | `64` | Chunks are embedded in batches for throughput (fewer round-trips) while staying comfortably within request-size limits. |
