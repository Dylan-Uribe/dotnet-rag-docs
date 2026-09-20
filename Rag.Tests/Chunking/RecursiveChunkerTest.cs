using Microsoft.Extensions.Options;
using Microsoft.ML.Tokenizers;
using Rag.API.Ingestion;
using Rag.API.Options;
using Xunit;
using Xunit.Abstractions;

namespace Rag.Tests.Chunking;

public class RecursiveChunkerTests
{
    private const int ChunkSize = 40;
    private const int OverlapSize = 8;

    private readonly ITestOutputHelper _output;
    private readonly Tokenizer _tokenizer = TiktokenTokenizer.CreateForEncoding("cl100k_base");

    public RecursiveChunkerTests(ITestOutputHelper output) => _output = output;

    private RecursiveChunker CreateChunker(int chunkSize = ChunkSize, int overlap = 0)
    {
        var options = Options.Create(new RagOptions
        {
            ChunkSizeTokens = chunkSize,
            ChunkOverlapTokens = overlap
        });

        return new RecursiveChunker(options, _tokenizer);
    }

    private const string SampleText = """
        Retrieval augmented generation combines search with generation. The model does not memorize your documents. It looks them up at query time.

        Short one.

        This paragraph is deliberately long and contains no blank lines inside it, but it does contain single newlines, which means the chunker must fall down one level to split it apart properly.
        The second line continues the same idea and adds enough words to push the whole thing well past a small token budget.
        A third line follows here so that the line level split has several pieces to greedily pack together into chunks.

        This paragraph has no internal newlines at all so the newline separator will be useless against it and the splitter has to keep descending until it reaches the sentence level. Each sentence here is a reasonable length. The tokenizer should count them separately. Version 3.14 of the spec and the Node.js runtime both appear in this sentence on purpose. Mr. Smith reviewed the document last Tuesday.

        supercalifragilistic-pseudo-token-wall alpha bravo charlie delta echo foxtrot golf hotel india juliet kilo lima mike november oscar papa quebec romeo sierra tango uniform victor whiskey xray yankee zulu one two three four five six seven eight nine ten eleven twelve thirteen fourteen fifteen

        Final paragraph after some messy spacing.
        """;

    // ---------- invariants (run at both overlap settings) ----------

