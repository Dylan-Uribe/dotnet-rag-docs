using System.ComponentModel.DataAnnotations;

namespace Rag.API.Contracts;

public sealed record QueryRequest(
    [property: Required]
    [property: MaxLength(1000)]
    string Question);
