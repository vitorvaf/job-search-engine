namespace Jobs.Domain.Models;

public sealed class IngestionRun
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid SourceId { get; init; }
    public DateTimeOffset StartedAt { get; init; } = DateTimeOffset.UtcNow;
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
