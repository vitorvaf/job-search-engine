namespace Jobs.Domain.Models;

public sealed record JobSourceRef(
    string Name,
    SourceType Type,
    string Url,
    string? SourceJobId = null);

public sealed record CompanyRef(
    string Name,
    string? Website = null,
    string? Industry = null);

public sealed record SalaryRange(
    decimal? Min,
    decimal? Max,
    string? Currency,
    string? Period);

public sealed record LocationRef(
    string? Country,
    string? State,
    string? City);

public sealed record DedupeInfo(
    string Fingerprint,
    string? ClusterId);

public sealed class JobPosting
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public JobSourceRef Source { get; init; } = default!;
    public string Title { get; init; } = default!;
    public CompanyRef Company { get; init; } = default!;
    public string LocationText { get; init; } = "";
    public LocationRef? Location { get; init; }

    public WorkMode WorkMode { get; init; } = WorkMode.Unknown;
    public Seniority Seniority { get; init; } = Seniority.Unknown;
    public EmploymentType EmploymentType { get; init; } = EmploymentType.Unknown;

    public SalaryRange? Salary { get; init; }
    public string DescriptionText { get; init; } = "";
    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Languages { get; init; } = Array.Empty<string>();

    public DateTimeOffset? PostedAt { get; init; }
    public DateTimeOffset CapturedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastSeenAt { get; init; } = DateTimeOffset.UtcNow;

    public JobStatus Status { get; init; } = JobStatus.Active;
    public DedupeInfo Dedupe { get; init; } = default!;

    public Dictionary<string, object> Metadata { get; init; } = new();
}
