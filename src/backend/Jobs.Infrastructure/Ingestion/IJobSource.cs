using Jobs.Domain.Models;

namespace Jobs.Infrastructure.Ingestion;

public interface IJobSource
{
    string Name { get; }
    SourceType Type { get; }

    // Retorna vagas já "parseadas" no formato normalizado
    IAsyncEnumerable<JobPosting> FetchAsync(IngestionFetchOptions options, CancellationToken ct);
}
