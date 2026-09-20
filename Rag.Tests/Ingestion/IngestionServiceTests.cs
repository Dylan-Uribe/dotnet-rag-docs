using Microsoft.EntityFrameworkCore;
using Rag.API.Common;
using Rag.API.Contracts;
using Rag.API.Data;
using Rag.API.Domain;
using Rag.API.Ingestion;
using Rag.Tests.TestSupport;

namespace Rag.Tests.Ingestion;

public class IngestionServiceTests
{
    // One chunk per line: alpha and bravo on page 1, charlie on page 2.
    private static readonly PageText[] TwoPages =
    [
        new("alpha\nbravo", 1),
        new("charlie", 2)
    ];

    private static IngestionService CreateService(
        ApplicationDbContext context,
        IDocumentParser parser,
        IChunker? chunker = null,
        FakeEmbeddingService? embeddings = null) =>
        new([parser], chunker ?? new FakeChunker(), embeddings ?? new FakeEmbeddingService(), context);

    private static Task<Result<IngestResponse>> IngestAsync(IngestionService service, string fileName) =>
        service.IngestAsync(new MemoryStream(), fileName);

    // ---------- parser selection ----------

    [Fact]
    public async Task IngestAsync_FailsWithUnsupportedType_WhenNoParserHandlesTheExtension()
    {
        await using ApplicationDbContext context = TestDatabase.CreateContext();
        var parser = new FakeDocumentParser(TwoPages, ".pdf");

        Result<IngestResponse> result = await IngestAsync(CreateService(context, parser), "report.docx");

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.UnsupportedType, result.Error!.Type);
        Assert.Equal(0, parser.CallCount);
    }

    [Fact]
    public async Task IngestAsync_SelectsTheParserIgnoringTheCaseOfTheExtension()
    {
        await using ApplicationDbContext context = TestDatabase.CreateContext();
        var parser = new FakeDocumentParser(TwoPages, ".pdf");

        Result<IngestResponse> result = await IngestAsync(CreateService(context, parser), "REPORT.PDF");

        Assert.True(result.IsSuccess);
        Assert.Equal(1, parser.CallCount);
    }

    // ---------- documents we cannot use ----------

    [Fact]
    public async Task IngestAsync_FailsWithUnprocessable_WhenTheDocumentHasNoExtractableText()
    {
        // A scanned PDF parses without error and yields nothing. It needs OCR, so it
        // is the caller's problem (422), not a server fault.
        await using ApplicationDbContext context = TestDatabase.CreateContext();
        var parser = new FakeDocumentParser([], ".pdf");

        Result<IngestResponse> result = await IngestAsync(CreateService(context, parser), "scan.pdf");

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Unprocessable, result.Error!.Type);
    }

    [Fact]
    public async Task IngestAsync_FailsWithUnprocessable_WhenChunkingProducesNothing()
    {
        await using ApplicationDbContext context = TestDatabase.CreateContext();
        var parser = new FakeDocumentParser(TwoPages, ".pdf");

        IngestionService service = CreateService(context, parser, new FakeChunker(returnsNothing: true));

        Result<IngestResponse> result = await IngestAsync(service, "empty.pdf");

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Unprocessable, result.Error!.Type);
    }

    [Fact]
    public async Task IngestAsync_StoresNothing_WhenTheDocumentIsRejected()
    {
        await using ApplicationDbContext context = TestDatabase.CreateContext();
        var parser = new FakeDocumentParser([], ".pdf");

        await IngestAsync(CreateService(context, parser), "scan.pdf");

        Assert.Empty(await context.Documents.ToListAsync());
        Assert.Empty(await context.DocumentChunks.ToListAsync());
    }

    [Fact]
    public async Task IngestAsync_EmbedsNothing_WhenTheDocumentIsRejected()
    {
        // Rejecting before the embedding call is what keeps a bad upload free.
        await using ApplicationDbContext context = TestDatabase.CreateContext();
        var parser = new FakeDocumentParser([], ".pdf");
        var embeddings = new FakeEmbeddingService();

        await IngestAsync(CreateService(context, parser, embeddings: embeddings), "scan.pdf");

        Assert.Equal(0, embeddings.CallCount);
    }

    // ---------- what ends up in the database ----------

    [Fact]
    public async Task IngestAsync_SavesTheDocumentWithOneChunkPerChunkedText()
    {
        await using ApplicationDbContext context = TestDatabase.CreateContext();

        await IngestAsync(CreateService(context, new FakeDocumentParser(TwoPages, ".pdf")), "handbook.pdf");

        Document document = await context.Documents
            .Include(d => d.DocumentChunks)
            .SingleAsync();

        Assert.Equal("handbook.pdf", document.Name);
        Assert.Equal(3, document.DocumentChunks.Count);
        Assert.Equal(["alpha", "bravo", "charlie"], document.DocumentChunks.Select(c => c.TextContent));
    }

    [Fact]
    public async Task IngestAsync_StoresEachChunkWithTheEmbeddingOfItsOwnText()
    {
        // Chunks and vectors are paired by position. A shift of one would store every
        // chunk under a neighbour's vector: nothing throws, and retrieval is ruined.
        await using ApplicationDbContext context = TestDatabase.CreateContext();

        await IngestAsync(CreateService(context, new FakeDocumentParser(TwoPages, ".pdf")), "handbook.pdf");

        List<DocumentChunk> chunks = await context.DocumentChunks.ToListAsync();

        Assert.All(chunks, chunk =>
            Assert.Equal(TestVectors.For(chunk.TextContent), chunk.Embedding.ToArray()));
    }

    [Fact]
    public async Task IngestAsync_KeepsThePageEachChunkCameFrom()
    {
        // The page number is what the answer cites, so losing it makes the API lie.
        await using ApplicationDbContext context = TestDatabase.CreateContext();

        await IngestAsync(CreateService(context, new FakeDocumentParser(TwoPages, ".pdf")), "handbook.pdf");

        List<DocumentChunk> chunks = await context.DocumentChunks.ToListAsync();

        Assert.Equal(1, chunks.Single(c => c.TextContent == "alpha").PageNumber);
        Assert.Equal(1, chunks.Single(c => c.TextContent == "bravo").PageNumber);
        Assert.Equal(2, chunks.Single(c => c.TextContent == "charlie").PageNumber);
    }

    [Fact]
    public async Task IngestAsync_EmbedsEveryChunkOnceInASingleCall()
    {
        // The service batches internally; calling the embedding service per chunk
        // would multiply both latency and cost.
        await using ApplicationDbContext context = TestDatabase.CreateContext();
        var embeddings = new FakeEmbeddingService();

        await IngestAsync(
            CreateService(context, new FakeDocumentParser(TwoPages, ".pdf"), embeddings: embeddings),
            "handbook.pdf");

        Assert.Equal(1, embeddings.CallCount);
        Assert.Equal(["alpha", "bravo", "charlie"], embeddings.EmbeddedTexts);
    }

    // ---------- what we report back ----------

    [Fact]
    public async Task IngestAsync_ReportsThePageAndChunkCountsOfTheSavedDocument()
    {
        await using ApplicationDbContext context = TestDatabase.CreateContext();

        Result<IngestResponse> result = await IngestAsync(
            CreateService(context, new FakeDocumentParser(TwoPages, ".pdf")), "handbook.pdf");

        Document document = await context.Documents.SingleAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(document.Id, result.Value!.DocumentId);
        Assert.Equal(2, result.Value.PageCount);
        Assert.Equal(3, result.Value.ChunkCount);
    }
}
