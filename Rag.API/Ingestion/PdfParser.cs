using System.Text;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace Rag.API.Ingestion;

public sealed class PdfParser : IDocumentParser
{
    private const double FullWidthPercentile = 0.90;
    private const int WordSlack = 12;

    public IReadOnlyCollection<string> SupportedExtensions { get; } = [".pdf"];

    public IReadOnlyList<PageText> Parse(Stream documentStream)
    {
        ArgumentNullException.ThrowIfNull(documentStream);

        var pages = new List<PageText>();

        using var document = PdfDocument.Open(documentStream);

        foreach (Page page in document.GetPages())
        {
            string raw = ContentOrderTextExtractor.GetText(page);
            string text = NormalizeParagraphs(raw);

            if (string.IsNullOrWhiteSpace(text)) continue;

            pages.Add(new PageText(text, page.Number));
        }

        return pages;
    }

    private static string NormalizeParagraphs(string pageText)
    {
        if (string.IsNullOrWhiteSpace(pageText)) return string.Empty;

        string[] lines = pageText
            .Replace("\r\n", "\n")
            .Replace('\r', '\n')
            .Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.Length > 0)
            .ToArray();

        if (lines.Length == 0) return string.Empty;
        if (lines.Length == 1) return lines[0];

        double threshold = FullWidth(lines) - WordSlack;

        var builder = new StringBuilder();

        for (int i = 0; i < lines.Length; i++)
        {
            builder.Append(lines[i]);

            if (i == lines.Length - 1) break;

            bool wasWrapped = lines[i].Length >= threshold;
            builder.Append(wasWrapped ? " " : "\n\n");
        }

        return builder.ToString();
    }
    private static double FullWidth(string[] lines)
    {
        int[] lengths = lines.Select(line => line.Length).Order().ToArray();

        int index = (int)(lengths.Length * FullWidthPercentile);

        return lengths[Math.Min(index, lengths.Length - 1)];
    }
}
