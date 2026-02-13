namespace Jobs.Infrastructure.Ingestion;

public sealed class ParsedSourceJob
{
    public string Title { get; set; } = string.Empty;
    public string Company { get; set; } = "Unknown";
    public string LocationText { get; set; } = string.Empty;
    public DateTimeOffset? PostedAt { get; set; }
    public string? SalaryText { get; set; }
    public string? DescriptionText { get; set; }
    public string Url { get; set; } = string.Empty;
    public string? SourceJobId { get; set; }
    public string? WorkModeText { get; set; }
}
