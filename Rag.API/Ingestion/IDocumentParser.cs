namespace Rag.API.Ingestion;

public sealed record PageText(string Text, int PageNumber);

public interface IDocumentParser
{
    IReadOnlyList<PageText> Parse(Stream pdfStream);
}
