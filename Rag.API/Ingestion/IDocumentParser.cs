namespace Rag.API.Ingestion;

public sealed record PageText(string Text, int PageNumber);

public interface IDocumentParser
{
    IReadOnlyCollection<string> SupportedExtensions { get; }
    IReadOnlyList<PageText> Parse(Stream documentStream);
}
