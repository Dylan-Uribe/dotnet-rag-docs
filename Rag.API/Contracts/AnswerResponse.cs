namespace Rag.API.Contracts;

public sealed record AnswerResponse(
    string Answer,
    IReadOnlyList<Citation> Citations);

public sealed record Citation(
    string FileName,
    int PageNumber,
    double Distance);
