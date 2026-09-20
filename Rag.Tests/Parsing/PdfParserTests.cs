using Rag.API.Ingestion;

namespace Rag.Tests.Parsing;

public class PdfParserTests
{
    private const int PageCount = 50;

    private static readonly string FixturePath =
        Path.Combine(AppContext.BaseDirectory, "Parsing", "Fixtures", "football.pdf");

    private static IReadOnlyList<PageText> ParseFixture()
    {
        using var stream = File.OpenRead(FixturePath);
        return new PdfParser().Parse(stream);
    }

    private static PageText Page(int number) =>
        ParseFixture().First(p => p.PageNumber == number);

    // ---------- structure ----------

    [Fact]
    public void Parse_ReturnsOnePageTextPerPdfPage()
    {
        var pages = ParseFixture();

        Assert.Equal(PageCount, pages.Count);
    }

    [Fact]
    public void Parse_NumbersPagesStartingAtOne()
    {
        var pages = ParseFixture();

        Assert.Equal(Enumerable.Range(1, PageCount), pages.Select(p => p.PageNumber));
    }

    [Fact]
    public void Parse_NeverReturnsAnEmptyPage()
    {
        var pages = ParseFixture();

        Assert.All(pages, page => Assert.False(string.IsNullOrWhiteSpace(page.Text)));
    }

    // ---------- extraction quality ----------

    [Fact]
    public void Parse_DoesNotFuseWordsAcrossLineBreaks()
    {
        // Regression guard for page.Text, which emits no line breaks and glues the last
        // word of a line to the first word of the next ("LANengagements").
        var page38 = Page(38);

        Assert.Contains("LAN engagements", page38.Text);
        Assert.DoesNotContain("LANengagements", page38.Text);
    }

    [Fact]
    public void Parse_PreservesAccentedAndSymbolCharacters()
    {
        Assert.Contains("legal@cefa.esport", Page(2).Text);
        Assert.Contains("Pabellón del Río", Page(38).Text);
    }

    // ---------- paragraph reconstruction ----------

    [Fact]
    public void Parse_ProducesParagraphBreaks()
    {
        // The chunker's coarsest separator is "\n\n". Raw PDF extraction contains none,
        // so this asserts the whole reason NormalizeParagraphs exists.
        var pages = ParseFixture();

        Assert.All(pages, page => Assert.Contains("\n\n", page.Text));
    }

    [Fact]
    public void Parse_JoinsWrappedLinesIntoParagraphs()
    {
        // Guards against the threshold degenerating so that no join ever fires,
        // which leaves every visual line as its own paragraph.
        // A single wrapped line is at most ~100 chars, so a much longer paragraph
        // can only exist if lines were joined.
        var page38 = Page(38);

        int longestParagraph = page38.Text
            .Split("\n\n")
            .Max(paragraph => paragraph.Length);

        Assert.True(longestParagraph > 300,
            $"Longest paragraph was {longestParagraph} chars; wrapped lines were not joined.");
    }

    [Fact]
    public void Parse_KeepsHeadingsAsTheirOwnParagraphs()
    {
        var page4 = Page(4);

        string[] paragraphs = page4.Text.Split("\n\n");

        Assert.Contains("Article 5 — Promotion and Relegation", paragraphs);
        Assert.Contains("Article 6 — Calendar and Match Windows", paragraphs);
    }

    [Fact]
    public void Parse_CalibratesPerPageSoNarrowLayoutsStillJoin()
    {
        // Page 48 is an appendix of tables: its widest line is ~68 chars against the
        // ~95 of a prose page. A document-wide threshold would exceed every line here
        // and mark all of them as paragraph ends.
        var page48 = Page(48);

        int longestParagraph = page48.Text
            .Split("\n\n")
            .Max(paragraph => paragraph.Length);

        Assert.True(longestParagraph > 200,
            $"Longest paragraph on the narrow page was {longestParagraph} chars.");
    }

    [Fact]
    public void Parse_EmitsNoWindowsLineEndings()
    {
        // The chunker splits on "\n". A surviving "\r" rides along inside every part
        // and only gets trimmed at chunk edges, never in the interior.
        var pages = ParseFixture();

        Assert.All(pages, page => Assert.DoesNotContain('\r', page.Text));
    }

    [Fact]
    public void Parse_DoesNotLeaveBlankParagraphs()
    {
        var pages = ParseFixture();

        foreach (var page in pages)
        {
            Assert.All(
                page.Text.Split("\n\n"),
                paragraph => Assert.False(string.IsNullOrWhiteSpace(paragraph)));
        }
    }

    // ---------- contract ----------

    [Fact]
    public void Parse_DoesNotDisposeTheCallersStream()
    {
        using var stream = File.OpenRead(FixturePath);

        new PdfParser().Parse(stream);

        Assert.True(stream.CanRead);
    }

    [Fact]
    public void Parse_WorksFromAnInMemoryStream()
    {
        // The upload endpoint hands over IFormFile.OpenReadStream(), not a file path.
        using var stream = new MemoryStream(File.ReadAllBytes(FixturePath));

        var pages = new PdfParser().Parse(stream);

        Assert.Equal(PageCount, pages.Count);
    }

    [Fact]
    public void Parse_ThrowsWhenStreamIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => new PdfParser().Parse(null!));
    }

    [Fact]
    public void Parse_ThrowsWhenContentIsNotAPdf()
    {
        using var stream = new MemoryStream("this is not a pdf"u8.ToArray());

        Assert.ThrowsAny<Exception>(() => new PdfParser().Parse(stream));
    }
}
