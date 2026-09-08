namespace Rag.API.Retrieval;

public sealed record RetrievedChunk(
    string FileName,
    int PageNumber,
    string Text,
    double Distance);
