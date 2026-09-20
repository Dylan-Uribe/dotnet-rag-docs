# RAG API

A small **Retrieval-Augmented Generation (RAG)** API built with .NET 10. Upload a
document, and ask questions that are answered **strictly from its content** —
with citations back to the source pages.

![.NET](https://img.shields.io/badge/.NET-10-512BD4)
![PostgreSQL](https://img.shields.io/badge/PostgreSQL-pgvector-336791)
![OpenAI](https://img.shields.io/badge/OpenAI-embeddings%20%2B%20chat-412991)

## How it works

1. **Ingest** — you upload a PDF. The API extracts its text, splits it into
   overlapping chunks, turns each chunk into an embedding, and stores them in
   Postgres with the [pgvector](https://github.com/pgvector/pgvector) extension.
2. **Query** — you ask a question. The API embeds the question, retrieves the most
   similar chunks by cosine distance, and asks a chat model to answer **using only
   that context**. If the answer isn't in the documents, it says so.

## Tech stack

- **.NET 10**, ASP.NET Core Minimal API
- **EF Core 10** + Npgsql + **pgvector** (HNSW index, cosine distance)
- **Microsoft.Extensions.AI** (`IChatClient` / `IEmbeddingGenerator`) with the OpenAI provider
- **PdfPig** for PDF text extraction, **ML.Tokenizers** for token-aware chunking
- **Scalar** for the interactive OpenAPI reference UI

## Project layout

```
Rag.API/
  Endpoints/     HTTP surface (documents, query)
  Ingestion/     PDF parsing, chunking, ingestion pipeline
  Retrieval/     pgvector similarity search
  Generation/    chat answering over retrieved context
  Query/         query orchestration
  Documents/     document listing / retrieval / deletion
  Embeddings/    embedding generation
  Contracts/     request/response DTOs
  Data/          DbContext, entity configuration, migrations
  Domain/        entities
  Options/       strongly-typed configuration
  Extensions/    dependency-injection wiring
  Common/        Result type, global exception handling

Rag.Tests/       unit tests
Rag.Evals/       evaluation runner and golden sets
```

## Quick start (Docker)

Everything runs with Docker

**Prerequisites:** [Docker](https://www.docker.com/) and an
[OpenAI API key](https://platform.openai.com/api-keys).

> **Note:** This project works only with an **OpenAI** API key. Other providers
> (Anthropic, Gemini, etc.) are not supported without code changes.

#### 1. Clone

```bash
git clone https://github.com/Dylan-Uribe/dotnet-rag-docs.git
cd Rag
```

#### 2. Create your `.env` from the template

```bash
cp .env.example .env
```

Then open `.env` in your editor and set your `POSTGRES_*` credentials and your
`OPENAI_API_KEY` — see [Configuration](#configuration) below for what each value is.

#### 3. Start the stack (API + database)
```bash
docker compose up --build
```

- **API:** http://localhost:8080
- **Interactive docs (Scalar):** http://localhost:8080/scalar

Open Scalar to try every endpoint from the browser.

## Configuration

**Secrets** live in `.env`. Copy `.env.example` and fill it in:

| Variable | Description |
| --- | --- |
| `POSTGRES_USER` | Database username |
| `POSTGRES_PASSWORD` | Database password |
| `POSTGRES_DB` | Database name |
| `OPENAI_API_KEY` | Your OpenAI API key |

**Behavior** is tuned in `Rag.API/appsettings.json`:

| Setting | Default | Description |
| --- | --- | --- |
| `Rag:ChunkSizeTokens` | `350` | Target chunk size (tokens) |
| `Rag:ChunkOverlapTokens` | `70` | Overlap between chunks (tokens) |
| `Rag:TopK` | `8` | Number of chunks retrieved per query |
| `Rag:MaxDistance` | `null` | Optional cosine-distance cutoff for retrieved chunks |
| `OpenAI:EmbeddingModel` | `text-embedding-3-small` | Embedding model |
| `OpenAI:EmbeddingDimensions` | `1536` | Embedding dimension (must match the DB vector column) |
| `OpenAI:ChatModel` | `gpt-4o-mini` | Chat model used to answer |

## API

| Method | Route | Description | Success |
| --- | --- | --- | --- |
| `POST` | `/documents` | Upload and ingest a PDF (`multipart/form-data`, field `file`) | `201 Created` + `Location` |
| `GET` | `/documents` | List ingested documents | `200 OK` |
| `GET` | `/documents/{id}` | Get one document | `200 OK` / `404` |
| `DELETE` | `/documents/{id}` | Delete a document and its chunks | `204 No Content` / `404` |
| `POST` | `/query` | Ask a question over the ingested documents | `200 OK` |

### Examples

The sample document [`football.pdf`](football.pdf) (a fictional esports league
rulebook) is included in the repo so you can try the API right away.

```bash
# Ingest the sample PDF
curl -F "file=@football.pdf" http://localhost:8080/documents

# Ask a question about it
curl -X POST http://localhost:8080/query \
  -H "Content-Type: application/json" \
  -d '{ "question": "How many clubs are in the Elite Division?" }'
```

A sample response from `/query`:

```json
{
  "answer": "The Elite Division comprises sixteen clubs.",
  "citations": [
    { "fileName": "football.pdf", "pageNumber": 4, "distance": 0.21 }
  ]
}
```

Ready-made HTTP requests are also available in... [`Rag.API/Rag.API.http`](Rag.API/Rag.API.http).

## Testing and evaluation

66 unit tests cover chunking, PDF parsing, ingestion, embedding and answer generation.
They need nothing running:

```bash
dotnet test Rag.Tests/Rag.Tests.csproj
```

Tests assert; theses cannot tell whether the system got **better**. That is what
[`Rag.Evals`](Rag.Evals/README.md) is for, four evals over the included `football.pdf`.

| Eval | Measures | Result |
| --- | --- | --- |
| Retrieval | Does the page holding the answer come back? | recall@5 `0.94`, MRR `0.86` |
| Abstention | Does it decline when the corpus cannot answer? | `30/30` |
| Faithfulness | Is every claim grounded, and is the answer right? | `0.97` / `0.88` |
| Injection | Can a hostile document hijack the answer? | `0.88` resistance |

```bash
# needs Postgres running and an OpenAI key
dotnet run --project Rag.Evals -- --eval retrieval
```

A parameter sweep re-runs the retrieval eval across chunking configurations, so
`Rag:ChunkSizeTokens` can be chosen from measurements instead of intuition.

## Documentation

- [`Rag.Evals/README.md`](Rag.Evals/README.md) — the evals in detail: how each one is
  built, what they found, and what they do not measure.
- [`docs/decisions.md`](docs/decisions.md) — design decisions and the reasoning
  behind them, including why the default configuration values were chosen.
- [`docs/sample-questions.md`](docs/sample-questions.md) — sample questions for the
  included `football.pdf`, with expected answers.
