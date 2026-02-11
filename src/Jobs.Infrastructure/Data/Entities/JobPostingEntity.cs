using Jobs.Domain.Models;

namespace Jobs.Infrastructure.Data.Entities;

public sealed class JobPostingEntity
{
    public Guid Id { get; set; }

    public string SourceName { get; set; } = default!;
    public SourceType SourceType { get; set; }
    public string SourceUrl { get; set; } = default!;
    public string? SourceJobId { get; set; }

    public string Title { get; set; } = default!;
    public string CompanyName { get; set; } = default!;
    public string? CompanyWebsite { get; set; }
    public string? CompanyIndustry { get; set; }

    public string LocationText { get; set; } = "";
    public string? Country { get; set; }
    public string? State { get; set; }
    public string? City { get; set; }

    public WorkMode WorkMode { get; set; }
    public Seniority Seniority { get; set; }
    public EmploymentType EmploymentType { get; set; }

    public decimal? SalaryMin { get; set; }
    public decimal? SalaryMax { get; set; }
    public string? SalaryCurrency { get; set; }
    public string? SalaryPeriod { get; set; }

    public string DescriptionText { get; set; } = "";

    public string[] Tags { get; set; } = Array.Empty<string>();
    public string[] Languages { get; set; } = Array.Empty<string>();

    public DateTimeOffset? PostedAt { get; set; }
    public DateTimeOffset CapturedAt { get; set; }
    public DateTimeOffset LastSeenAt { get; set; }

    public JobStatus Status { get; set; }

    public string Fingerprint { get; set; } = default!;
    public string? ClusterId { get; set; }

    public string MetadataJson { get; set; } = "{}";
}
