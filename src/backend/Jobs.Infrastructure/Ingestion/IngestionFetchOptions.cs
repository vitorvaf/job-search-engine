namespace Jobs.Infrastructure.Ingestion;

public sealed record IngestionFetchOptions(
    int MaxItemsPerRun,
    int MaxDetailFetch);
