namespace Jobs.Infrastructure.Data.Entities;

public sealed class IngestionRunEntity
{
    public Guid Id { get; set; }
    public Guid SourceId { get; set; }

    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public string Status { get; set; } = "Running";

    public int Fetched { get; set; }
    public int Parsed { get; set; }
    public int Normalized { get; set; }
    public int Indexed { get; set; }
    public int Duplicates { get; set; }
    public int Errors { get; set; }

    public string? ErrorSample { get; set; }
}
