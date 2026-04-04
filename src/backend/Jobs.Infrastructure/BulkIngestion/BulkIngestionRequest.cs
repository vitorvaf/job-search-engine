namespace Jobs.Infrastructure.BulkIngestion;

/// <summary>Request payload for bulk job ingestion.</summary>
public sealed class BulkIngestionRequest
{
    /// <summary>Name of the external system sending the batch (e.g. "Firecrawl", "n8n").</summary>
    public string SourceName { get; set; } = string.Empty;

    /// <summary>Source type identifier for the batch (e.g. "ExternalIngestion").</summary>
    public string SourceType { get; set; } = "ExternalIngestion";

    /// <summary>Job items to ingest. Maximum 100 items per request.</summary>
    public List<BulkIngestionItemRequest> Items { get; set; } = new();
}

/// <summary>A single job item within a bulk ingestion request.</summary>
public sealed class BulkIngestionItemRequest
{
    /// <summary>Unique identifier assigned by the source system.</summary>
    public string? SourceJobId { get; set; }

    /// <summary>URL of the job on the crawled/source system.</summary>
    public string? SourceUrl { get; set; }

    /// <summary>Canonical URL of the job on the company website. Used as primary idempotency key.</summary>
    public string? OriginUrl { get; set; }

    /// <summary>Job title. Required.</summary>
    public string? Title { get; set; }

    /// <summary>Company information. Required (company.name is mandatory).</summary>
    public BulkIngestionCompanyRequest? Company { get; set; }

    /// <summary>Free-text location string (e.g. "São Paulo, SP").</summary>
    public string? LocationText { get; set; }

    /// <summary>Work mode: Remote, Hybrid, Onsite, or Unknown.</summary>
    public string? WorkMode { get; set; }

    /// <summary>Seniority level: Intern, Junior, Mid, Senior, Staff, Lead, Principal, or Unknown.</summary>
    public string? Seniority { get; set; }

    /// <summary>Employment type: CLT, PJ, Contractor, Internship, Temporary, or Unknown.</summary>
    public string? EmploymentType { get; set; }

    /// <summary>Full job description text.</summary>
    public string? DescriptionText { get; set; }

    /// <summary>Technology/skill tags (e.g. ["dotnet", "azure"]).</summary>
    public List<string>? Tags { get; set; }

    /// <summary>Language codes for the job posting (e.g. ["pt-BR"]).</summary>
    public List<string>? Languages { get; set; }

    /// <summary>Date the job was originally posted on the source.</summary>
    public DateTimeOffset? PostedAt { get; set; }

    /// <summary>Additional metadata from the source system (merged into stored metadata).</summary>
    public Dictionary<string, object>? Metadata { get; set; }

    // Per-item source override (optional — falls back to request-level SourceName/SourceType)
    public string? SourceName { get; set; }
    public string? SourceType { get; set; }
}

/// <summary>Company information within a bulk ingestion item.</summary>
public sealed class BulkIngestionCompanyRequest
{
    /// <summary>Company name. Required.</summary>
    public string? Name { get; set; }

    /// <summary>Company website URL.</summary>
    public string? Website { get; set; }

    /// <summary>Industry sector.</summary>
    public string? Industry { get; set; }
}