    [Theory]
    [InlineData(0)]
    [InlineData(OverlapSize)]
    public void NoChunkExceedsTheTokenBudget(int overlap)
    {
        var chunks = CreateChunker(overlap: overlap).Chunk(SampleText, pageNumber: 1);

        var oversized = chunks
            .Where(c => _tokenizer.CountTokens(c.Text) > ChunkSize)
            .ToList();

        foreach (var chunk in oversized)
        {
            _output.WriteLine($"OVERSIZED ({_tokenizer.CountTokens(chunk.Text)}): {chunk.Text}");
        }

        Assert.Empty(oversized);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(OverlapSize)]
    public void NoChunkIsEmptyOrWhitespace(int overlap)
    {
        var chunks = CreateChunker(overlap: overlap).Chunk(SampleText, pageNumber: 1);
        Assert.All(chunks, c => Assert.False(string.IsNullOrWhiteSpace(c.Text)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(OverlapSize)]
    public void NoChunkHasLeadingOrTrailingWhitespace(int overlap)
    {
        var chunks = CreateChunker(overlap: overlap).Chunk(SampleText, pageNumber: 1);
        Assert.All(chunks, c => Assert.Equal(c.Text.Trim(), c.Text));
    }

    [Fact]
    public void ProducesChunks()
    {
        var chunks = CreateChunker().Chunk(SampleText, pageNumber: 1);
        Assert.NotEmpty(chunks);
    }

    [Fact]
    public void EveryChunkCarriesThePageNumber()
    {
        var chunks = CreateChunker(overlap: OverlapSize).Chunk(SampleText, pageNumber: 7);
        Assert.All(chunks, c => Assert.Equal(7, c.PageNumber));
    }

    [Theory]
    [InlineData("Retrieval")]
    [InlineData("Short one")]
    [InlineData("greedily")]
    [InlineData("Node.js")]
    [InlineData("supercalifragilistic")]
    [InlineData("Final paragraph")]
    public void NoContentIsLost(string expectedFragment)
    {
        var chunks = CreateChunker().Chunk(SampleText, pageNumber: 1);
        var combined = string.Join(" ", chunks.Select(c => c.Text));

        Assert.Contains(expectedFragment, combined);
    }

    [Fact]
    public void ShortTextThatFitsBecomesASingleChunk()
    {
        var chunks = CreateChunker().Chunk("A very short page.", pageNumber: 1);

        Assert.Single(chunks);
        Assert.Equal("A very short page.", chunks[0].Text);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("   \n\n   \n  ")]
    public void EmptyOrWhitespaceInputReturnsNoChunks(string input)
    {
        var chunks = CreateChunker(overlap: OverlapSize).Chunk(input, pageNumber: 1);
        Assert.Empty(chunks);
    }

    [Fact]
    public void ChunksAreInDocumentOrder()
    {
        var chunks = CreateChunker().Chunk(SampleText, pageNumber: 1);
        var combined = string.Join(" ", chunks.Select(c => c.Text));

        var first = combined.IndexOf("Retrieval", StringComparison.Ordinal);
        var last = combined.IndexOf("Final paragraph", StringComparison.Ordinal);

        Assert.True(first >= 0 && last > first, "Chunks are not in document order.");
    }

    // ---------- overlap-specific ----------

    [Fact]
    public void OverlapProducesMoreChunksThanNoOverlap()
    {
        var without = CreateChunker(overlap: 0).Chunk(SampleText, pageNumber: 1);
        var with = CreateChunker(overlap: OverlapSize).Chunk(SampleText, pageNumber: 1);

        _output.WriteLine($"overlap 0  -> {without.Count} chunks");
        _output.WriteLine($"overlap {OverlapSize}  -> {with.Count} chunks");

        Assert.True(with.Count > without.Count,
            $"Expected more chunks with overlap, got {with.Count} vs {without.Count}.");
    }

    [Fact]
    public void SomeConsecutiveChunksShareText()
    {
        var chunks = CreateChunker(overlap: OverlapSize).Chunk(SampleText, pageNumber: 1);

        int sharingPairs = 0;

        for (int i = 0; i < chunks.Count - 1; i++)
        {
            var tailWords = chunks[i].Text
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .TakeLast(3);

            string tail = string.Join(' ', tailWords);

            if (chunks[i + 1].Text.Contains(tail, StringComparison.Ordinal))
            {
                sharingPairs++;
                _output.WriteLine($"[{i}] -> [{i + 1}] shares: \"{tail}\"");
            }
        }

        _output.WriteLine($"Sharing pairs: {sharingPairs} of {chunks.Count - 1}");
        Assert.True(sharingPairs > 0, "No consecutive chunks shared text; overlap is not working.");
    }

    [Fact]
    public void ZeroOverlapChangesNothing()
    {
        var a = CreateChunker(overlap: 0).Chunk(SampleText, pageNumber: 1);
        var b = CreateChunker(overlap: 0).Chunk(SampleText, pageNumber: 1);

        Assert.Equal(a.Count, b.Count);
        Assert.Equal(a.Select(c => c.Text), b.Select(c => c.Text));
    }

    // ---------- visual inspection ----------

    [Theory]
    [InlineData(0)]
    [InlineData(OverlapSize)]
    public void DumpChunksForVisualInspection(int overlap)
    {
        var chunks = CreateChunker(overlap: overlap).Chunk(SampleText, pageNumber: 1);

        _output.WriteLine($"### OVERLAP = {overlap} | chunk size = {ChunkSize}");
        _output.WriteLine($"Total chunks: {chunks.Count}");
        _output.WriteLine(new string('=', 60));

        for (int i = 0; i < chunks.Count; i++)
        {
            var tokens = _tokenizer.CountTokens(chunks[i].Text);
            _output.WriteLine($"[{i}] page {chunks[i].PageNumber} | {tokens} tokens");
            _output.WriteLine(chunks[i].Text);
            _output.WriteLine(new string('-', 60));
        }

        Assert.True(true);
    }
}