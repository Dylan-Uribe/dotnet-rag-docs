using Rag.API.Common;
using Rag.API.Contracts;

namespace Rag.API.Ingestion;

public interface IIngestionService
{
    Task<Result<IngestResponse>> IngestAsync(Stream documentStream, string fileName);
}
